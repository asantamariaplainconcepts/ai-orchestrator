import { useState } from "react";
import { MoreHorizontal, ShieldCheck, UserRound, UserRoundPlus } from "lucide-react";
import { Link } from "react-router";
import { useRuns } from "@/features/runs/useRuns";
import type { RunView } from "@/features/runs/types";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/shared/ui/sheet";
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

/** A legal destination for a move — a step column or Untouched, never a human pile. */
interface MoveTarget {
  key: string;
  label: string;
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
  // The hovered legal column while a drag is in flight — what the drop slot renders under.
  const [over, setOver] = useState<string | null>(null);
  // BR-001's refusal, anchored where the gesture pointed: the target column (or the open sheet).
  const [refused, setRefused] = useState<{ story: string; column: string } | null>(null);
  // The card whose move sheet is open — the touch path's whole gesture (no touch drag).
  const [moving, setMoving] = useState<StoryView | null>(null);
  const [activeColumn, setActiveColumn] = useState<string>(UNTOUCHED);

  const enabled = automations.filter((automation) => automation.enabled);

  // Ordered by the flow, not by when somebody happened to create each Automation (#128, design D1).
  // Deduplicated: two Automations may share a trigger label, and that is one column, not two.
  const byTrigger = new Map(enabled.map((automation) => [automation.triggerLabel, automation]));
  // The first hand-off that lands on a column, since #165 made the output a set: the board is a
  // row of columns, so it follows the spine the canvas draws and leaves the branches to the canvas,
  // which has room to say what they are.
  const handsTo = (automation: BoardAutomation) =>
    automation.outputLabels
      .map((label) => byTrigger.get(label))
      .find((target) => target !== undefined);
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

  /** Whether the move was accepted — the sheet stays open on a refusal, so the caller must know. */
  function attempt(story: StoryView, to: string): boolean {
    const from = columnOf(story);
    if (from === to) return true;

    // BR-001 refused before any write (design D3): letting the label land and the match decline
    // silently would leave the vendor labelled and this board lying.
    const run = latestRun.get(story.vendorId);
    if (to !== UNTOUCHED && run && ACTIVE_STATES.includes(run.state)) {
      setRefused({ story: story.vendorId, column: to });
      return false;
    }

    setRefused(null);
    move.mutate({ vendorStoryId: story.vendorId, from, to });
    return true;
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

  // A column can disappear under the pager's feet (an Automation disabled mid-visit); the first
  // pile is always there to fall back on.
  const mobileActive = piles.some((pile) => pile.key === activeColumn)
    ? activeColumn
    : (piles[0]?.key ?? UNTOUCHED);

  /** The move menu is the semantics; drag is sugar (design D1). Human piles are never targets. */
  const targetsFrom = (current: string): MoveTarget[] =>
    piles
      .filter((pile) => !pile.after && pile.key !== current)
      .map((pile) => ({ key: pile.key, label: pile.label }));

  const movingRun = moving ? latestRun.get(moving.vendorId) : undefined;
  const movingRefused = moving !== null && refused?.story === moving.vendorId;

  return (
    <div className="flex flex-col gap-3">
      {move.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("board.moveFailed")}
        </p>
      )}

      {/* The pager: one column per phone screen, every column's count in reach (#2b). */}
      <div className="flex gap-1.5 overflow-x-auto pb-1 md:hidden">
        {piles.map((pile) => (
          <button
            key={pile.key}
            type="button"
            aria-pressed={pile.key === mobileActive}
            onClick={() => setActiveColumn(pile.key)}
            className={cn(
              "flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition-colors outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50",
              pile.key === mobileActive
                ? "bg-primary text-primary-foreground"
                : pile.after
                  ? "bg-warning/15 text-warning"
                  : "bg-muted text-muted-foreground",
              pile.key !== UNTOUCHED && !pile.after && "font-mono",
            )}
          >
            {pile.label} <b className="font-semibold">{pile.stories.length}</b>
          </button>
        ))}
      </div>

