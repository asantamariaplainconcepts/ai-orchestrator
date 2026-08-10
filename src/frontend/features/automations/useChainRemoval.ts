import type { Automation } from "./types";
import { useUpdateAutomation } from "./useAutomations";
import { requestFor } from "./automationRequest";
import { AUTOMATION_BLOCK, removalRewrites } from "./chainDrag";

/**
 * The rail's half of the gesture: dropping a step here takes it out of the chain, which clears
 * whichever label pointed at it (8a). Nothing is put in its place — an absence has no two ends.
 */
export function useChainRemoval(
  projectId: string,
  automations: Automation[],
): { onDragOver: (event: React.DragEvent) => void; onDrop: (event: React.DragEvent) => void } {
  const update = useUpdateAutomation(projectId);

  return {
    onDragOver: (event) => {
      if (event.dataTransfer.types.includes(AUTOMATION_BLOCK)) event.preventDefault();
    },
    onDrop: (event) => {
      const id = event.dataTransfer.getData(AUTOMATION_BLOCK);
      const dragged = automations.find((candidate) => candidate.id === id);
      if (!dragged) return;
      event.preventDefault();

      for (const rewrite of removalRewrites(dragged, automations)) {
        update.mutate({
          id: rewrite.automation.id,
          request: requestFor(rewrite.automation, { outputLabels: rewrite.outputLabels }),
        });
      }
    },
  };
}
