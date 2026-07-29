import { useState } from "react";
import { UserRound, UserRoundPlus } from "lucide-react";
import { Link } from "react-router";
import { useRuns } from "@/features/runs/useRuns";
import type { RunView } from "@/features/runs/types";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { NativeSelect } from "@/shared/ui/native-select";
import { useUpdateAutomation } from "@/features/automations/useAutomations";
import { UNTOUCHED, useMoveStory } from "./useMoveStory";
import type { Automation } from "@/features/automations/types";
import type { StoryView } from "./types";

/** BR-001's list, client-side. The server remains the authority; this refuses the gesture. */
const ACTIVE_STATES: readonly RunView["state"][] = [
  "Queued",
  "Planning",
  "AwaitingApproval",
  "Executing",
  "AwaitingInput",
];

/**
 * The board reads an Automation and, since #128, writes one: placing a person between two steps
 * clears the preceding step's output label through the ordinary update, which resends the whole
 * record. So this is the Automation, not a subset of it.
 *
 * It was a hand-written subset of four fields while the board only read. Widening it rather than
 * threading a callback keeps one type for one thing — and the field that unlocked #128 was
 * `outputLabel`: without it the board could not see an edge, so it could not order by the flow.
 */
export type BoardAutomation = Automation;

/** A column: a step's trigger label, or a place where the flow waits for a person (#128). */
interface Pile {
  key: string;
  label: string;
  stories: StoryView[];
  /** The step this human column follows, when it is one. */
  after?: BoardAutomation;
}

/**
 * The pipeline made spatial (#110): columns are the project's enabled Automation trigger labels
 * (design D4), and moving a card IS UC-008's licensed label write — ordinary matching does the
 * rest, so there is no dispatch machinery here at all.
 * <p>
 * A view, not a second data source (design D5): same queries as the list, same mutation. That is
 * why a label applied at the vendor moves a card on the next poll with no board-specific code.
 * </p>
 */
