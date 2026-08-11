import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import type { RunTerminal as RunTerminalAvailability } from "./useRuns";

/**
 * A shell in the Run's sandbox, beside the agent (#304). The preview's sibling — both exist only
 * while the Run does, and both disappear the same way — and its opposite in what it permits: a
 * preview renders what the agent built, this executes commands on the machine the agent is using.
 *
 * Opened on a click, never on render. A terminal that attached itself the moment somebody looked at
 * a Run would spend a sandbox's resources on curiosity, and would record an attach nobody made.
 *
 * The size is measured once, when the shell opens. Resizing a live pseudo-terminal needs a system
 * call .NET cannot make, so the copy says so rather than leaving a reader waiting for a reflow that
 * is never coming.
 */
export function RunTerminal({
  projectId,
  runId,
  terminal,
  runFinished,
}: {
  projectId: string;
  runId: string;
  terminal: RunTerminalAvailability | undefined;
  runFinished: boolean;
}) {
  const host = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [state, setState] = useState<"connecting" | "live" | "ended">("connecting");

  // Whether this reader ever had a shell open. A Run that was already finished when the page opened
  // must show nothing; one that ENDED while somebody was typing is a different moment, and letting
  // the terminal vanish unexplained would read as a glitch.
  const [wasOpen, setWasOpen] = useState(false);

  useEffect(() => {
    if (!open || host.current === null) return;

    let cancelled = false;
    let connection: HubConnection | undefined;
    let disposeTerminal: (() => void) | undefined;

    void (async () => {
      // Mock mode has no server to open a shell on, and the dynamic imports keep xterm out of the
      // bundle every reader who never opens a terminal downloads.
      if (import.meta.env.MODE === "mock") return;

      const [{ Terminal }, { FitAddon }, { HubConnectionBuilder }] = await Promise.all([
        import("@xterm/xterm"),
        import("@xterm/addon-fit"),
        import("@microsoft/signalr"),
      ]);
      await import("@xterm/xterm/css/xterm.css");
      if (cancelled || host.current === null) return;

      const term = new Terminal({
        fontSize: 13,
        fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
        cursorBlink: true,
        convertEol: false,
      });
      const fit = new FitAddon();
      term.loadAddon(fit);
      term.open(host.current);
      fit.fit();
      disposeTerminal = () => term.dispose();

      connection = new HubConnectionBuilder().withUrl("/hubs/run-terminal").build();

      // Binary frames, reassembled by xterm: a UTF-8 sequence split across two reads would corrupt
      // if it were decoded as text here, and a terminal splits sequences constantly.
      connection.on("output", (chunk: string) => term.write(base64ToBytes(chunk)));
      connection.on("ended", () => {
        setState("ended");
        void connection?.stop();
      });
      connection.onclose(() => setState("ended"));

      term.onData((data) => {
        void connection?.invoke("Send", bytesToBase64(new TextEncoder().encode(data)));
      });

      try {
        await connection.start();
        // The size travels once, here. See the class comment for why it cannot travel again.
        await connection.invoke("Open", runId, term.cols, term.rows);
        if (!cancelled) {
          setState("live");
          setWasOpen(true);
          term.focus();
        }
      } catch (error) {
        // The hub's refusals are sentences a person can act on — no permission, no terminal here,
        // no executing Run — so they are shown rather than replaced with a generic failure.
        setState("ended");
        term.write(`\r\n\x1b[31m${(error as Error).message}\x1b[0m\r\n`);
      }
    })();

    return () => {
      cancelled = true;
      void connection?.stop();
      disposeTerminal?.();
    };
  }, [open, runId, projectId]);

  // Never hosted here: not a disabled button, because asking for access would not help and an
  // affordance implying otherwise promises what this habitat cannot keep (ADR-0021).
  if (terminal !== undefined && !terminal.hosted) {
    return null;
  }

  if (wasOpen && runFinished) {
    return (
      <Card className="gap-0 py-0">
        <p className="px-4 py-3 text-sm text-muted-foreground" role="status">
          {t("run.terminal.ended")}
        </p>
      </Card>
    );
  }

  if (terminal === undefined || !terminal.available) {
    return null;
  }

  if (!terminal.permitted) {
    return (
      <Card className="gap-0 py-0">
        <p className="px-4 py-3 text-sm text-muted-foreground" role="status">
          {t("run.terminal.forbidden")}
        </p>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <span className="flex flex-wrap items-center gap-2">
        <h2 className="text-sm font-semibold">{t("run.terminal.heading")}</h2>
        {open && state === "live" ? (
          <Badge variant="outline" className="border-info/40 bg-info/10 text-info">
            <span className="size-1.5 animate-pulse rounded-full bg-info" aria-hidden="true" />
            {t("run.log.live")}
          </Badge>
        ) : null}
        <span className="text-xs text-muted-foreground">{t("run.terminal.whose")}</span>
      </span>

      {open ? (
        <Card className="gap-0 overflow-hidden py-0">
          <div ref={host} className="h-[24rem] w-full bg-surface p-2" />
          <p className="border-t px-4 py-2 text-xs text-muted-foreground">
            {state === "connecting" ? t("run.terminal.connecting") : t("run.terminal.fixedSize")}
          </p>
        </Card>
      ) : (
        <Card className="gap-0 py-0">
          <div className="px-4 py-3">
            <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
              {t("run.terminal.open")}
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}

// SignalR's JSON protocol carries byte[] as base64 in both directions; these two are that wire
// format and nothing more. The cost is ~33% on output, accepted because the alternative was
// hand-rolling the authorization the hub already gets from being a hub.
function base64ToBytes(value: string): Uint8Array {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

function bytesToBase64(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}
