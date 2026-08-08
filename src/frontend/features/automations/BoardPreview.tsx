import { ChevronDown, ChevronRight, UserRound } from "lucide-react";
import { useState } from "react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { GateChip } from "@/shared/ui/gate-chip";
import type { WorkflowChain } from "./workflowGraph";

/**
 * What this workflow makes of the Backlog tab (design review turn 8, option 8b).
 *
 * It exists to close a mental loop the product had left open: wiring the workflow and seeing its
 * effect on the board were two different tabs, so the consequence of a gesture was somewhere the
 * person making it was not looking. The columns of the board **are** the workflow's triggers, so
 * this is the same derivation painted sideways — not a second model that could disagree with the
 * first.
 *
 * Read-only on purpose. The drag happens in the chain above; this reacts. A preview that could
 * also be edited would be a second place to wire the same thing, and the two would drift.
 */
export function BoardPreview({
  chains,
  /** The step a drop just added, highlighted so the consequence is visible where it landed. */
  highlight,
}: {
  chains: WorkflowChain[];
  highlight?: string | null;
}) {
  const [open, setOpen] = useState(true);

  // Every step, in reading order, deduplicated: a branch row re-enters the board at its own
  // column rather than opening a second one with the same name.
  const seen = new Set<string>();
  const columns = chains
    .flatMap((chain) => chain.nodes)
    .filter((node) => seen.size !== seen.add(node.automation.triggerLabel).size);

  if (columns.length === 0) {
    return null;
  }

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
        {t("canvas.preview.title")}
        <span className="font-normal">{t("canvas.preview.hint")}</span>
        <span className="sr-only">
          {open ? t("canvas.preview.hide") : t("canvas.preview.show")}
        </span>
      </button>

      {open ? (
        <div className="flex gap-2 overflow-x-auto pb-1">
          {/* Where Stories start. Named rather than implied: a board whose first column is a
              trigger would say work begins already labelled, which is not how any of this
              starts. */}
          <PreviewColumn
            label={t("canvas.preview.untouched")}
            hint={t("canvas.preview.untouchedHint")}
          />

          {columns.map((node) => (
            <PreviewColumn
              key={node.automation.id}
              label={node.automation.triggerLabel}
              hint={
                node.automation.requiresApproval
                  ? t("canvas.preview.gate")
                  : t("canvas.preview.noApproval")
              }
              gated={node.automation.requiresApproval}
              added={highlight === node.automation.id}
              // Where the flow stops, the board stops too, and a person carries it on. Same
              // vocabulary as the real board: tinted ground and an accent for the human stop.
              stops={node.next === null}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function PreviewColumn({
  label,
  hint,
  gated,
  added,
  stops,
}: {
  label: string;
  hint: string;
  gated?: boolean;
  added?: boolean;
  stops?: boolean;
}) {
  return (
    <div className="flex shrink-0 items-stretch gap-2">
      <div
        className={cn(
          "flex w-40 shrink-0 flex-col gap-1 rounded-md border px-2.5 py-2",
          added ? "border-primary bg-primary/10" : "border-border bg-muted/40",
        )}
      >
        <span className="flex items-center gap-1.5">
          <span className="truncate font-mono text-[11px] font-semibold text-primary">{label}</span>
          {gated ? <GateChip hint={t("canvas.preview.gate")} /> : null}
        </span>
        <span className="text-[10px] leading-snug text-muted-foreground">{hint}</span>
      </div>

      {/* The stop, drawn where it happens. A dotted arm rather than a solid one, because nothing
          carries the work across it on its own. */}
      {stops ? (
        <div className="flex shrink-0 items-center gap-2">
          <div aria-hidden="true" className="h-0 w-6 border-t-2 border-dashed border-warning" />
          <div className="flex w-28 shrink-0 flex-col gap-0.5 rounded-md border border-warning/50 bg-warning/10 px-2 py-1.5">
            <span className="flex items-center gap-1 text-[11px] font-semibold text-warning">
              <UserRound className="size-3 shrink-0" aria-hidden="true" />
              {t("canvas.preview.person")}
            </span>
            <span className="text-[10px] leading-snug text-muted-foreground">
              {t("canvas.preview.personHint")}
            </span>
          </div>
        </div>
      ) : null}
    </div>
  );
}
