import { useState } from "react";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { TerminalPane } from "@/shared/ui/terminal-pane";
import { sandboxIsStopped, type SandboxRow } from "./useSandboxes";

/**
 * One sandbox on this machine, and a shell in it (#311).
 *
 * The Run's terminal keyed by a sandbox's name instead of a Run's id — same transport, same hub,
 * different question. This one has to carry two things the Run's never did: a sandbox may belong to
 * no Run at all, and it may be stopped, in which case opening a shell **starts** it. Both are said
 * plainly rather than discovered.
 */
export function SandboxTerminal({ sandbox }: { sandbox: SandboxRow }) {
  const [open, setOpen] = useState(false);
  const [live, setLive] = useState(false);
  const stopped = sandboxIsStopped(sandbox);

  return (
    <Card className="gap-0 py-0">
      <div className="flex flex-wrap items-center gap-2 px-4 py-3">
        <span className="font-mono text-sm">{sandbox.name}</span>

        <Badge variant="outline" className={stopped ? "text-muted-foreground" : undefined}>
          {sandbox.status}
        </Badge>

        {sandbox.runId === null ? (
          <span className="text-xs text-muted-foreground">{t("sandboxes.noRun")}</span>
        ) : (
          <span className="text-xs text-muted-foreground">{t("sandboxes.itsRun")}</span>
        )}

        {open && live ? (
          <Badge variant="outline" className="border-info/40 bg-info/10 text-info">
            <span className="size-1.5 animate-pulse rounded-full bg-info" aria-hidden="true" />
            {t("run.log.live")}
          </Badge>
        ) : null}

        {!open && (
          <Button
            variant="outline"
            size="sm"
            className="ml-auto"
            onClick={() => setOpen(true)}
            // The accessible name carries which sandbox, because the visible label cannot: a page of
            // "Open a terminal" buttons is a page a screen reader cannot tell apart.
            aria-label={`${t("sandboxes.openOn")} ${sandbox.name}`}
          >
            {t("sandboxes.open")}
          </Button>
        )}
      </div>

      {sandbox.workspace !== null && (
        <p className="border-t px-4 py-2 font-mono text-xs text-muted-foreground">
          {sandbox.workspace}
        </p>
      )}

      {/* Said before the click can happen, not after: `sbx exec` on a stopped sandbox starts it, so
          an unwarned reader would boot a virtual machine by looking for one. */}
      {stopped && !open && (
        <p className="border-t px-4 py-2 text-xs text-warning">{t("sandboxes.startsIt")}</p>
      )}

      {open && (
        <div className="border-t p-2">
          <TerminalPane
            invoke={(connection, columns, rows) =>
              connection.invoke("OpenSandbox", sandbox.name, columns, rows)
            }
            onLive={() => setLive(true)}
            onEnded={() => setLive(false)}
          />
        </div>
      )}
    </Card>
  );
}
