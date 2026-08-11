import { useState } from "react";
import { MoreHorizontal, UserRound } from "lucide-react";
import { Link } from "react-router";
import { useRuns } from "@/features/runs/useRuns";
import type { RunView } from "@/features/runs/types";
import { t } from "@/shared/i18n";
// Shared with the read-only preview (#232): one chip, one meaning.
import { GateChip } from "@/shared/ui/gate-chip";
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
import { NativeSelect } from "@/shared/ui/native-select";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/shared/ui/sheet";
import { useUpdateAutomation } from "@/features/automations/useAutomations";
import { useLifecycle } from "@/features/automations/useLifecycle";
import { requestFor } from "@/features/automations/automationRequest";
import { claimantsByToStage, fold } from "@/features/automations/workflowGraph";
import { AUTOMATION_BLOCK, claimPatch, refusalFor } from "@/features/automations/chainDrag";
import type { Boundary as LifecycleBoundary, DropRefusal } from "@/features/automations/chainDrag";
import { ApiError } from "@/shared/http/client";
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
 * The board reads an Automation and writes one, so this is the Automation and not a subset of it.
 * Since #310 the write is the arrangement itself — which transition an Automation claims — and every
 * one of them goes through `requestFor`, ADR-0019's one builder.
 */
export type BoardAutomation = Automation;

/** A column: a stage of the project's lifecycle, or the pile Stories start in. */
interface Pile {
  key: string;
  label: string;
  stories: StoryView[];
}

/** A legal destination for a move. Every column is one now — there are no non-label columns. */
interface MoveTarget {
  key: string;
  label: string;
}

/**
 * The project's flow, made spatial and now **authored** here (#310, AC 1/2/12).
 *
 * <p>
 * Columns are the project's stored lifecycle stages, in the stored order — not a walk over labels.
 * There used to be one here: it built chains from output labels, put the chained Automations first and
 * the unchained ones after, and that ordering rule was invented because a derivation could not supply
 * an order. It is gone, and so is the rule. The order is stored, the owner serves it, and an
 * Automation that claims no transition contributes no stage — a Story carrying its trigger label needs
 * no column it did not already have, because that label is a mark.
 * </p>
 * <p>
 * Between every pair of columns is a <b>boundary</b>: the transition into the right-hand stage. At most
 * one Automation claims it (AC 13), and it is drawn there and nowhere else (AC 2). An unclaimed boundary
 * is a person's turn — BR-006, so no error, no "incomplete configuration" marker and no clock. The
 * boundary before the <i>first</i> column is how a step gets placed first (AC 4): assigning an
 * Automation there gives its own trigger label a stage immediately before the one it moves work into.
 * </p>
 * <p>
 * Every arrangement change is offered by an explicit control and by dragging, and both call this
 * component's own `assign` — one function per change, two ways in (AC 12). That is not a preference:
 * Playwright cannot perform an HTML5 drag (#110), so the control sharing the drop's function is what
 * puts this logic under test at all.
 * </p>
 * <p>
 * Moving a card is unchanged and still UC-008's licensed label write: ordinary matching does the rest,
 * so there is no dispatch machinery here at all.
 * </p>
 */
