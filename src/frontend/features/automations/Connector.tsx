import { UserRound, UserRoundPlus } from "lucide-react";
import { useState } from "react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Button } from "@/shared/ui/button";
import { NativeSelect } from "@/shared/ui/native-select";
import type { Automation } from "./types";
import { AUTOMATION_BLOCK } from "./chainDrag";
import type { DropRefusal } from "./chainDrag";
import { HUMAN_BLOCK } from "./HumanStepBlock";
import { DropSlot } from "./DropSlot";

/**
 * What happens after this Automation succeeds: a solid arm to the next one, or a dotted arm
 * ending in the human balloon. The control names the choice, so the picture is never the only
 * way to read it.
 */
export function Connector({
  automation,
  connected,
  candidates,
  dragging,
  onConnect,
  onDisconnect,
  onDropBlock,
  following,
  carried,
  refusal,
  onDropAutomation,
  automations,
}: {
  automation: Automation;
  connected: boolean;
  candidates: Automation[];
  /** True while a human block is being dragged anywhere over the flow (#137). */
  dragging: boolean;
  onConnect: (triggerLabel: string) => void;
  onDisconnect: () => void;
  /** The gap the block came from, or null for one out of the catalogue. */
  onDropBlock: (movedFrom: string | null) => void;
  /** The step after this slot, or null when the slot is the end of the chain (8a). */
  following: Automation | null;
  /** The Automation in flight, so this slot can say what its drop would wire before it lands. */
  carried: Automation | null;
  /** Why this slot cannot take the carried step, or null when it can (8c). */
  refusal: DropRefusal | null;
  onDropAutomation: (dragged: Automation) => void;
  automations: Automation[];
}) {
  // The dangling warning moved to the node that owns the label (#232) — it was announced here, at
  // the gap, which is not where the label lives or where it gets fixed.

  // Local and deliberately not lifted: which gap is being connected is this connector's own
  // business, and nothing above it needs to know.
  const [choosing, setChoosing] = useState(false);

  const rule = cn(
    "w-0 flex-1 border-l-2",
    connected ? "border-solid border-primary" : "border-dashed border-warning",
  );

  return (
    // Two states, two widths, because they need different things. A connected step needs one
    // control and appears between every pair, so its width is paid once per step and comes
    // straight out of how much of the chain fits on screen (#136's density criterion) — it gets
    // an icon and stays thin. An open gap has to offer a choice of destinations, and it appears
    // at most where a person is required, so it can afford the room a select needs.
    // Full width when the flow stacks vertically, where there is no chain to compete with.
    <div
      // A connected gap can accept the block; an open one already has its review, so it never
      // calls preventDefault and the cursor says no-drop. Marked while a drag is in flight so a
      // valid target is visible before the pointer reaches it, not learned by failing.
      onDragOver={(event) => {
        if (connected && event.dataTransfer.types.includes(HUMAN_BLOCK)) event.preventDefault();
        // A refused slot never calls preventDefault, so the cursor says no-drop before the drop
        // is attempted — the refusal below says why, at the slot rather than in a toast after.
        if (event.dataTransfer.types.includes(AUTOMATION_BLOCK) && carried && !refusal) {
          event.preventDefault();
        }
      }}
      onDrop={(event) => {
        const chained = event.dataTransfer.getData(AUTOMATION_BLOCK);
        if (chained) {
          if (refusal) return;
          const dragged = automations.find((candidate) => candidate.id === chained);
          if (!dragged) return;
          event.preventDefault();
          onDropAutomation(dragged);
          return;
        }

        if (!connected) return;
        event.preventDefault();
        const payload = event.dataTransfer.getData(HUMAN_BLOCK);
        onDropBlock(payload === "new" ? null : payload);
      }}
      className={cn(
        "flex w-full min-w-0 flex-col items-center gap-2 self-stretch py-2",
        connected ? "px-2" : "px-3",
        dragging &&
          connected &&
          "rounded-md bg-warning/10 outline-2 outline-dashed outline-warning",
        carried && !refusal && "rounded-md bg-primary/10 outline-2 outline-dashed outline-primary",
        carried &&
          refusal &&
          "rounded-md bg-destructive/10 outline-2 outline-dashed outline-destructive",
      )}
    >
      {/* The rule stands between one step and the next, broken in the middle by what it means:
          a way to require a person, or the person who is already required. */}
      <div aria-hidden="true" className={rule} />

      {/* What this drop would wire, spelled out before it happens (8a) — or the rule that stops
          it, quoted where the pointer is rather than after the gesture (8c). */}
      {carried ? (
        <DropSlot
          preceding={automation}
          following={following}
          dragged={carried}
          refusal={refusal}
        />
      ) : null}

      {connected ? (
        // The sentence becomes the accessible name rather than a line of wrapped text: one
        // action, and the icon carries it. Still an explicit control — the spec asks that no
        // gesture be drag-only, not that every control spell itself out in prose.
        <Button
          variant="ghost"
          size="sm"
          type="button"
          onClick={onDisconnect}
          aria-label={t("canvas.disconnect")}
          title={t("canvas.disconnect")}
        >
          <UserRoundPlus className="size-4" aria-hidden="true" />
        </Button>
      ) : (
        <div className="flex w-full flex-col items-center gap-1.5">
          <span
            draggable
            onDragStart={(event) => {
              // Carries the id of the step whose label is cleared — the gap being moved from.
              event.dataTransfer.setData(HUMAN_BLOCK, automation.id);
              event.dataTransfer.effectAllowed = "move";
            }}
            className="flex cursor-grab items-center gap-1 text-center text-xs font-medium text-warning"
          >
            <UserRound className="size-3.5 shrink-0" aria-hidden="true" />
            {t("canvas.human")}
          </span>
          {/* Revealed, not permanent (#232): a select at every open gap is a control offered to
              somebody who is not connecting anything, and the flow reads as a form. Still one
              click away, because ADR-0006 asks that the capability be reachable — not that it be
              on screen at all times. */}
          {choosing ? (
            <NativeSelect
              autoFocus
              className="h-8 text-xs"
              aria-label={t("canvas.handsTo")}
              value=""
              onChange={(event) => {
                if (event.target.value) onConnect(event.target.value);
              }}
              onBlur={() => setChoosing(false)}
            >
              <option value="">{t("canvas.handsTo")}</option>
              {candidates.map((candidate) => (
                <option key={candidate.id} value={candidate.triggerLabel}>
                  {candidate.triggerLabel}
                </option>
              ))}
            </NativeSelect>
          ) : (
            <Button
              variant="ghost"
              size="sm"
              type="button"
              className="h-7 text-xs"
              onClick={() => setChoosing(true)}
            >
              {t("canvas.handsTo")}
            </Button>
          )}
        </div>
      )}

      <div aria-hidden="true" className={rule} />
    </div>
  );
}
