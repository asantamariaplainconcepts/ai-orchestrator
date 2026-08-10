import { CornerDownRight } from "lucide-react";
import { useState } from "react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import type { Automation, CreateAutomationRequest } from "./types";
import { useUpdateAutomation } from "./useAutomations";
import { hasBranches, workflowChains } from "./workflowGraph";
import { BoardPreview } from "./BoardPreview";
import { AutomationNode } from "./AutomationNode";
import { Connector } from "./Connector";
import { HUMAN_BLOCK } from "./HumanStepBlock";
import type { Carry } from "./automationDrag";
import { requestFor } from "./automationRequest";
import { type ChainDrop, refusalFor, rewritesFor } from "./chainDrag";

/**
 * The pipeline as a shape (#116). Edges are label agreements — nothing about the picture is
 * stored, so the canvas cannot claim a chain that would not fire (design D1).
 * <p>
 * The human balloon hangs off a node's <b>output</b> rather than between two nodes, because an
 * absence has no two ends: "no output label" does not name a destination, so the gesture that
 * removes it must also choose one. On a node the same balloon is `requiresApproval` (design D2).
 * Dragging is sugar; every gesture here is an explicit control (design D3).
 * </p>
 */
export function WorkflowCanvas({
  projectId,
  automations,
  onEdit,
  carried,
  onCarry,
}: {
  projectId: string;
  automations: Automation[];
  /**
   * The Automation being dragged, from wherever the drag began — the rail or a step's own handle.
   * Held above this component because both surfaces start the gesture and only one of them is
   * inside the canvas (turn 8).
   */
  carried: Automation | null;
  onCarry: Carry;
  /**
   * Opens the edit panel on a step (design review 6b). The canvas raises the intent and never owns
   * the form: the same panel answers a click here and a click in the rail, which is what keeps the
   * two surfaces from growing two different edit experiences.
   */
  onEdit: (automation: Automation) => void;
}) {
  const update = useUpdateAutomation(projectId);
  // Only what is a workflow (#136, design D2): a chain of one is an Automation with no edge, which
  // belongs to the catalogue and not here. That single filter is what removed #122's special case.
  const chains = workflowChains(automations);
  // Whether any step hands on to more than one place — what the BR-001 note is for.
  const branching = hasBranches(chains);
  const [dragging, setDragging] = useState(false);
  // The step a drop just added, so the board preview can show the consequence where it landed.
  const [justChained, setJustChained] = useState<string | null>(null);

  /**
   * Every canvas change is an ordinary Automation update (design D4), so BR-003's overlap check
   * and #115's self-trigger refusal apply unchanged. The whole Automation is resent because the
   * endpoint replaces it — including the fields this screen never shows, which is why the API
   * had to start returning them.
   */
  function change(automation: Automation, patch: Partial<CreateAutomationRequest>) {
    update.mutate({ id: automation.id, request: requestFor(automation, patch) });
  }

  // A project whose Automations all stand alone has a catalogue and no flow. That is a state, not
  // an error and not a blank area, and it says what would make a flow exist.
  /**
   * A block dropped into the gap after `preceding` (#137, design D1): clear that step's output
   * label, so the chain stops and a person reviews what it produced. Never `requiresApproval` —
   * that is the other wait and it belongs to the card.
   *
   * A move breaks the new gap **before** reconnecting the old (design D3), so an interruption leaves
   * a review in both places and never in neither.
   *
   * The reconnect is attempted only when the destination is knowable. Once an output label is
   * cleared, nothing records what used to follow — design D2's "an absence has no two ends" — so a
   * move out of such a gap leaves it open, with its existing select as the way to close it. That is
   * the fail-safe direction anyway: an extra review costs a click, a missing one lets work through.
   */
  function placeBlock(preceding: Automation, movedFrom: string | null, edge?: string) {
    // One edge, not the field (#165): a step that hands on to three places must not lose the other
    // two because a person was placed on one of them. Without a named edge — the row's own
    // hand-off — the first label is the one this gap represents.
    const removed = edge ?? preceding.outputLabels[0];
    change(preceding, {
      outputLabels: preceding.outputLabels.filter((label) => label !== removed),
    });

    if (!movedFrom || movedFrom === preceding.id) {
      return;
    }

    const source = automations.find((candidate) => candidate.id === movedFrom);
    const destination = reconnectionFor(movedFrom);
    if (source && destination && !source.outputLabels.includes(destination)) {
      change(source, { outputLabels: [...source.outputLabels, destination] });
    }
  }

  /**
   * A step dropped into the slot after `preceding` (8a). Two label rewrites where it lands between
   * two steps, one where it lands on the end — and nothing else, because the graph is derived
   * (design D1) and every gesture is an ordinary update (design D4).
   */
  function chainInto(drop: ChainDrop) {
    for (const rewrite of rewritesFor(drop)) {
      change(rewrite.automation, { outputLabels: rewrite.outputLabels });
    }
    setJustChained(drop.dragged.id);
  }

  /**
   * Where the step at an open gap should hand work to, when that is derivable: the root of the chain
   * drawn immediately after the one it ends. Undefined when nothing follows, which the caller reads
   * as "leave the gap open and let the Admin name a destination".
   */
  function reconnectionFor(endingAutomationId: string): string | undefined {
    const index = chains.findIndex(
      (chain) => chain.nodes[chain.nodes.length - 1]?.automation.id === endingAutomationId,
    );
    return index >= 0 ? chains[index + 1]?.nodes[0]?.automation.triggerLabel : undefined;
  }

  if (chains.length === 0) {
    return <p className="text-sm text-muted-foreground">{t("automations.workflow.empty")}</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      {update.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("canvas.changeRefused")}
        </p>
      )}

      <p className="text-xs text-muted-foreground">{t("canvas.hint")}</p>

      {/* The ceiling, stated where the edges are explained (#165, design D3). A picture of two
          branches cannot say this by itself, and left unsaid it teaches its reader that both run:
          BR-001 allows one active Run per Story, so the second simultaneous match is ignored
          rather than queued. */}
      {branching ? <p className="text-xs text-warning">{t("canvas.branchesSerialize")}</p> : null}

      {/* A chain reads left to right, like the board's columns, and each step is separated from
          the next by a vertical rule — solid where work flows on its own, dotted where a person
          carries it. Chains stack, and a long one scrolls sideways exactly as the board does. */}
      <div className="flex flex-col gap-6">
        {chains.map((chain) => (
          <div
            key={chain.nodes[0]?.automation.id}
            // One layout at every width (#232). It used to flip horizontal at xl and scroll
            // sideways, which meant two interaction models and a codepath forked throughout — and
            // below xl the drag was hidden entirely, so a phone could not reorder a pipeline at
            // all. A chain reads top-down, which is the direction it actually flows.
            className={cn(
              "flex max-w-[520px] flex-col items-stretch gap-0",
              // A branch indents under the step it leaves, so the picture says which it came from
              // as well as the chip does.
              chain.branchedFrom && "pl-8",
            )}
            onDragOver={(event) => {
              // Only our own blocks: any other drag passing over the flow is none of its business.
              if (event.dataTransfer.types.includes(HUMAN_BLOCK)) setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={() => setDragging(false)}
          >
            {/* A branch row exists because an edge points into it, so it opens by naming where
                that edge came from — otherwise the second hand-off would read as an unrelated
                chain that happens to sit below. */}
            {chain.branchedFrom ? (
              <div
                // Named, not just drawn: the chip's own accessible name is "from <trigger>", which
                // is what makes "this row is a branch of that step" assertable — and readable by
                // somebody who cannot see the layout that would otherwise carry the meaning.
                aria-label={`${t("canvas.branchFrom")} ${chain.branchedFrom.triggerLabel}`}
                className="flex shrink-0 items-center pr-2 text-xs text-muted-foreground"
              >
                <CornerDownRight className="mr-1 size-3.5 shrink-0" aria-hidden="true" />
                {t("canvas.branchFrom")}{" "}
                <span className="ml-1 font-mono">{chain.branchedFrom.triggerLabel}</span>
              </div>
            ) : null}
            {chain.nodes.map((node) => (
              // A column, not a row (#238). The step and the connector that follows it are stacked
              // for the same reason the chain is: in the horizontal layout the connector sat to the
              // right of its step, and #232 turned the *outer* container vertical while leaving
              // this one a row — so the rail rendered in a lane beside the steps instead of between
              // them. min-w-0 so a card still shrinks below its content rather than forcing a
              // sideways scroll.
              <div key={node.automation.id} className="flex min-w-0 flex-col items-stretch">
                <AutomationNode
                  automation={node.automation}
                  connected={node.next !== null}
                  onCarry={onCarry}
                  onEdit={() => onEdit(node.automation)}
                  onToggleApproval={() =>
                    change(node.automation, {
                      requiresApproval: !node.automation.requiresApproval,
                    })
                  }
                />
                <Connector
                  automation={node.automation}
                  connected={node.next !== null}
                  dragging={dragging}
                  following={node.next}
                  carried={carried}
                  refusal={
                    carried
                      ? refusalFor(
                          { preceding: node.automation, following: node.next, dragged: carried },
                          automations,
                        )
                      : null
                  }
                  onDropAutomation={(dragged) => {
                    onCarry(null);
                    chainInto({
                      preceding: node.automation,
                      following: node.next,
                      dragged,
                    });
                  }}
                  automations={automations}
                  onDropBlock={(movedFrom) => {
                    setDragging(false);
                    placeBlock(node.automation, movedFrom, node.next?.triggerLabel);
                  }}
                  candidates={automations.filter(
                    (candidate) =>
                      candidate.id !== node.automation.id &&
                      candidate.triggerLabel !== node.automation.triggerLabel,
                  )}
                  onConnect={(triggerLabel) =>
                    change(node.automation, {
                      // Added, not assigned (#165): connecting a second destination is what a
                      // branch is, and replacing the field would silently delete the first.
                      outputLabels: node.automation.outputLabels.includes(triggerLabel)
                        ? node.automation.outputLabels
                        : [...node.automation.outputLabels, triggerLabel],
                    })
                  }
                  // The button and the drop are one path with two callers, the discipline
                  // RunCreator and HandOn already apply. It matters more than usual here:
                  // Playwright cannot perform an HTML5 drag (#110 recorded this), so routing the
                  // explicit control through the same function is what puts this logic under test
                  // at all.
                  onDisconnect={() => placeBlock(node.automation, null, node.next?.triggerLabel)}
                />
              </div>
            ))}
          </div>
        ))}
      </div>

      {/* The consequence of the gesture, where the gesture is (8b). Below the chain rather than in
          another tab, because wiring the workflow and seeing what it does to the board were two
          screens and the person wiring was never looking at the second. */}
      <BoardPreview chains={chains} highlight={justChained} />
    </div>
  );
}
