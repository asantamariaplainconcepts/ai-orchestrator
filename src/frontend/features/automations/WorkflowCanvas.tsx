import { GripVertical, UserRound, UserRoundPlus } from "lucide-react";
import { useState } from "react";
import { t, tCount } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { NativeSelect } from "@/shared/ui/native-select";
import { EXECUTABLE_ACTIONS } from "./types";
import type { Automation, CreateAutomationRequest } from "./types";
import { useSetAutomationEnabled, useUpdateAutomation } from "./useAutomations";
import { summarise, workflowChains } from "./workflowGraph";

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
      className="hidden items-center gap-2 self-start rounded-md border border-dashed border-warning px-3 py-2 text-xs text-warning xl:flex"
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
}: {
  projectId: string;
  automations: Automation[];
}) {
  const update = useUpdateAutomation(projectId);
  const setEnabled = useSetAutomationEnabled(projectId);
  // Only what is a workflow (#136, design D2): a chain of one is an Automation with no edge, which
  // belongs to the catalogue and not here. That single filter is what removed #122's special case.
  const chains = workflowChains(automations);
  const summary = summarise(chains);
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
        rubricPath: automation.rubricPath ?? null,
        outputLabel: automation.outputLabel ?? null,
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
  function placeBlock(preceding: Automation, movedFrom: string | null) {
    change(preceding, { outputLabel: null });

    if (!movedFrom || movedFrom === preceding.id) {
      return;
    }

    const source = automations.find((candidate) => candidate.id === movedFrom);
    const destination = reconnectionFor(movedFrom);
    if (source && destination) {
      change(source, { outputLabel: destination });
    }
  }

  /**
   * Where the step at an open gap should hand work to, when that is derivable: the root of the chain
   * drawn immediately after the one it ends. Undefined when nothing follows, which the caller reads
   * as "leave the gap open and let the Admin name a destination".
   */
  function reconnectionFor(endingAutomationId: string): string | undefined {
    const index = chains.findIndex(
      (chain) => chain[chain.length - 1]?.automation.id === endingAutomationId,
    );
    return index >= 0 ? chains[index + 1]?.[0]?.automation.triggerLabel : undefined;
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

      {/* Steps and human stops, both derived (design D4): "6 Automations" is a fact about the
          catalogue and says nothing about the pipeline. */}
      <p className="text-xs text-muted-foreground">
        {tCount(
          summary.steps,
          "automations.workflow.steps.one",
          "automations.workflow.steps.other",
        )}
        {" \u00b7 "}
        {tCount(
          summary.humanStops,
          "automations.workflow.stops.one",
          "automations.workflow.stops.other",
        )}
      </p>

      <p className="text-xs text-muted-foreground">{t("canvas.hint")}</p>

      {/* A chain reads left to right, like the board's columns, and each step is separated from
          the next by a vertical rule — solid where work flows on its own, dotted where a person
          carries it. Chains stack, and a long one scrolls sideways exactly as the board does. */}
      <div className="flex flex-col gap-6">
        {chains.map((chain) => (
          <div
            key={chain[0]?.automation.id}
            // Inside its own container, deliberately: a row that lets the page scroll sideways
            // breaks every other screen on a phone. flex-nowrap so a long chain scrolls rather
            // than folding into a grid whose rows mean nothing (design D3).
            className="flex flex-col items-stretch gap-0 xl:flex-row xl:flex-nowrap xl:overflow-x-auto xl:pb-2"
            onDragOver={(event) => {
              // Only our own block: any other drag passing over the flow is none of its business.
              if (event.dataTransfer.types.includes(HUMAN_BLOCK)) setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={() => setDragging(false)}
          >
            {chain.map((node) => (
              <div key={node.automation.id} className="flex shrink-0 items-stretch">
                <AutomationNode
                  automation={node.automation}
                  onToggleApproval={() =>
                    change(node.automation, {
                      requiresApproval: !node.automation.requiresApproval,
                    })
                  }
                  onToggleEnabled={() =>
                    setEnabled.mutate({
                      id: node.automation.id,
                      enabled: !node.automation.enabled,
                    })
                  }
                />
                <Connector
                  automation={node.automation}
                  connected={node.next !== null}
                  dragging={dragging}
                  onDropBlock={(movedFrom) => {
                    setDragging(false);
                    placeBlock(node.automation, movedFrom);
                  }}
                  candidates={automations.filter(
                    (candidate) =>
                      candidate.id !== node.automation.id &&
                      candidate.triggerLabel !== node.automation.triggerLabel,
                  )}
                  onConnect={(triggerLabel) =>
                    change(node.automation, { outputLabel: triggerLabel })
                  }
                  // The button and the drop are one path with two callers, the discipline
                  // RunCreator and HandOn already apply. It matters more than usual here:
                  // Playwright cannot perform an HTML5 drag (#110 recorded this), so routing the
                  // explicit control through the same function is what puts this logic under test
                  // at all.
                  onDisconnect={() => placeBlock(node.automation, null)}
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
  onToggleApproval,
  onToggleEnabled,
}: {
  automation: Automation;
  onToggleApproval: () => void;
  onToggleEnabled: () => void;
}) {
  return (
    <Card className={cn("w-60", !automation.enabled && "opacity-60")}>
      <CardContent className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-1.5">
          <Badge variant="secondary">{automation.triggerLabel}</Badge>
          {automation.triggerState ? (
            <Badge variant="outline">{automation.triggerState}</Badge>
          ) : null}
        </div>

        <span className="truncate text-sm font-medium">{automation.action}</span>
        <span className="truncate text-xs text-muted-foreground">
          {automation.runtime}
          {EXECUTABLE_ACTIONS.includes(automation.action)
            ? ""
            : ` · ${t("automations.actionNotExecutable")}`}
        </span>

        {/* The balloon, on a node: approval. Same word and same colour as the one on an edge,
            because to the reader they mean one thing — a person is required here. */}
        <Button
          variant={automation.requiresApproval ? "default" : "outline"}
          size="sm"
          type="button"
          onClick={onToggleApproval}
          className={cn(automation.requiresApproval && "bg-warning text-warning-foreground")}
        >
          <UserRound className="size-3.5" />
          {automation.requiresApproval ? t("canvas.approval.on") : t("canvas.approval.off")}
        </Button>

        <Button variant="ghost" size="sm" type="button" onClick={onToggleEnabled}>
          {automation.enabled ? t("automations.disable") : t("automations.enable")}
        </Button>
      </CardContent>
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
  // An output label pointing at no Automation: the vendor will carry the label and nobody will
  // answer it. Said plainly rather than drawn as a chain that does not exist.
  const dangling = !connected && Boolean(automation.outputLabel);

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
        "flex w-full shrink-0 flex-col items-center gap-2 self-stretch py-2",
        connected ? "px-2 xl:w-16" : "px-3 xl:w-48",
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
          {dangling ? (
            <span className="text-center text-xs text-destructive">
              {t("canvas.dangling")} <span className="font-mono">{automation.outputLabel}</span>
            </span>
          ) : null}
          <NativeSelect
            className="h-8 text-xs"
            aria-label={t("canvas.handsTo")}
            value=""
            onChange={(event) => {
              if (event.target.value) onConnect(event.target.value);
            }}
          >
            <option value="">{t("canvas.handsTo")}</option>
            {candidates.map((candidate) => (
              <option key={candidate.id} value={candidate.triggerLabel}>
                {candidate.triggerLabel}
              </option>
            ))}
          </NativeSelect>
        </div>
      )}

      <div aria-hidden="true" className={rule} />
    </div>
  );
}
