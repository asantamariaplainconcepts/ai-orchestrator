import { useState } from "react";
import { Link } from "react-router";
import { useRuns } from "@/features/runs/useRuns";
import type { RunView } from "@/features/runs/types";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { NativeSelect } from "@/shared/ui/native-select";
import { UNTOUCHED, useMoveStory } from "./useMoveStory";
import type { StoryView } from "./types";

/** BR-001's list, client-side. The server remains the authority; this refuses the gesture. */
const ACTIVE_STATES: readonly RunView["state"][] = [
  "Queued",
  "Planning",
  "AwaitingApproval",
  "Executing",
  "AwaitingInput",
];

export interface BoardAutomation {
  id: string;
  triggerLabel: string;
  enabled: boolean;
  requiresApproval: boolean;
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
  const [dragging, setDragging] = useState<{ story: string; from: string } | null>(null);
  const [refused, setRefused] = useState<string | null>(null);

  // Deduplicated, in configured order: two Automations may share a trigger label, and that is
  // one column, not two.
  const columns = [
    ...new Set(automations.filter((automation) => automation.enabled).map((a) => a.triggerLabel)),
  ];
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

  const piles: { key: string; label: string; stories: StoryView[] }[] = [
    {
      key: UNTOUCHED,
      label: t("board.untouched"),
      stories: stories.filter((story) => columnOf(story) === UNTOUCHED),
    },
    ...columns.map((column) => ({
      key: column,
      label: column,
      stories: stories.filter((story) => columnOf(story) === column),
    })),
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
            className="flex w-64 shrink-0 snap-start flex-col gap-2 rounded-lg border bg-muted/40 p-2"
            onDragOver={(event) => event.preventDefault()}
            onDrop={() => {
              const story = stories.find((candidate) => candidate.vendorId === dragging?.story);
              if (story) attempt(story, pile.key);
              setDragging(null);
            }}
          >
            <header className="flex items-center justify-between gap-2 px-1">
              <span className="flex min-w-0 items-center gap-1.5">
                <span className="truncate text-sm font-semibold">{pile.label}</span>
                {gated.has(pile.key) ? (
                  <Badge className="bg-info text-info-foreground" title={t("board.gated.hint")}>
                    {t("board.gated")}
                  </Badge>
                ) : null}
              </span>
              <span className="text-xs text-muted-foreground">{pile.stories.length}</span>
            </header>

            {pile.stories.length === 0 ? (
              <p className="px-1 py-4 text-xs text-muted-foreground">{t("board.columnEmpty")}</p>
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
