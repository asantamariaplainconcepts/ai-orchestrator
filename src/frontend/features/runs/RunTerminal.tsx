import { useState } from "react";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { TerminalPane } from "@/shared/ui/terminal-pane";
import type { RunTerminal as RunTerminalAvailability } from "./useRuns";

/**
 * A shell in the Run's sandbox, beside the agent (#304). The preview's sibling — both exist only
 * while the Run does, and both disappear the same way — and its opposite in what it permits: a
 * preview renders what the agent built, this executes commands on the machine the agent is using.
 *
 * Opened on a click, never on render. A terminal that attached itself the moment somebody looked at
 * a Run would spend a sandbox's resources on curiosity, and would record an attach nobody made.
 *
 * The transport lives in {@link TerminalPane}, shared with the machine's sandboxes surface (#311).
 * What stays here is what is specific to a Run: the three availability answers, and the fact that a
 * Run can end underneath a reader who is typing.
 */
export function RunTerminal({
  runId,
  terminal,
  runFinished,
}: {
  runId: string;
  terminal: RunTerminalAvailability | undefined;
  runFinished: boolean;
}) {
  const [open, setOpen] = useState(false);

  // Whether this reader ever had a shell open. A Run that was already finished when the page opened
  // must show nothing; one that ENDED while somebody was typing is a different moment, and letting
  // the terminal vanish unexplained would read as a glitch.
  const [wasOpen, setWasOpen] = useState(false);

  // Whether one is open RIGHT NOW, which is a different question and needs its own answer. Reusing
  // `wasOpen` for the badge left it reading "live" over a shell that had already ended, because
  // "ever" never becomes false — the sandboxes surface has kept the two apart since #311.
  const [live, setLive] = useState(false);

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
        {open && live ? (
          <Badge variant="outline" className="border-info/40 bg-info/10 text-info">
            <span className="size-1.5 animate-pulse rounded-full bg-info" aria-hidden="true" />
            {t("run.log.live")}
          </Badge>
        ) : null}
        <span className="text-xs text-muted-foreground">{t("run.terminal.whose")}</span>
      </span>

      {open ? (
        <TerminalPane
          invoke={(connection, columns, rows) => connection.invoke("Open", runId, columns, rows)}
          onLive={() => {
            setWasOpen(true);
            setLive(true);
          }}
          onEnded={() => setLive(false)}
        />
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