      <div className="flex gap-3 md:snap-x md:overflow-x-auto md:pb-2">
        {piles.map((pile) => (
          <section
            key={pile.key}
            aria-label={pile.label}
            className={cn(
              "flex w-full shrink-0 flex-col gap-2 rounded-lg border p-2 transition-colors md:w-64 md:snap-start",
              pile.key !== mobileActive && "hidden md:flex",
              // A place, not a marker: the human column is its own kind — warm fill, left accent,
              // person icon and a one-line explainer, so color is never the only signal.
              pile.after
                ? "border-l-4 border-warning/60 border-l-warning bg-warning/10"
                : "bg-muted/40",
              over === pile.key && !pile.after && "border-primary bg-accent ring-4 ring-primary/10",
            )}
            onDragOver={(event) => {
              // A human column is not a label, so nothing can be dropped into it: a Story arrives
              // there by its step finishing, never by a gesture.
              if (pile.after) return;
              event.preventDefault();
              setOver(pile.key);
            }}
            onDragLeave={(event) => {
              // Leaving into a child still counts as inside; only a true exit clears the target.
              if (event.currentTarget.contains(event.relatedTarget as Node)) return;
              setOver((current) => (current === pile.key ? null : current));
            }}
            onDrop={() => {
              setOver(null);
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
                  className={cn(
                    "truncate text-xs font-semibold",
                    pile.after
                      ? "text-warning"
                      : pile.key === UNTOUCHED
                        ? "tracking-wide text-muted-foreground uppercase"
                        : "font-mono text-muted-foreground",
                  )}
                  title={pile.after ? t("board.human.hint") : undefined}
                >
                  {pile.label}
                </span>
                {/* The other wait, and deliberately still on the step's own column: a Run awaiting
                    approval has reached that step, so a column before it would claim the work had
                    not arrived (#128, design D2). A chip, not a badge — it must not compete with
                    the run badges the cards wear. */}
                {gated.has(pile.key) ? <GateChip /> : null}
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
                      className="rounded p-0.5 text-warning outline-none hover:bg-warning/10 focus-visible:ring-[3px] focus-visible:ring-ring/50"
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
                            promptPath: step.promptPath ?? null,
                            outputLabels: [],
                          },
                        })
                      }
                    >
                      <UserRoundPlus className="size-3.5" aria-hidden="true" />
                    </button>
                  ) : null;
                })()}
                <span className="text-xs font-semibold text-muted-foreground">
                  {pile.stories.length}
                </span>
              </span>
            </header>

            {/* The kind, said rather than implied: which step this wait follows, and that the
                column takes no drops. */}
            {pile.after ? (
              <p className="px-1 text-[10.5px] leading-snug text-warning">
                <span className="font-mono">{pile.after.triggerLabel}</span>{" "}
                {t("board.human.explainer")}
              </p>
            ) : null}

            {/* BR-001, said where the gesture pointed — on the column, before any write. */}
            {refused?.column === pile.key && moving === null ? (
              <p
                className="rounded-md border border-destructive/40 bg-destructive/10 px-2 py-1.5 text-xs text-destructive"
                role="status"
                aria-live="polite"
              >
                {t("board.refusedActiveRun")}
              </p>
            ) : null}

            {/* The drag's visible, readable outcome — an explicit slot naming the label a drop
                would apply. */}
            {over === pile.key && dragging && !pile.after ? (
              <div className="flex h-13 shrink-0 items-center justify-center gap-1 rounded-md border-2 border-dashed border-primary/60 bg-background/60 text-xs font-medium text-primary">
                {t("board.dropToApply")}
                <span className={cn(pile.key !== UNTOUCHED && "font-mono")}>{pile.label}</span>
              </div>
            ) : null}

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
                  targets={targetsFrom(pile.key)}
                  gated={gated}
                  human={Boolean(pile.after)}
                  lifted={dragging?.story === story.vendorId}
                  onDragStart={() => {
                    setRefused(null);
                    setDragging({ story: story.vendorId, from: pile.key });
                  }}
                  onDragEnd={() => {
                    setDragging(null);
                    setOver(null);
                  }}
                  onMove={(to) => attempt(story, to)}
                  onOpenMove={() => {
                    setRefused(null);
                    setMoving(story);
                  }}
                />
              ))
            )}
          </section>
        ))}
      </div>

      {/* The touch move path: the sheet IS the gesture (#2b) — targets at thumb size, the Gate
          chip travelling with the target it belongs to, and BR-001's refusal answered in place. */}
      <Sheet
        open={moving !== null}
        onOpenChange={(open) => {
          if (!open) {
            setMoving(null);
            setRefused(null);
          }
        }}
      >
        <SheetContent side="bottom" className="rounded-t-xl">
          <SheetHeader>
            <SheetTitle className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
              {t("board.moveTo")} <span className="font-mono">#{moving?.vendorId}</span>
            </SheetTitle>
          </SheetHeader>
          <div className="flex flex-col gap-1.5 px-4 pb-6">
            {movingRefused ? (
              <>
                <p className="text-sm text-destructive" role="status" aria-live="polite">
                  {t("board.refusedActiveRun")}
                </p>
                {movingRun ? (
                  <Link
                    className="text-sm font-medium text-primary underline-offset-4 hover:underline"
                    to={`/projects/${projectId}/runs/${movingRun.id}`}
                    onClick={() => setMoving(null)}
                  >
                    {t("board.viewActiveRun")}
                  </Link>
                ) : null}
              </>
            ) : moving ? (
              targetsFrom(columnOf(moving)).map((target) => (
                <button
                  key={target.key}
                  type="button"
                  onClick={() => {
                    if (attempt(moving, target.key)) setMoving(null);
                  }}
                  className="flex min-h-12 items-center justify-between rounded-lg border bg-card px-3.5 text-sm font-medium transition-colors outline-none hover:bg-accent focus-visible:ring-[3px] focus-visible:ring-ring/50"
                >
                  <span className={cn(target.key !== UNTOUCHED && "font-mono")}>
                    {target.label}
                  </span>
                  {gated.has(target.key) ? <GateChip /> : null}
                </button>
              ))
            ) : null}
          </div>
        </SheetContent>
      </Sheet>
    </div>
  );
}

