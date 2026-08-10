import { GripVertical, UserRound } from "lucide-react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { GateChip } from "@/shared/ui/gate-chip";
import { EXECUTABLE_ACTIONS } from "./types";
import type { Automation } from "./types";
import { automationDragProps } from "./automationDrag";
import type { Carry } from "./automationDrag";

export function AutomationNode({
  automation,
  connected,
  onEdit,
  onToggleApproval,
  onCarry,
}: {
  automation: Automation;
  /** Whether an edge leaves this step. Only the graph knows; the node cannot infer it. */
  connected: boolean;
  onEdit: () => void;
  onToggleApproval: () => void;
  /** Picked up by its handle, to reorder it or take it out of the chain (8c). */
  onCarry: Carry;
}) {
  // An output label pointing at no Automation, announced on the step that owns it (#232). It used
  // to be said at the connector below, which is where the reader is looking at a *gap* — the label
  // belongs to this node, and naming it here is what makes it fixable. Same condition as before,
  // moved: not connected, yet carrying labels the vendor will apply and nobody will answer.
  const dangling = !connected && automation.outputLabels.length > 0;

  return (
    // One line per step (design review 6a). The node used to be a two-zone card — a header row plus
    // a body repeating the action and the runtime — which made a three-step flow taller than the
    // screen and buried the shape the canvas exists to show. What a step needs at a glance is what
    // fires it, whether a person gates it, and which prompt it runs; the rest is one click away in
    // the edit panel, where it can be changed rather than only read.
    <Card
      className={cn(
        "w-full gap-0 py-0",
        !automation.enabled && "opacity-60",
        dangling && "border-destructive/50",
      )}
    >
      <div className="flex min-h-11 flex-wrap items-center justify-between gap-x-2 gap-y-1 px-3 py-2">
        <span className="flex min-w-0 items-center gap-2">
          {/* The handle, not the whole card: a card that is entirely draggable cannot be
              selected, and the text inside it stops being text (8c). */}
          <span
            {...automationDragProps(automation, onCarry)}
            aria-label={`${t("canvas.reorder")} ${automation.triggerLabel}`}
            title={t("canvas.reorder")}
            className="shrink-0 cursor-grab text-muted-foreground active:cursor-grabbing"
          >
            <GripVertical className="size-3.5" aria-hidden="true" />
          </span>
          <span className="truncate font-mono text-xs font-semibold text-primary">
            {automation.triggerLabel}
          </span>
          {/* The board's chip, not one that looks like it (#232). Before the approval toggle in DOM
              order, so "what is true" reads ahead of "what you can change". */}
          {automation.requiresApproval ? <GateChip hint={t("canvas.approval.on")} /> : null}
          {automation.promptPath ? (
            <span className="truncate font-mono text-[11px] text-muted-foreground">
              {automation.promptPath}
            </span>
          ) : null}
          {automation.enabled ? null : (
            <span className="shrink-0 text-[10px] font-semibold text-muted-foreground">
              {t("automations.disabled")}
            </span>
          )}
          {/* Dormant today — every catalogue action executes — and kept because the list exists so a
              future action can be offered before it runs, which is precisely when a step drawn in a
              flow must not look like it will fire. */}
          {EXECUTABLE_ACTIONS.includes(automation.action) ? null : (
            <span className="shrink-0 text-[10px] font-semibold text-warning">
              {t("automations.actionNotExecutable")}
            </span>
          )}
        </span>
        <span className="flex shrink-0 items-center gap-1">
          <span className="text-[10px] text-muted-foreground">
            {automation.triggerState ?? t("automations.anyState")}
          </span>
          {/* The one gesture that belongs to the picture: gating a step is a property of the flow,
              so it stays on the node. Everything else about the Automation is edited in the panel —
              ADR-0006 asks that a capability be reachable, and one click is reachable. */}
          <Button
            variant="ghost"
            size="icon-sm"
            type="button"
            onClick={onToggleApproval}
            aria-label={
              automation.requiresApproval ? t("canvas.approval.on") : t("canvas.approval.off")
            }
            title={automation.requiresApproval ? t("canvas.approval.on") : t("canvas.approval.off")}
            className={cn(automation.requiresApproval && "text-warning")}
          >
            <UserRound className="size-3.5" aria-hidden="true" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            type="button"
            className="text-xs text-primary"
            onClick={onEdit}
          >
            {t("automations.edit")}
          </Button>
        </span>
        {/* An output label pointing at nothing is a defect in the configuration, so it stays on the
            node rather than moving into the panel: it has to be visible without opening anything. */}
        {dangling ? (
          <span className="w-full text-xs text-destructive">
            {t("canvas.dangling")}{" "}
            <span className="font-mono">{automation.outputLabels.join(", ")}</span>
          </span>
        ) : null}
      </div>
    </Card>
  );
}
