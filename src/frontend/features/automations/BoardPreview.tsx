import { ChevronDown, ChevronRight, UserRound } from "lucide-react";
import { useState } from "react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { GateChip } from "@/shared/ui/gate-chip";
import type { Automation } from "./types";
import { claimantsByToStage, fold, holds } from "./workflowGraph";

/**
 * What this project's lifecycle makes of the Backlog board, shown beside the catalogue that produces
 * it (#310, design D7).
 *
 * Re-parented from the deleted canvas, which was its only caller, and rewritten to read the **stored**
 * stage list rather than re-derive one. The dedupe it used to do is gone: it existed because a branch
 * row re-entered the board at an existing column, and with branching unrepresentable a stage appears
 * once because the list holds it once.
 *
 * Read-only on purpose, and now for a sharper reason than before. The board *is* the authoring surface,
 * so this is the consequence of what the catalogue holds — not a second place to arrange it. Two places
 * to change one thing is exactly what ADR-0022 was written about.
 */
export function BoardPreview({
  stages,
  automations,
}: {
  /** The stored lifecycle, in order. Empty means nothing has claimed a transition yet. */
  stages: string[];
  automations: Automation[];
}) {
  const [open, setOpen] = useState(true);

  if (stages.length === 0) {
    return null;
  }

  const claimants = claimantsByToStage(automations);

  return (
    <div className="flex flex-col gap-2">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        className="flex items-center gap-1.5 self-start text-xs font-semibold text-muted-foreground outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
      >
        {open ? (
          <ChevronDown className="size-3.5" aria-hidden="true" />
        ) : (
          <ChevronRight className="size-3.5" aria-hidden="true" />
        )}
        {t("preview.title")}
        <span className="font-normal">{t("preview.hint")}</span>
        <span className="sr-only">{open ? t("preview.hide") : t("preview.show")}</span>
      </button>

      {open ? (
        <div className="flex gap-2 overflow-x-auto pb-1">
          {/* Where Stories start. Named rather than implied: a board whose first column is a stage
              would say work begins already labelled, which is not how any of this starts. */}
          <PreviewColumn label={t("preview.untouched")} hint={t("preview.untouchedHint")} />

          {stages.map((stage) => {
            // The Automation moving Stories *into* this stage, which is the boundary before it. An
            // absent one is a person's turn (BR-006) and not a fault.
            const claimant = claimants.get(fold(stage));
            const gated = holds(claimant);

            return (
              <PreviewColumn
                key={stage}
                label={stage}
                hint={
                  claimant === undefined
                    ? t("preview.personHint")
                    : gated
                      ? t("preview.gate")
                      : t("preview.noApproval")
                }
                gated={gated}
                waiting={claimant === undefined}
              />
            );
          })}
        </div>
      ) : null}
    </div>
  );
}

function PreviewColumn({
  label,
  hint,
  gated,
  waiting,
}: {
  label: string;
  hint: string;
  gated?: boolean;
  /** Nobody claims the transition into this stage, so a person carries the work across it. */
  waiting?: boolean;
}) {
  return (
    <div className="flex shrink-0 items-stretch gap-2">
      {/* The wait, drawn where it happens — a dotted arm rather than a solid one, because nothing
          carries the work across it on its own. */}
      {waiting ? (
        <div className="flex shrink-0 items-center gap-1">
          <div aria-hidden="true" className="h-0 w-4 border-t-2 border-dashed border-warning" />
          <span className="flex items-center gap-1 text-[11px] font-semibold text-warning">
            <UserRound className="size-3 shrink-0" aria-hidden="true" />
            {t("preview.person")}
          </span>
          <div aria-hidden="true" className="h-0 w-4 border-t-2 border-dashed border-warning" />
        </div>
      ) : null}

      <div
        className={cn(
          "flex w-40 shrink-0 flex-col gap-1 rounded-md border px-2.5 py-2",
          "border-border bg-muted/40",
        )}
      >
        <span className="flex items-center gap-1.5">
          <span className="truncate font-mono text-[11px] font-semibold text-primary">{label}</span>
          {gated ? <GateChip hint={t("preview.gate")} /> : null}
        </span>
        <span className="text-[10px] leading-snug text-muted-foreground">{hint}</span>
      </div>
    </div>
  );
}
