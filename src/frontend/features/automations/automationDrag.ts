import type { Automation } from "./types";
import { AUTOMATION_BLOCK } from "./chainDrag";

/**
 * What a catalogue row needs to become draggable into the chain (turn 8, option 8a). Defined here
 * beside the slots that accept it, so the two halves of one gesture cannot drift apart.
 */
export function automationDragProps(automation: Automation, onCarry: Carry) {
  return {
    draggable: true,
    onDragStart: (event: React.DragEvent) => {
      event.dataTransfer.setData(AUTOMATION_BLOCK, automation.id);
      event.dataTransfer.effectAllowed = "move";
      // Announced through React rather than read back from the drag: `dataTransfer.getData` is
      // deliberately empty during `dragover` for security, so a slot cannot ask what is over it.
      // Without this the slot could only say "something", and saying which two labels a drop
      // rewrites is the entire point of the gesture.
      onCarry(automation);
    },
    onDragEnd: () => onCarry(null),
  };
}

/** Told what is in flight, so every slot can describe its own drop before it happens. */
export type Carry = (automation: Automation | null) => void;
