import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { t } from "@/shared/i18n";
import { Card } from "@/shared/ui/card";

/**
 * One open shell in a browser: xterm, the hub connection, and the byte pump between them.
 *
 * Extracted from the Run's terminal when the machine's sandboxes gained one of their own (#311).
 * The two surfaces differ only in which hub method opens the shell and what it is keyed by — a Run's
 * id, or a sandbox's name — and everything below that is identical. A second copy would have been two
 * places to get the base64 framing wrong.
 *
 * Mounted only once a reader has asked for a shell, never on render: a terminal that attached itself
 * the moment somebody looked at a page would spend a sandbox's resources on curiosity, and would
 * record an attach nobody made.
 *
 * The size is measured once, here. Resizing a live pseudo-terminal needs a system call .NET cannot
 * make, so the footer says so rather than leaving a reader waiting for a reflow that is never coming.
 */
export function TerminalPane({
  invoke,
  onLive,
  onEnded,
}: {
  /** How this surface opens its shell: the hub method and its key, at the measured geometry. */
  invoke: (connection: HubConnection, columns: number, rows: number) => Promise<void>;
  onLive?: () => void;
  onEnded?: () => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  // "failed" is not a kind of "ended": a shell that never opened has not ended, and telling a reader
  // to open one again is wrong advice when the hub refused because somebody else already holds it.
  const [state, setState] = useState<"connecting" | "live" | "ended" | "failed">("connecting");

  // Held in refs so a parent that re-renders (a poll landing, say) cannot tear down a live shell by
  // passing a new closure. The pane opens exactly once per mount, which is what the parent controls.
  const latest = useRef({ invoke, onLive, onEnded });
  latest.current = { invoke, onLive, onEnded };

  useEffect(() => {
    if (host.current === null) return;

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
        latest.current.onEnded?.();
        void connection?.stop();
      });
      connection.onclose(() => {
        // Only a shell that was live can end. A connection closing behind a refusal must not
        // overwrite it with a sentence about a shell that never opened.
        setState((current) => (current === "live" ? "ended" : current));
        latest.current.onEnded?.();
      });

      term.onData((data) => {
        void connection?.invoke("Send", bytesToBase64(new TextEncoder().encode(data)));
      });

      try {
        await connection.start();
        // The size travels once, here. See the comment above for why it cannot travel again.
        await latest.current.invoke(connection, term.cols, term.rows);
        if (!cancelled) {
          setState("live");
          latest.current.onLive?.();
          term.focus();
        }
      } catch (error) {
        // The hub's refusals are sentences a person can act on — no permission, no terminal here,
        // not this machine's sandbox — so they are shown rather than replaced with a generic failure.
        setState("failed");
        term.write(`\r\n\x1b[31m${(error as Error).message}\x1b[0m\r\n`);
      }
    })();

    return () => {
      cancelled = true;
      void connection?.stop();
      disposeTerminal?.();
    };
  }, []);

  return (
    <Card className="gap-0 overflow-hidden py-0">
      <div ref={host} className="h-[24rem] w-full bg-surface p-2" />
      {/* Three states, three sentences. Two of them shared one line until a terminal that opened and
          then said nothing was indistinguishable from a working one: "sized to this window" is a
          fact about a LIVE shell, and reading it over a dead one is what made the failure
          unreadable. `role="status"` because the transition happens without a click. */}
      <p className="border-t px-4 py-2 text-xs text-muted-foreground" role="status">
        {state === "connecting"
          ? t("run.terminal.connecting")
          : state === "ended"
            ? t("run.terminal.shellEnded")
            : state === "failed"
              ? t("run.terminal.notOpened")
              : t("run.terminal.fixedSize")}
      </p>
    </Card>
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