export function KanbanBoard({
  projectId,
  stories,
  automations,
  canArrange,
}: {
  projectId: string;
  stories: StoryView[];
  automations: BoardAutomation[];
  /**
   * Whether this caller may change the arrangement (BR-009, AC 9). False offers no control that
   * assigns, moves or clears a claim — and the API refuses one anyway, which is where the guarantee
   * lives: this only decides what is worth showing.
   */
  canArrange: boolean;
}) {
  const runs = useRuns(projectId, null);
  const move = useMoveStory(projectId);
  // The one read that replaced six walks (#310, design D6). Nothing here derives an order.
  const lifecycle = useLifecycle(projectId);
  const updateAutomation = useUpdateAutomation(projectId);
  const [dragging, setDragging] = useState<{ story: string; from: string } | null>(null);
  // The hovered legal column while a Story drag is in flight — what the drop slot renders under.
  const [over, setOver] = useState<string | null>(null);
  // The Automation in flight and the boundary it is over, so a boundary can say what its drop would
  // do — and why it would not — before the pointer lets go.
  const [carried, setCarried] = useState<BoardAutomation | null>(null);
  const [overBoundary, setOverBoundary] = useState<string | null>(null);
  // BR-001's refusal, anchored where the gesture pointed: the target column (or the open sheet).
  const [refused, setRefused] = useState<{ story: string; column: string } | null>(null);
  // The card whose move sheet is open — the touch path's whole gesture (no touch drag).
  const [moving, setMoving] = useState<StoryView | null>(null);
  const [activeColumn, setActiveColumn] = useState<string>(UNTOUCHED);

  const stages = lifecycle.data?.stages ?? [];
  const claimants = claimantsByToStage(automations);
  const gated = new Set(
    automations
      .filter((automation) => automation.enabled && automation.requiresApproval)
      .map((automation) => fold(automation.triggerLabel)),
  );

  // The latest Run per Story — the one whose state the card wears.
  const latestRun = new Map<string, RunView>();
  for (const run of runs.data ?? []) {
    // A change-targeted Run has no Story (run-on-a-pr) — nothing on this board to wear it.
    if (run.vendorStoryId === null) continue;
    const held = latestRun.get(run.vendorStoryId);
    if (!held || run.createdAt > held.createdAt) latestRun.set(run.vendorStoryId, run);
  }

  function columnOf(story: StoryView): string {
    return (
      stages.find((stage) => story.labels.some((label) => fold(label) === fold(stage))) ?? UNTOUCHED
    );
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
   * **The one arrangement function** (AC 12). The boundary's select calls it, and so does its drop —
   * so the gesture Playwright cannot perform and the control it can drive are the same code, and the
   * control is what puts the logic under test at all.
   *
   * One ordinary Automation update, through `requestFor` so the fields this screen never shows survive
   * the write (ADR-0019). What travels is `claimPatch`'s answer: the to-stage, and — at any boundary
   * but the first — the from-stage as the Automation's new trigger, because a claim's from-stage *is*
   * its trigger (design D2) and a step moved to a later boundary now fires there. Only this Automation
   * is written, so no other Automation's claimed transition can change as a consequence (AC 5).
   *
   * A stage that does not exist yet is created by the write itself — the domain inserts the from-stage
   * immediately before the to-stage — which is how a step gets placed first without a stage editor
   * coming into scope (AC 4).
   */
  function assign(automation: BoardAutomation, boundary: LifecycleBoundary) {
    setCarried(null);
    setOverBoundary(null);
    updateAutomation.mutate({
      id: automation.id,
      request: requestFor(automation, claimPatch(boundary)),
    });
  }

  /**
   * Clearing is its own change, not an assignment to nowhere: the step keeps firing at its own stage
   * and stops handing work on, so the boundary after it becomes a person's turn (AC 3, BR-006). The
   * trigger is deliberately untouched — clearing a hand-off must not also move the step.
   */
  function clear(automation: BoardAutomation) {
    setCarried(null);
    setOverBoundary(null);
    updateAutomation.mutate({
      id: automation.id,
      request: requestFor(automation, { toStage: null }),
    });
  }

  const piles: Pile[] = [
    {
      key: UNTOUCHED,
      label: t("board.untouched"),
      stories: stories.filter((story) => columnOf(story) === UNTOUCHED),
    },
    ...stages.map((stage) => ({
      key: stage,
      label: stage,
      stories: stories.filter((story) => columnOf(story) === stage),
    })),
  ];

  // A column can disappear under the pager's feet; the first pile is always there to fall back on.
  const mobileActive = piles.some((pile) => pile.key === activeColumn)
    ? activeColumn
    : (piles[0]?.key ?? UNTOUCHED);

  /** The move menu is the semantics; drag is sugar (design D1). Every column is a legal target. */
  const targetsFrom = (current: string): MoveTarget[] =>
    piles
      .filter((pile) => pile.key !== current)
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
      {/* A refused arrangement change, said once and at the top, in the API's own words: BR-003's
          refusal names the Automation already claiming that transition (AC 6), and a generic line
          would throw that name away — which is the whole content of the refusal. */}
      {updateAutomation.isError && (
        <p className="text-sm text-destructive" role="alert">
          {(updateAutomation.error instanceof ApiError && updateAutomation.error.detail) ||
            t("board.arrangeFailed")}
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
                : "bg-muted text-muted-foreground",
              pile.key !== UNTOUCHED && "font-mono",
            )}
          >
            {pile.label} <b className="font-semibold">{pile.stories.length}</b>
          </button>
        ))}
      </div>

      <div className="flex gap-3 md:snap-x md:overflow-x-auto md:pb-2">
        {piles.map((pile) => (
          <div key={pile.key} className="flex shrink-0 items-stretch gap-3">
            {/* The boundary into this stage: the transition an Automation claims, drawn between the
                two columns it joins and on no other (AC 2). Untouched has none — nothing transitions
                into "carrying no stage label at all". */}
            {pile.key === UNTOUCHED ? null : (
              <Boundary
                // The stage before this one, or null at the head of the flow — where an Automation
                // assigned here brings its own trigger label as the new first stage (AC 4).
                boundary={{ from: stages[stages.indexOf(pile.key) - 1] ?? null, to: pile.key }}
                claimant={claimants.get(fold(pile.key))}
                automations={automations}
                canArrange={canArrange}
                carried={carried}
                hovered={overBoundary === pile.key}
                gated={gated}
                busy={updateAutomation.isPending}
                // At phone width two columns never share a screen (#2b), so the boundary travels with
                // the column it leads into — "the transition into what you are looking at" is the
                // reading that survives having one column on screen. AC 12 asks for an explicit
                // control at every width the board supports, and this is where it lives on the pager.
                visible={pile.key === mobileActive}
                onHover={setOverBoundary}
                onCarry={setCarried}
                onAssign={assign}
                onClear={clear}
              />
            )}

            <section
              aria-label={pile.label}
              data-stage={pile.key === UNTOUCHED ? undefined : pile.key}
              className={cn(
                "flex w-full shrink-0 flex-col gap-2 rounded-lg border bg-muted/40 p-2 transition-colors md:w-64 md:snap-start",
                pile.key !== mobileActive && "hidden md:flex",
                over === pile.key && "border-primary bg-accent ring-4 ring-primary/10",
              )}
              onDragOver={(event) => {
                // Only a Story: an Automation being moved to a boundary is not a drop a column
                // accepts, and taking it would put a claim where a label belongs.
                if (event.dataTransfer.types.includes(AUTOMATION_BLOCK)) return;
                event.preventDefault();
                setOver(pile.key);
              }}
              onDragLeave={(event) => {
                // Leaving into a child still counts as inside; only a true exit clears the target.
                if (event.currentTarget.contains(event.relatedTarget as Node)) return;
                setOver((current) => (current === pile.key ? null : current));
              }}
              onDrop={(event) => {
                setOver(null);
                if (event.dataTransfer.types.includes(AUTOMATION_BLOCK)) return;
                const story = stories.find((candidate) => candidate.vendorId === dragging?.story);
                if (story) attempt(story, pile.key);
                setDragging(null);
              }}
            >
              <header className="flex items-center justify-between gap-2 px-1">
                <span className="flex min-w-0 items-center gap-1.5">
                  <span
                    className={cn(
                      "truncate text-xs font-semibold",
                      pile.key === UNTOUCHED
                        ? "tracking-wide text-muted-foreground uppercase"
                        : "font-mono text-muted-foreground",
                    )}
                  >
                    {pile.label}
                  </span>
                  {/* The other wait, and deliberately still on the step's own column: a Run awaiting
                      approval has reached that step, so a column before it would claim the work had
                      not arrived (#128, design D2). It stays distinct from an unclaimed boundary,
                      which is a different thing entirely (BR-007, UC-013, AC 8). */}
                  {gated.has(fold(pile.key)) ? <GateChip /> : null}
                </span>
                <span className="text-xs font-semibold text-muted-foreground">
                  {pile.stories.length}
                </span>
              </header>

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
              {over === pile.key && dragging ? (
                <div className="flex h-13 shrink-0 items-center justify-center gap-1 rounded-md border-2 border-dashed border-primary/60 bg-background/60 text-xs font-medium text-primary">
                  {t("board.dropToApply")}
                  <span className={cn(pile.key !== UNTOUCHED && "font-mono")}>{pile.label}</span>
                </div>
              ) : null}

              {pile.stories.length === 0 ? (
                <p className="px-1 py-4 text-xs text-muted-foreground">{t("board.columnEmpty")}</p>
              ) : (
                pile.stories.map((story) => (
                  <StoryCard
                    key={story.vendorId}
                    projectId={projectId}
                    story={story}
                    run={latestRun.get(story.vendorId)}
                    targets={targetsFrom(pile.key)}
                    gated={gated}
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
          </div>
        ))}

        {/* The end of the flow, stated as the fact it is and asserting nothing about who acts next
            (AC 8, BR-007): BR-007 permits a Run straight to Executing, and DEC-062 makes pushing the
            Agent's own act, so any sentence naming an actor here would be wrong for some project.
            Drawn only once there is a flow to end. */}
        {stages.length > 0 ? (
          <div
            data-flow-end="true"
            className={cn(
              "w-40 shrink-0 items-center rounded-lg border border-dashed px-3 py-2 text-xs text-muted-foreground",
              stages[stages.length - 1] === mobileActive ? "flex" : "hidden md:flex",
            )}
          >
            {t("board.flowEnds")}
          </div>
        ) : null}
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
                  {gated.has(fold(target.key)) ? <GateChip /> : null}
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
 * One boundary of the lifecycle: the transition into <c>toStage</c>.
 *
 * It says three things, in the order a reader needs them: who moves work across it, that a person
 * carries it when nobody does, and how to change either. The middle one is stated as a fact about who
 * acts (BR-006, AC 3) — no validation error, no "incomplete configuration" marker, and no elapsed
 * time, because a human wait is untimed and a clock here would invent an expectation the product does
 * not hold. It is visually its own kind, distinct from the on-card approval gate, which stays on the
 * column of the step it gates (AC 8).
 */
function Boundary({
  boundary,
  claimant,
  automations,
  canArrange,
  carried,
  hovered,
  gated,
  busy,
  visible,
  onHover,
  onCarry,
  onAssign,
  onClear,
}: {
  /** The transition this boundary is. `from` is null at the head of the flow (AC 4). */
  boundary: LifecycleBoundary;
  claimant: BoardAutomation | undefined;
  automations: BoardAutomation[];
  canArrange: boolean;
  carried: BoardAutomation | null;
  hovered: boolean;
  gated: ReadonlySet<string>;
  busy: boolean;
  visible: boolean;
  onHover: (toStage: string | null) => void;
  onCarry: (automation: BoardAutomation | null) => void;
  onAssign: (automation: BoardAutomation, boundary: LifecycleBoundary) => void;
  onClear: (automation: BoardAutomation) => void;
}) {
  const toStage = boundary.to;
  const refusal = carried ? refusalFor(carried, boundary, automations) : null;

  /**
   * What the select offers: every enabled Automation. Deliberately not filtered down to the ones that
   * would be accepted — BR-003's refusal names the Automation already claiming the transition (AC 6),
   * and that sentence is the useful answer to "why can this not go here". Hiding the option would
   * replace a named refusal with an unexplained absence.
   *
   * The one exclusion is the Automation already here, because assigning it to where it is does nothing.
   */
  const candidates = automations.filter(
    (automation) => automation.enabled && automation.id !== claimant?.id,
  );

  return (
    <div
      data-boundary={toStage}
      className={cn(
        "w-44 shrink-0 flex-col items-center justify-center gap-1.5 self-stretch rounded-lg border border-dashed px-2 py-2",
        visible ? "flex" : "hidden md:flex",
        // A place, not a marker: an unclaimed boundary is its own kind — warm fill, dashed edge,
        // person icon and a one-line explainer, so colour is never the only signal.
        claimant ? "border-border" : "border-warning/60 bg-warning/10",
        carried && hovered && !refusal && "border-primary bg-primary/10",
        carried && hovered && refusal && "border-destructive bg-destructive/10",
      )}
      onDragOver={(event) => {
        if (!canArrange) return;
        if (!event.dataTransfer.types.includes(AUTOMATION_BLOCK)) return;
        onHover(toStage);
        // A refused boundary never calls preventDefault, so the cursor says no-drop before the drop
        // is attempted — and the sentence below says why, here rather than in a toast afterwards.
        if (!refusal) event.preventDefault();
      }}
      onDragLeave={() => onHover(null)}
      onDrop={(event) => {
        onHover(null);
        if (!canArrange || refusal) return;
        const id = event.dataTransfer.getData(AUTOMATION_BLOCK);
        const dragged = automations.find((candidate) => candidate.id === id);
        if (!dragged) return;
        event.preventDefault();
        // The same function the select below calls (AC 12) — one change, two ways in.
        onAssign(dragged, boundary);
      }}
    >
      {claimant ? (
        <>
          <span
            // Named in the DOM as well as drawn: "this Automation is on this boundary and on no
            // other" (AC 2) has to be assertable without the assertion tripping over the assign
            // control's own list of candidates, which names every Automation at every boundary.
            data-claimant={claimant.triggerLabel}
            // The handle is the label, not the whole boundary: a region that is entirely draggable
            // cannot be selected, and the text inside it stops being text.
            draggable={canArrange}
            onDragStart={(event) => {
              event.dataTransfer.setData(AUTOMATION_BLOCK, claimant.id);
              event.dataTransfer.effectAllowed = "move";
              // Announced through React rather than read back from the drag: `getData` is
              // deliberately empty during `dragover` for security, so a boundary could otherwise only
              // say "something", and saying which claim a drop moves is the point of the gesture.
              onCarry(claimant);
            }}
            onDragEnd={() => onCarry(null)}
            className={cn(
              "flex max-w-full items-center gap-1 truncate font-mono text-[11px] font-semibold text-primary",
              canArrange && "cursor-grab active:cursor-grabbing",
            )}
            title={canArrange ? t("board.boundary.move") : undefined}
          >
            {claimant.triggerLabel}
          </span>
          {gated.has(fold(claimant.triggerLabel)) ? <GateChip /> : null}
          <span className="text-center text-[10px] leading-snug text-muted-foreground">
            {t("board.boundary.claimed")}
          </span>
        </>
      ) : (
        <>
          <span className="flex items-center gap-1 text-[11px] font-semibold text-warning">
            <UserRound className="size-3 shrink-0" aria-hidden="true" />
            {t("board.boundary.person")}
          </span>
          <span className="text-center text-[10px] leading-snug text-muted-foreground">
            {boundary.from === null
              ? t("board.boundary.firstHint")
              : t("board.boundary.personHint")}
          </span>
        </>
      )}

      {/* What this drop would do, spelled out before it happens — or the rule that stops it, quoted
          where the pointer is rather than after the gesture. */}
      {carried && hovered ? (
        <span className="text-center text-[10px] leading-snug">
          {refusal ? (
            <span className="text-destructive">
              <span className="font-mono">{carried.triggerLabel}</span> {explain(refusal)}
            </span>
          ) : (
            <span className="text-primary">
              <span className="font-mono">{carried.triggerLabel}</span>{" "}
              {t("board.boundary.wouldMoveTo")} <span className="font-mono">{toStage}</span>
            </span>
          )}
        </span>
      ) : null}

      {/* The explicit controls — the same changes the drag makes, through the same function (AC 12).
          Offered at every width, and to nobody who may not rearrange (BR-009, AC 9). */}
      {canArrange ? (
        <>
          <NativeSelect
            className="h-7 w-full text-[11px]"
            aria-label={`${t("board.boundary.assign")} ${toStage}`}
            value=""
            disabled={busy}
            onChange={(event) => {
              const chosen = automations.find((candidate) => candidate.id === event.target.value);
              if (chosen) onAssign(chosen, boundary);
            }}
          >
            <option value="">{t("board.boundary.assign")}</option>
            {candidates.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidate.triggerLabel}
              </option>
            ))}
          </NativeSelect>
          {claimant ? (
            <Button
              variant="ghost"
              size="sm"
              type="button"
              className="h-7 text-[11px] text-warning"
              disabled={busy}
              // Its own change, not an assignment to nowhere: the step keeps firing at its own stage
              // and stops handing work on, so this boundary becomes a person's turn (AC 3).
              onClick={() => onClear(claimant)}
            >
              {t("board.boundary.clear")}
            </Button>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

/** The surviving refusal sentences, each naming the rule rather than the symptom. */
function explain(refusal: DropRefusal): string {
  if (refusal === "shared") return t("board.boundary.refuseShared");
  if (refusal === "self") return t("board.boundary.refuseSelf");
  return t("board.boundary.refuseAlready");
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
                  {gated.has(fold(target.key)) ? <GateChip /> : null}
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
        {/* This wait carries its age, and it is the Run's own — BR-007's approval gate, not the
            boundary's. An unclaimed boundary never carries a clock (BR-006). */}
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

/** How long a Run has been waiting — the only wait on this board that carries a clock. */
function age(iso: string): string {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 60) return `${Math.max(minutes, 1)}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return `${Math.round(hours / 24)}d`;
}