export function KanbanBoard({
  projectId,
  stories,
  automations,
}: {
  projectId: string;
  stories: StoryView[];
  automations: BoardAutomation[];
}) {
  const runs = useRuns(projectId, null);
  const move = useMoveStory(projectId);
  // The board's placing gesture writes the same field the canvas's block does (#128, design D3), so
  // an Admin who puts a person between two steps on one screen finds the same arrangement on the
  // other. It goes through the ordinary Automation update, so BR-003 and #115's refusals apply.
  const updateAutomation = useUpdateAutomation(projectId);
  const [dragging, setDragging] = useState<{ story: string; from: string } | null>(null);
  const [refused, setRefused] = useState<string | null>(null);

  const enabled = automations.filter((automation) => automation.enabled);

  // Ordered by the flow, not by when somebody happened to create each Automation (#128, design D1).
  // Deduplicated: two Automations may share a trigger label, and that is one column, not two.
  const byTrigger = new Map(enabled.map((automation) => [automation.triggerLabel, automation]));
  const handsTo = (automation: BoardAutomation) =>
    automation.outputLabel ? byTrigger.get(automation.outputLabel) : undefined;
  const receives = new Set(
    enabled.map((automation) => handsTo(automation)?.id).filter(Boolean) as string[],
  );

  /** Roots first, then whatever each hands to — the same walk the canvas does. */
  const chains: BoardAutomation[][] = [];
  const placed = new Set<string>();
  for (const root of enabled.filter((automation) => !receives.has(automation.id))) {
    const chain: BoardAutomation[] = [];
    let current: BoardAutomation | undefined = root;
    while (current && !placed.has(current.id)) {
      placed.add(current.id);
      chain.push(current);
      current = handsTo(current);
    }
    chains.push(chain);
  }
  // A cycle has no root, so its members are still unplaced. Shown rather than dropped.
  for (const orphan of enabled) {
    if (!placed.has(orphan.id)) {
      placed.add(orphan.id);
      chains.push([orphan]);
    }
  }

  // Chains of one are Automations outside the workflow (DEC-053). They still get columns — a Story
  // can carry `ai:estimate` and has to be somewhere — but after the flow, because the board orders
  // the flow rather than deciding what exists.
  const flow = chains.filter((chain) => chain.length > 1);
  const loose = chains.filter((chain) => chain.length === 1);
  const ordered = [...flow.flat(), ...loose.flat()];
  const columns = [...new Set(ordered.map((automation) => automation.triggerLabel))];
  const gated = new Set(
    automations.filter((a) => a.enabled && a.requiresApproval).map((a) => a.triggerLabel),
  );

  // The latest Run per Story — the one whose state the card wears.
  const latestRun = new Map<string, RunView>();
  for (const run of runs.data ?? []) {
    const held = latestRun.get(run.vendorStoryId);
    if (!held || run.createdAt > held.createdAt) latestRun.set(run.vendorStoryId, run);
  }

  function columnOf(story: StoryView): string {
    return story.labels.find((label) => columns.includes(label)) ?? UNTOUCHED;
  }

  function attempt(story: StoryView, to: string) {
    const from = columnOf(story);
    if (from === to) return;

    // BR-001 refused before any write (design D3): letting the label land and the match decline
    // silently would leave the vendor labelled and this board lying.
    const run = latestRun.get(story.vendorId);
    if (to !== UNTOUCHED && run && ACTIVE_STATES.includes(run.state)) {
      setRefused(story.vendorId);
      return;
    }

    setRefused(null);
    move.mutate({ vendorStoryId: story.vendorId, from, to });
  }

  /**
   * A Story has finished at its step when its latest Run succeeded there (#128, design D2). Those
   * Stories are the ones a person is being waited on for: the step is done and the chain stops.
   */
  function finishedAt(story: StoryView, automation: BoardAutomation): boolean {
    const run = latestRun.get(story.vendorId);
    return run?.state === "Succeeded" && run.automationId === automation.id;
  }

  /**
   * The steps a human column follows: the end of a chain that hands work to nobody.
   *
   * Only inside the flow, deliberately. An Automation outside the workflow hands to nobody either,
   * but "a person carries the work onward" means nothing there — `ai:estimate` is a trigger somebody
   * applies, not a step whose output waits for a decision. A column after each of those would be
   * noise the reader has to learn to ignore.
   */
  const awaitsAPerson = flow
    .map((chain) => chain[chain.length - 1])
    .filter((last): last is BoardAutomation => last !== undefined && handsTo(last) === undefined);

  const piles: Pile[] = [
    {
      key: UNTOUCHED,
      label: t("board.untouched"),
      stories: stories.filter((story) => columnOf(story) === UNTOUCHED),
    },
    ...columns.flatMap((column) => {
      const automation = byTrigger.get(column);
      const waiting = automation && awaitsAPerson.includes(automation) ? automation : undefined;

      const step: Pile = {
        key: column,
        label: column,
        stories: stories.filter(
          (story) => columnOf(story) === column && !(waiting && finishedAt(story, waiting)),
        ),
      };

      // The wait given a place, immediately after the step whose output nobody has carried on.
      return waiting
        ? [
            step,
            {
              key: `human:${column}`,
              label: t("board.human"),
              stories: stories.filter(
                (story) => columnOf(story) === column && finishedAt(story, waiting),
              ),
              after: waiting,
            } satisfies Pile,
          ]
        : [step];
    }),
  ];

  return (
    <div className="flex flex-col gap-3">
      {move.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("board.moveFailed")}
        </p>
      )}

      <div className="flex snap-x gap-3 overflow-x-auto pb-2">
        {piles.map((pile) => (
          <section
            key={pile.key}
            aria-label={pile.label}
            className={cn(
              "flex w-64 shrink-0 snap-start flex-col gap-2 rounded-lg border p-2",
              // A place, not a marker: the human column is drawn as its own kind of column so a
              // reader can tell "work is here" from "work is waiting for me" without reading.
              pile.after ? "border-dashed border-warning bg-warning/5" : "bg-muted/40",
            )}
            onDragOver={(event) => {
              // A human column is not a label, so nothing can be dropped into it: a Story arrives
              // there by its step finishing, never by a gesture.
              if (!pile.after) event.preventDefault();
            }}
            onDrop={() => {
              if (pile.after) return;
              const story = stories.find((candidate) => candidate.vendorId === dragging?.story);
              if (story) attempt(story, pile.key);
              setDragging(null);
            }}
          >
            <header className="flex items-center justify-between gap-2 px-1">
              <span className="flex min-w-0 items-center gap-1.5">
                {pile.after ? (
                  <UserRound className="size-3.5 shrink-0 text-warning" aria-hidden="true" />
                ) : null}
                <span
                  className={cn("truncate text-sm font-semibold", pile.after && "text-warning")}
                  title={pile.after ? t("board.human.hint") : undefined}
                >
                  {pile.label}
                </span>
                {/* The other wait, and deliberately still a badge on the step's own column: a Run
                    awaiting approval has reached that step, so a column before it would claim the
                    work had not arrived (#128, design D2). */}
                {gated.has(pile.key) ? (
                  <Badge className="bg-info text-info-foreground" title={t("board.gated.hint")}>
                    {t("board.gated")}
                  </Badge>
                ) : null}
              </span>
              <span className="flex items-center gap-1.5">
                {/* Only on a step that currently hands work on: placing a person is breaking that
                    hand-off, and there is nothing to break where the chain already stops. */}
                {(() => {
                  const step = byTrigger.get(pile.key);
                  const next = step ? handsTo(step) : undefined;
                  return step && next ? (
                    <button
                      type="button"
                      className="rounded p-0.5 text-warning hover:bg-warning/10"
                      title={t("board.requirePerson")}
                      aria-label={t("board.requirePerson")}
                      disabled={updateAutomation.isPending}
                      onClick={() =>
                        updateAutomation.mutate({
                          id: step.id,
                          request: {
                            triggerLabel: step.triggerLabel,
                            triggerState: step.triggerState,
                            action: step.action,
                            runtime: step.runtime,
                            requiresApproval: step.requiresApproval,
                            timeoutMinutes: step.timeoutMinutes,
                            rubricPath: step.rubricPath ?? null,
                            outputLabel: null,
                          },
                        })
                      }
                    >
                      <UserRoundPlus className="size-3.5" aria-hidden="true" />
                    </button>
                  ) : null;
                })()}
                <span className="text-xs text-muted-foreground">{pile.stories.length}</span>
              </span>
            </header>

            {pile.stories.length === 0 ? (
              <p className="px-1 py-4 text-xs text-muted-foreground">
                {pile.after ? t("board.human.empty") : t("board.columnEmpty")}
              </p>
            ) : (
              pile.stories.map((story) => (
                <StoryCard
                  key={story.vendorId}
                  projectId={projectId}
                  story={story}
                  run={latestRun.get(story.vendorId)}
                  columns={piles.map((candidate) => ({
                    key: candidate.key,
                    label: candidate.label,
                  }))}
                  current={pile.key}
                  refused={refused === story.vendorId}
                  onDragStart={() => setDragging({ story: story.vendorId, from: pile.key })}
                  onMove={(to) => attempt(story, to)}
                />
              ))
            )}
          </section>
        ))}
      </div>
    </div>
  );
}

