import { GripVertical, UserRound } from "lucide-react";
import { t } from "@/shared/i18n";

/**
 * What a drag carries (#137). "new" is a block coming out of the catalogue; anything else is the id
 * of the step whose output label is already cleared — the gap the block is being moved *from*.
 */
export const HUMAN_BLOCK = "application/x-aio-human-step";

/**
 * The block an Admin drags into a gap (#137, design D1). Lives in the catalogue and is defined here
 * so the meaning of the gesture stays in one file with the gaps that accept it.
 *
 * Hidden below the width at which the flow reads left to right: a drag on a phone competes with the
 * gesture that scrolls, and losing that fight silently is worse than not offering it (design D5).
 */
export function HumanStepBlock() {
  return (
    <div
      draggable
      onDragStart={(event) => {
        event.dataTransfer.setData(HUMAN_BLOCK, "new");
        event.dataTransfer.effectAllowed = "move";
      }}
      className="flex items-center gap-2 self-start rounded-md border border-dashed border-warning px-3 py-2 text-xs text-warning"
    >
      <GripVertical className="size-3.5 shrink-0" aria-hidden="true" />
      <UserRound className="size-3.5 shrink-0" aria-hidden="true" />
      {t("canvas.block")}
    </div>
  );
}
