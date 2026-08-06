import { CornerDownRight, GripVertical, UserRound, UserRoundPlus } from "lucide-react";
import { useState } from "react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { GateChip } from "@/shared/ui/gate-chip";
import { NativeSelect } from "@/shared/ui/native-select";
import { EXECUTABLE_ACTIONS } from "./types";
import type { Automation, CreateAutomationRequest } from "./types";
import { useUpdateAutomation } from "./useAutomations";
import { hasBranches, workflowChains } from "./workflowGraph";

/**
 * What a drag carries (#137). "new" is a block coming out of the catalogue; anything else is the id
 * of the step whose output label is already cleared — the gap the block is being moved *from*.
 */
const HUMAN_BLOCK = "application/x-aio-human-step";

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
}: {
  projectId: string;
  automations: Automation[];
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

  /**
   * Every canvas change is an ordinary Automation update (design D4), so BR-003's overlap check
   * and #115's self-trigger refusal apply unchanged. The whole Automation is resent because the
   * endpoint replaces it — including the fields this screen never shows, which is why the API
   * had to start returning them.
   */
  function change(automation: Automation, patch: Partial<CreateAutomationRequest>) {
    update.mutate({
      id: automation.id,
      request: {
        triggerLabel: automation.triggerLabel,
        triggerState: automation.triggerState,
        action: automation.action,
        runtime: automation.runtime,
        requiresApproval: automation.requiresApproval,
        timeoutMinutes: automation.timeoutMinutes,
        promptPath: automation.promptPath ?? null,
        outputLabels: automation.outputLabels,
        ...patch,
      },
    });
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
              // Only our own block: any other drag passing over the flow is none of its business.
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
    </div>
  );
}

function AutomationNode({
  automation,
  connected,
  onEdit,
  onToggleApproval,
}: {
  automation: Automation;
  /** Whether an edge leaves this step. Only the graph knows; the node cannot infer it. */
  connected: boolean;
  onEdit: () => void;
  onToggleApproval: () => void;
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

/**
 * What happens after this Automation succeeds: a solid arm to the next one, or a dotted arm
 * ending in the human balloon. The control names the choice, so the picture is never the only
 * way to read it.
 */
function Connector({
  automation,
  connected,
  candidates,
  dragging,
  onConnect,
  onDisconnect,
  onDropBlock,
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
      }}
      onDrop={(event) => {
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
      )}
    >
      {/* The rule stands between one step and the next, broken in the middle by what it means:
          a way to require a person, or the person who is already required. */}
      <div aria-hidden="true" className={rule} />

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