function StoryCard({
  projectId,
  story,
  run,
  columns,
  current,
  refused,
  onDragStart,
  onMove,
}: {
  projectId: string;
  story: StoryView;
  run: RunView | undefined;
  columns: { key: string; label: string }[];
  current: string;
  refused: boolean;
  onDragStart: () => void;
  onMove: (to: string) => void;
}) {
  return (
    <article
      draggable
      onDragStart={onDragStart}
      className={cn(
        "flex cursor-grab flex-col gap-2 rounded-md border bg-card p-2 active:cursor-grabbing",
        refused && "border-destructive",
      )}
    >
      <span className="flex items-center justify-between gap-2">
        <span className="font-mono text-xs text-muted-foreground">#{story.vendorId}</span>
        {run ? <RunBadge projectId={projectId} run={run} /> : null}
      </span>

      <Link
        className="text-sm font-medium transition-colors hover:text-primary"
        to={`/projects/${projectId}/stories/${story.vendorId}`}
      >
        {story.title}
      </Link>

      {/* BR-001, said rather than implied — the card explains why it would not move. */}
      {refused && (
        <p className="text-xs text-destructive" role="alert">
          {t("board.refusedActiveRun")}
        </p>
      )}

      {/* The move menu is the semantics; dragging is sugar (design D1). Present at every width,
          which is also what makes the board usable by keyboard. */}
      <NativeSelect
        className="h-8 text-xs"
        aria-label={t("board.moveTo")}
        value=""
        onChange={(event) => {
          if (event.target.value) onMove(event.target.value);
        }}
      >
        <option value="">{t("board.moveTo")}</option>
        {columns
          .filter((column) => column.key !== current)
          .map((column) => (
            <option key={column.key} value={column.key}>
              {column.label}
            </option>
          ))}
      </NativeSelect>
    </article>
  );
}

/** What the Story's latest Run is doing, and a way in when it is worth watching. */
function RunBadge({ projectId, run }: { projectId: string; run: RunView }) {
  const to = `/projects/${projectId}/runs/${run.id}`;

  if (run.state === "Executing" || run.state === "Planning") {
    return (
      <Link to={to}>
        <Badge className="bg-info text-info-foreground">{t("board.run.executing")}</Badge>
      </Link>
    );
  }
  if (run.state === "AwaitingInput") {
    return (
      <Link to={to}>
        <Badge className="bg-warning text-warning-foreground">
          {t("board.run.question")} · {age(run.createdAt)}
        </Badge>
      </Link>
    );
  }
  if (run.state === "AwaitingApproval") {
    return (
      <Link to={to}>
        <Badge className="bg-warning text-warning-foreground">{t("board.run.approval")}</Badge>
      </Link>
    );
  }
  if (run.state === "Failed") {
    return (
      <Link to={to}>
        <Badge variant="destructive">{t("board.run.failed")}</Badge>
      </Link>
    );
  }
  if (run.state === "Succeeded") {
    return (
      <Link to={to}>
        <Badge className="bg-success text-success-foreground">{t("board.run.succeeded")}</Badge>
      </Link>
    );
  }
  return (
    <Link to={to}>
      <Badge variant="outline">{run.state}</Badge>
    </Link>
  );
}

/** How long the wait has lasted — BR-006's untimed wait made visible. */
function age(iso: string): string {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 60) return `${Math.max(minutes, 1)}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return `${Math.round(hours / 24)}d`;
}