/**
 * Two zones only (#2c): a meta row (id, run badge, ⋯ actions) and the title. The whole card is
 * the link to the Story on pointer widths; on phones the tap opens the move sheet instead,
 * because there the move is the gesture the board exists for.
 */
function StoryCard({
  projectId,
  story,
  run,
  targets,
  gated,
  human,
  lifted,
  onDragStart,
  onDragEnd,
  onMove,
  onOpenMove,
}: {
  projectId: string;
  story: StoryView;
  run: RunView | undefined;
  targets: MoveTarget[];
  gated: ReadonlySet<string>;
  human: boolean;
  lifted: boolean;
  onDragStart: () => void;
  onDragEnd: () => void;
  onMove: (to: string) => void;
  onOpenMove: () => void;
}) {
  const storyPath = `/projects/${projectId}/stories/${story.vendorId}`;

  return (
    <article
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      className={cn(
        "group relative flex min-h-11 cursor-grab flex-col gap-1.5 rounded-md border bg-card p-2.5 transition-shadow active:cursor-grabbing md:min-h-0",
        human && "border-warning/40",
        // The lift: the dragged card visibly leaves the column instead of staying put.
        lifted && "-rotate-1 opacity-90 shadow-lg",
      )}
    >
      {/* Title is plain text and the whole card is the link target — a stretched link, kept
          under the controls. Desktop only: on phones the same surface opens the move sheet. */}
      <Link
        to={storyPath}
        aria-label={story.title}
        draggable={false}
        className="absolute inset-0 hidden rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 md:block"
      />
      <button
        type="button"
        aria-label={`${t("board.moveTo")} #${story.vendorId}`}
        onClick={onOpenMove}
        className="absolute inset-0 rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 md:hidden"
      />

      <span className="flex items-center justify-between gap-2">
        <span className="font-mono text-xs text-muted-foreground">#{story.vendorId}</span>
        {/* A stopPropagation island above the stretched link: the badge and the ⋯ menu act,
            the rest of the card navigates. */}
        <span className="relative z-10 flex items-center gap-1">
          {run ? <RunBadge projectId={projectId} run={run} /> : null}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon-xs"
                aria-label={t("board.cardActions")}
                // Revealed on hover and focus, never removed from the accessibility tree —
                // opacity, not display. Phones use the sheet, so the kebab stays off there.
                className="hidden text-muted-foreground opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100 focus-visible:opacity-100 data-[state=open]:opacity-100 md:inline-flex"
              >
                <MoreHorizontal className="size-3.5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
                {t("board.moveTo")}
              </DropdownMenuLabel>
              {targets.map((target) => (
                <DropdownMenuItem key={target.key} onSelect={() => onMove(target.key)}>
                  <span className={cn("text-xs", target.key !== UNTOUCHED && "font-mono")}>
                    {target.label}
                  </span>
                  {gated.has(target.key) ? <GateChip /> : null}
                </DropdownMenuItem>
              ))}
              <DropdownMenuSeparator />
              <DropdownMenuItem asChild>
                <Link to={storyPath}>{t("backlog.table.viewRuns")}</Link>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </span>
      </span>

      <span className="text-sm leading-snug font-medium">{story.title}</span>
    </article>
  );
}

/**
 * The approval gate as a chip on the thing that is gated — the column header or a move target —
 * instead of a badge competing with the run badges the cards wear.
 */
function GateChip() {
  return (
    <span
      title={t("board.gated.hint")}
      className="inline-flex shrink-0 items-center gap-1 rounded border border-info/40 bg-info/10 px-1.5 text-[10px] font-semibold text-info"
    >
      <ShieldCheck className="size-2.5" aria-hidden="true" />
      {t("board.gated")}
    </span>
  );
}

/** What the Story's latest Run is doing, and a way in when it is worth watching. */
function RunBadge({ projectId, run }: { projectId: string; run: RunView }) {
  const to = `/projects/${projectId}/runs/${run.id}`;

  if (run.state === "Executing" || run.state === "Planning") {
    return (
      <Link to={to}>
        <Badge className="bg-info text-info-foreground">
          <span
            className="size-1.5 animate-pulse rounded-full bg-info-foreground/70"
            aria-hidden="true"
          />
          {t("board.run.executing")}
        </Badge>
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
        {/* The wait carries its age (BR-006): "Plan awaits · 2h" is a queue with a clock. */}
        <Badge className="bg-warning text-warning-foreground">
          {t("board.run.approval")} · {age(run.createdAt)}
        </Badge>
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
