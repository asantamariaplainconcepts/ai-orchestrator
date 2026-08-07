import { useMemo, useState, type ReactNode } from "react";
import { Check, X } from "lucide-react";
import { Link, useParams } from "react-router";
import { renderStoryMarkdown } from "@/features/backlog/markdown";
import { useAutomations } from "@/features/automations/useAutomations";
import { podsBlocked, usePods } from "@/features/pods/usePods";
import { RunTranscript, TranscriptSpend } from "./RunTranscript";
import { ApiError } from "@/shared/http/client";
import { t, tCount, type TranslationKey } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { AppShell } from "@/shared/ui/AppShell";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { LocusChip } from "@/shared/ui/locus";
import { NativeSelect } from "@/shared/ui/native-select";
import { Card } from "@/shared/ui/card";
import { RunChanges } from "./RunChanges";
import { RunPreviewFrame } from "./RunPreviewFrame";
import { useRunChanges } from "./useRuns";
import { parseTranscript } from "./transcript";
import type { RunView } from "./types";
import {
  formatCost,
  useCancelRun,
  useDecideOnPlan,
  useDismissFailure,
  useRunLog,
  useRunPreview,
  useRuns,
} from "./useRuns";
import { useRunNow } from "./useRunNow";

/** The state names as copy — the UI never prints the enum's internal spelling. */
const STATE_COPY = {
  Queued: "run.state.queued",
  Planning: "run.state.planning",
  AwaitingApproval: "run.state.awaitingApproval",
  Executing: "run.state.executing",
  AwaitingInput: "run.state.awaitingInput",
  Succeeded: "run.state.succeeded",
  Failed: "run.state.failed",
  Cancelled: "run.state.cancelled",
} as const satisfies Record<RunView["state"], TranslationKey>;

/** Soft fill + border of each state's family — a pill, never a bare string. */
const STATE_STYLE = {
  Queued: "border-border bg-muted text-muted-foreground",
  Planning: "border-info/40 bg-info/10 text-info",
  AwaitingApproval: "border-warning/40 bg-warning/15 text-warning",
  Executing: "border-info/40 bg-info/10 text-info",
  AwaitingInput: "border-warning/40 bg-warning/15 text-warning",
  Succeeded: "border-success/40 bg-success/10 text-success",
  Failed: "border-destructive/40 bg-destructive/10 text-destructive",
  Cancelled: "border-border bg-transparent text-muted-foreground",
} as const satisfies Record<RunView["state"], string>;

/**
 * UC-013's review surface — the page the use case always assumed and #20 did not build. The
 * Plan is model output rendered in a browser, so it goes through the same sanitiser as a
 * Story's description and its documents (approval-gate D6).
 *
 * On the Platform theme since the 2026-08 design review (DEC-051): the lifecycle is a stepper,
 * the decision is a bar that cannot be missed, and the metadata is a rail beside the Plan
 * instead of a table above it.
 */
export function RunScreen() {
  const { projectId = "", runId = "" } = useParams();
  // The list is already the read model for Runs; one more endpoint for one row would be a
  // second source of the same truth.
  const runs = useRuns(projectId, null);
  // For the rail's Automation row: the trigger label is the name a person knows the step by.
  const automations = useAutomations(projectId);
  const decide = useDecideOnPlan(projectId);
  const cancel = useCancelRun(projectId);
  // #145 — both decisions a failure can carry, where the failure is. Run again goes through the
  // Run-now path (design D1), so BR-001, BR-002 and the approval gate apply without this screen
  // knowing they exist.
  const runAgain = useRunNow(projectId);
  // The re-run's per-Run runtime choice (#244): "" is "as resolved", the honest default.
  const [runAgainRuntime, setRunAgainRuntime] = useState("");
  const dismiss = useDismissFailure(projectId);

  const run = runs.data?.find((candidate) => candidate.id === runId);
  const log = useRunLog(projectId, runId);
  const transcript = useMemo(
    () => (log.data ? parseTranscript(log.data.content) : null),
    [log.data],
  );
  // The log's own done-flag is the terminal signal, because the server derives it from
  // RunStates.IsTerminal — reusing it here avoids a fourth hand-written copy of the state list,
  // which is exactly how the previous three drifted.
  const runFinished = log.data?.complete ?? false;
  const preview = useRunPreview(projectId, runId, runFinished);
  const awaiting = run?.state === "AwaitingApproval";
  const triggerLabel =
    automations.data?.find((automation) => automation.id === run?.automationId)?.triggerLabel ??
    null;

  // Only an unfinished Run can be cancelled; the API refuses the rest, so the control follows.
  const cancellable =
    run !== undefined &&
    ["Queued", "Planning", "AwaitingApproval", "Executing"].includes(run.state);

  // Design review 5c: a pod Run queued while pods cannot take work explains itself with a
  // pointer, never with destructive styling — the cause lives on the panel, not on the Run.
  const queuedForPods = run?.state === "Queued" && run.locus === "Pod";
  const pods = usePods({ enabled: queuedForPods });
  const waitingForPods = queuedForPods && podsBlocked(pods.data);

  return (
    <AppShell
      crumbs={[
        { label: t("shell.crumb.projects"), to: "/projects" },
        { label: t("run.crumb.project"), to: `/projects/${projectId}` },
        { label: t("run.title.fallback") },
      ]}
      title={
        run
          ? `${t("run.title.fallback")} · ${
              run.targetChangeNumber !== null
                ? `PR #${run.targetChangeNumber}`
                : `#${run.vendorStoryId}`
            }`
          : t("run.title.fallback")
      }
      actions={
        run ? (
          <>
            {run.targetChangeNumber !== null ? (
              <Button asChild variant="outline" size="sm">
                <a href={run.targetChangeUrl ?? "#"} target="_blank" rel="noreferrer">
                  {t("run.field.change")} #{run.targetChangeNumber}
                </a>
              </Button>
            ) : (
              <Button asChild variant="outline" size="sm">
                <Link to={`/projects/${projectId}/stories/${run.vendorStoryId}`}>
                  {t("run.field.story")} #{run.vendorStoryId}
                </Link>
              </Button>
            )}
            {/* Destructive, and dressed as one — never a twin of the navigation beside it. */}
            {cancellable ? (
              <Button
                variant="outline"
                size="sm"
                className="border-destructive/40 text-destructive hover:bg-destructive/10 hover:text-destructive"
                disabled={cancel.isPending}
                onClick={() => cancel.mutate(runId)}
              >
                {cancel.isPending ? t("run.cancelling") : t("run.cancel")}
              </Button>
            ) : null}
          </>
        ) : undefined
      }
    >
      {/* While the decision bar is pinned to the phone's bottom edge, the content clears it. */}
      <div className={cn("flex flex-col gap-4", awaiting && "pb-28 md:pb-0")}>
        {runs.isPending && <p className="text-sm text-muted-foreground">{t("run.loading")}</p>}
        {runs.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("run.error")}
          </p>
        )}
        {runs.data && !run && <p className="text-sm text-muted-foreground">{t("run.notFound")}</p>}

        {/* The API's own reason: a re-run refused by BR-001 must say so in Run now's voice. */}
        {runAgain.isError && (
          <p className="text-sm text-destructive" role="alert">
            {(runAgain.error instanceof ApiError && runAgain.error.detail) || t("run.again.failed")}
          </p>
        )}
        {dismiss.isError && (
          <p className="text-sm text-destructive" role="alert">
            {(dismiss.error instanceof ApiError && dismiss.error.detail) || t("run.dismiss.failed")}
          </p>
        )}
        {cancel.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("run.cancelFailed")}
          </p>
        )}
        {decide.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("run.decideFailed")}
          </p>
        )}

        {run && (
          <>
            {/* Turn 7: a failure is answerable where it is stated — full reason, the two
                decisions (#145) inside the banner and nowhere else, and the cause's remedy
                linked when the cause maps to a surface. The map is a closed list against the
                executor's own sentences (RunExecutor's refusals); anything else degrades to a
                banner without a link, never a guessed one. */}
            {run.state === "Failed" ? (
              <div
                role="alert"
                className="flex flex-col gap-3 rounded-lg border border-destructive/40 bg-destructive/10 p-4 md:flex-row md:items-start md:justify-between"
              >
                <div className="flex min-w-0 flex-col gap-1">
                  <span className="text-sm font-semibold text-destructive">
                    {t("run.failure.title")}
                  </span>
                  <span className="text-sm break-words">
                    {run.failureReason ?? t("run.failure.unknown")}
                    {remedyFor(run.failureReason) === "settings" ? (
                      <>
                        {" "}
                        <Link
                          className="font-medium text-primary underline-offset-4 hover:underline"
                          to={`/projects/${projectId}?tab=settings`}
                        >
                          {t("run.failure.remedy.settings")}
                        </Link>
                      </>
                    ) : remedyFor(run.failureReason) === "automations" ? (
                      <>
                        {" "}
                        <Link
                          className="font-medium text-primary underline-offset-4 hover:underline"
                          to={`/projects/${projectId}?tab=automations`}
                        >
                          {t("run.failure.remedy.automations")}
                        </Link>
                      </>
                    ) : null}
                  </span>
                </div>
                <span className="flex shrink-0 items-center gap-2">
                  {run.vendorStoryId !== null && run.automationId !== null ? (
                    <>
                      {/* The re-run is a launch point too (#244): the resolution pre-selected,
                          changeable for this Run only. */}
                      <NativeSelect
                        aria-label={t("automations.runtime")}
                        className="h-8 text-xs"
                        value={runAgainRuntime}
                        onChange={(event) => setRunAgainRuntime(event.target.value)}
                      >
                        <option value="">{t("runs.runNow.projectDefaultRuntime")}</option>
                        {(["ClaudeCodeHeadless", "OpenCode"] as const).map((candidate) => (
                          <option key={candidate} value={candidate}>
                            {candidate}
                          </option>
                        ))}
                      </NativeSelect>
                      <Button
                        size="sm"
                        disabled={runAgain.isPending}
                        onClick={() =>
                          runAgain.mutate({
                            vendorStoryId: run.vendorStoryId!,
                            automationId: run.automationId!,
                            runtime: runAgainRuntime || undefined,
                          })
                        }
                      >
                        {runAgain.isPending ? t("run.again.pending") : t("run.again")}
                      </Button>
                    </>
                  ) : null}
                  {run.dismissedAt ? (
                    <Badge variant="outline">
                      {t("run.dismissed")} · {formatWhen(run.dismissedAt)}
                    </Badge>
                  ) : (
                    <Button
                      variant="outline"
                      size="sm"
                      className="bg-background"
                      disabled={dismiss.isPending}
                      title={t("run.dismiss.hint")}
                      onClick={() => dismiss.mutate(runId)}
                    >
                      {dismiss.isPending ? t("run.dismiss.pending") : t("run.dismiss")}
                    </Button>
                  )}
                </span>
              </div>
            ) : null}

            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline" className={STATE_STYLE[run.state]}>
                {t(STATE_COPY[run.state])}
              </Badge>
              {/* Mock 3c: locus beside the state, in the vocabulary the projects list uses. */}
              <LocusChip locus={run.locus} />
              {waitingForPods ? (
                <span className="text-xs text-muted-foreground">
                  {t("run.queuedPods")}{" "}
                  <Link className="text-primary underline-offset-4 hover:underline" to="/pods">
                    {t("run.queuedPods.seeWhy")}
                  </Link>
                </span>
              ) : null}
            </div>

            <RunStepper run={run} />

            {/* The page's primary action when a plan waits — a bar, never a button buried in a
                card header. On phones it pins to the bottom edge, where the thumb is. */}
            {awaiting && (
              <div className="fixed inset-x-0 bottom-0 z-40 flex flex-col gap-2 border-t bg-card p-4 shadow-lg md:static md:z-auto md:flex-row md:items-center md:justify-between md:gap-3 md:rounded-lg md:border md:border-warning/40 md:bg-warning/15 md:p-3 md:shadow-none">
                <span className="text-xs text-muted-foreground md:text-sm md:font-medium md:text-foreground">
                  {t("run.decision.explainer")}
                </span>
                <span className="flex gap-2">
                  <Button
                    className="min-h-12 flex-1 md:min-h-9 md:flex-none"
                    disabled={decide.isPending}
                    onClick={() => decide.mutate({ runId, approve: true })}
                  >
                    {decide.isPending ? t("run.deciding") : t("run.approve")}
                  </Button>
                  <Button
                    variant="outline"
                    className="min-h-12 bg-background md:min-h-9"
                    disabled={decide.isPending}
                    onClick={() => decide.mutate({ runId, approve: false })}
                  >
                    {t("run.reject")}
                  </Button>
                </span>
              </div>
            )}

            <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_280px]">
              <div className="flex min-w-0 flex-col gap-4">
                {/* The instruction a change Run executed — its record (run-on-a-pr): what was
                    asked belongs beside what happened, or the transcript is an answer to an
                    invisible question. */}
                {run.instruction ? (
                  <Card className="gap-0 py-0">
                    <div className="flex items-center gap-2 border-b px-4 py-3">
                      <h2 className="text-sm font-semibold">{t("run.section.instruction")}</h2>
                    </div>
                    <div className="px-4 py-3">
                      <p className="text-sm leading-relaxed whitespace-pre-wrap">
                        {run.instruction}
                      </p>
                    </div>
                  </Card>
                ) : null}
                {/* Turn 7: a section with nothing to show takes one line, not a blank card —
                    the giant hole disappears. A plan that is merely awaited keeps its card. */}
                {!run.plan && !awaiting ? (
                  <Card className="gap-0 py-0">
                    <div className="flex items-baseline gap-2 px-4 py-3">
                      <h2 className="text-sm font-semibold">{t("run.section.plan")}</h2>
                      <span className="text-sm text-muted-foreground">{t("run.plan.none")}</span>
                    </div>
                  </Card>
                ) : (
                  <Card className="gap-0 py-0">
                    <div className="flex items-center gap-2 border-b px-4 py-3">
                      <h2 className="text-sm font-semibold">{t("run.section.plan")}</h2>
                      {awaiting ? (
                        <Badge
                          variant="outline"
                          className="border-warning/40 bg-warning/15 text-warning"
                        >
                          {t("run.plan.waiting")}
                        </Badge>
                      ) : null}
                    </div>
                    <div className="px-4 py-3">
                      {run.plan ? (
                        <div
                          className="prose text-sm leading-relaxed"
                          // Sanitised — a Plan is model output, as untrusted as any other text we
                          // did not write (approval-gate D6).
                          dangerouslySetInnerHTML={{ __html: renderStoryMarkdown(run.plan) }}
                        />
                      ) : (
                        <p className="text-sm text-muted-foreground">{t("run.plan.none")}</p>
                      )}
                    </div>
                  </Card>
                )}

                {/* Above the output, because both are live and they read as one idea: this Run
                    is happening now. Renders nothing at all once it is not (run-previews D1). */}
                <RunPreviewFrame
                  projectId={projectId}
                  runId={runId}
                  // Both, and the first is not redundant: `enabled` stops the query from
                  // FETCHING, it does not retract what it already fetched. On a finished Run the
                  // log has not arrived on the first render, so the preview query fires and its
                  // answer would otherwise stick — a frame on a Run that ended.
                  available={!runFinished && (preview.data?.available ?? false)}
                  runFinished={runFinished}
                />

                <Card className="gap-0 py-0">
                  <div
                    className={cn(
                      "flex flex-wrap items-center gap-2 px-4 py-3",
                      !(
                        log.data &&
                        log.data.complete &&
                        log.data.content.length === 0 &&
                        !log.isError
                      ) && "border-b",
                    )}
                  >
                    <h2 className="text-sm font-semibold">{t("run.section.log")}</h2>
                    {log.data &&
                    log.data.complete &&
                    log.data.content.length === 0 &&
                    !log.isError ? (
                      <span className="text-sm text-muted-foreground">{t("run.log.none")}</span>
                    ) : null}
                    {/* Live while it runs (UC-027): the poll stops itself on terminal (D3), and
                        the live region announces the stop. */}
                    <span aria-live="polite">
                      {log.data && !log.data.complete ? (
                        <Badge variant="outline" className="border-info/40 bg-info/10 text-info">
                          <span
                            className="size-1.5 animate-pulse rounded-full bg-info"
                            aria-hidden="true"
                          />
                          {t("run.log.live")}
                        </Badge>
                      ) : null}
                    </span>
                    {transcript && log.data && log.data.content.length > 0 ? (
                      <span className="ml-auto">
                        <TranscriptSpend totals={transcript.totals} />
                      </span>
                    ) : null}
                  </div>
                  {/* Empty-and-finished collapses with its header line above; anything live or
                      failed keeps the body. */}
                  {log.data &&
                  log.data.complete &&
                  log.data.content.length === 0 &&
                  !log.isError ? null : (
                    <div className="px-4 py-3">
                      {log.isError && (
                        <p className="text-sm text-destructive" role="alert">
                          {t("run.log.error")}
                        </p>
                      )}
                      {log.data &&
                        (log.data.content.length > 0 && transcript ? (
                          <RunTranscript entries={transcript.entries} />
                        ) : (
                          <p className="text-sm text-muted-foreground">
                            {log.data.complete ? t("run.log.none") : t("run.log.waitingForOutput")}
                          </p>
                        ))}
                    </div>
                  )}
                </Card>

                {/* Turn 7 D1: the diff needs the width the body has — 280px is illegible for a
                    diff by definition. */}
                <RunChanges projectId={projectId} runId={runId} />
              </div>

              <div className="flex flex-col gap-4">
                <Card className="gap-0 py-0">
                  <div className="border-b px-4 py-3">
                    <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
                      {t("run.section.details")}
                    </h2>
                  </div>
                  <div className="flex flex-col gap-2.5 px-4 py-3">
                    <DetailRow label={t("run.field.created")} value={formatWhen(run.createdAt)} />
                    <DetailRow
                      label={t("run.field.dispatched")}
                      value={run.dispatchedAt ? formatWhen(run.dispatchedAt) : null}
                    />
                    <DetailRow
                      label={t("run.field.approved")}
                      value={run.approvedAt ? formatWhen(run.approvedAt) : null}
                    />
                    <DetailRow label={t("runs.table.automation")} value={triggerLabel} mono />
                    <DetailRow
                      label={t("runs.table.cost")}
                      value={
                        formatCost(run.costUsd) ?? (
                          <span className="text-muted-foreground">{t("runs.cost.unknown")}</span>
                        )
                      }
                      mono
                    />
                    <DetailRow
                      label={t("run.field.tokens")}
                      value={
                        run.inputTokens === null
                          ? null
                          : `${run.inputTokens.toLocaleString("en")} ${t("run.transcript.in")} / ${(run.outputTokens ?? 0).toLocaleString("en")} ${t("run.transcript.out")}`
                      }
                      mono
                    />
                    <DetailRow
                      label={t("run.field.output")}
                      value={
                        run.outputLink ? (
                          <a
                            className="font-medium text-primary underline-offset-4 hover:underline"
                            href={run.outputLink}
                            target="_blank"
                            rel="noreferrer"
                          >
                            {t("runs.table.openOutput")}
                          </a>
                        ) : null
                      }
                    />
                  </div>
                </Card>

                {/* Mock 3c (#211): where it executed, between Details and Changes. Both kinds
                    read the same page — a local run names its folder and branch where a pod run
                    carries the job's fresh clone and a PR. */}
                <Card className="gap-0 py-0">
                  <div className="border-b px-4 py-3">
                    <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
                      {t("run.section.execution")}
                    </h2>
                  </div>
                  <div className="flex flex-col gap-2.5 px-4 py-3">
                    <DetailRow
                      label={t("run.field.runtimeKind")}
                      value={
                        run.locus === "Local"
                          ? t("run.execution.localProcess")
                          : t("run.execution.containerJob")
                      }
                    />
                    {run.locus === "Local" ? (
                      <DetailRow
                        label={t("run.field.workingFolder")}
                        value={run.workingFolder}
                        mono
                      />
                    ) : null}
                    <DetailRow label={t("run.field.branchCreated")} value={run.branchName} mono />
                    <DetailRow
                      label={t("run.field.output")}
                      value={
                        run.locus === "Local"
                          ? t("run.execution.localOutput")
                          : run.outputLink
                            ? t("run.execution.podOutput")
                            : null
                      }
                    />
                  </div>
                </Card>

                {/* Turn 7 D1: the rail carries only the change's summary, anchoring to the
                    block in the body — one diff on the page, one summary pointing at it. */}
                <RunChangesSummary projectId={projectId} runId={runId} />
              </div>
            </div>
          </>
        )}
      </div>
    </AppShell>
  );
}

/**
 * The rail's CHANGES card, summary-only (turn 7 D1): number, file count and ± totals, anchoring
 * to the block in the body. Shares the body's query key, so no second read happens.
 */
function RunChangesSummary({ projectId, runId }: { projectId: string; runId: string }) {
  const changes = useRunChanges(projectId, runId);
  const change = changes.data?.change ?? null;
  if (!change) return null;

  const additions = change.files.reduce((sum, file) => sum + file.additions, 0);
  const deletions = change.files.reduce((sum, file) => sum + file.deletions, 0);

  return (
    <Card className="gap-0 py-0">
      <div className="border-b px-4 py-3">
        <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
          {t("run.section.changes")}
        </h2>
      </div>
      <div className="px-4 py-3">
        <a
          href="#run-changes"
          className="text-xs font-medium text-primary underline-offset-4 hover:underline"
        >
          <span className="font-mono">#{change.number}</span> ·{" "}
          {tCount(change.files.length, "run.changes.file.one", "run.changes.file.other")} ·{" "}
          <span className="font-mono text-success">+{additions}</span>{" "}
          <span className="font-mono text-destructive">−{deletions}</span> ↓
        </a>
      </div>
    </Card>
  );
}

/**
 * The closed cause→remedy map (turn 7, design D2). Matched against the executor's own stable
 * sentences — see RunExecutor's refusals: "Credential could not be resolved: …" and
 * "The prompt at '…' has no body…". A changed message degrades to a banner without a link,
 * never a wrong link.
 */
function remedyFor(reason: string | null): "settings" | "automations" | null {
  if (!reason) return null;
  if (reason.includes("Credential could not be resolved")) return "settings";
  if (reason.includes("The prompt at ")) return "automations";
  return null;
}

/** One rail row: muted label left, value right, the design system's em dash for absence. */
function DetailRow({ label, value, mono }: { label: string; value: ReactNode; mono?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3 text-xs">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className={cn("min-w-0 text-right break-words", mono && "font-mono text-[11px]")}>
        {value ?? <span className="text-muted-foreground">—</span>}
      </span>
    </div>
  );
}

/**
 * The lifecycle as stations rather than a bare string: Queued → Planning → (Awaiting approval) →
 * Executing → Done. The gate station only renders when this Run actually carries one — a Plan,
 * an approval, or the wait itself; a Run whose Automation skips approval shows four stations.
 * Terminal states colour the last station: Succeeded in success, Failed in destructive,
 * Cancelled hollow.
 */
function RunStepper({ run }: { run: RunView }) {
  const gatePresent =
    run.state === "AwaitingApproval" || run.plan !== null || run.approvedAt !== null;

  const terminalLabel: TranslationKey =
    run.state === "Failed"
      ? "run.state.failed"
      : run.state === "Cancelled"
        ? "run.state.cancelled"
        : "run.step.done";

  const labels: TranslationKey[] = [
    "run.state.queued",
    "run.state.planning",
    ...(gatePresent ? (["run.state.awaitingApproval"] as TranslationKey[]) : []),
    "run.state.executing",
    terminalLabel,
  ];

  const terminal = ["Succeeded", "Failed", "Cancelled"].includes(run.state);
  const current = (() => {
    switch (run.state) {
      case "Queued":
        return 0;
      case "Planning":
        return 1;
      case "AwaitingApproval":
        return 2;
      case "Executing":
      case "AwaitingInput":
        // AwaitingInput is a pause inside execution, not a station of its own; the state pill
        // above the stepper carries its name.
        return gatePresent ? 3 : 2;
      default:
        return labels.length - 1;
    }
  })();

  function dot(index: number) {
    if (index < current || (index === current && run.state === "Succeeded")) {
      return (
        <span className="flex size-4.5 items-center justify-center rounded-full bg-success">
          <Check className="size-2.5 text-success-foreground" aria-hidden="true" />
        </span>
      );
    }
    if (index === current && run.state === "Failed") {
      return (
        <span className="flex size-4.5 items-center justify-center rounded-full bg-destructive">
          <X className="size-2.5 text-destructive-foreground" aria-hidden="true" />
        </span>
      );
    }
    if (index === current && run.state === "Cancelled") {
      return <span className="size-4.5 rounded-full border-2 border-input bg-muted" />;
    }
    if (index === current) {
      return <span className="size-4.5 rounded-full bg-warning ring-4 ring-warning/25" />;
    }
    return <span className="size-4.5 rounded-full border-2 border-input bg-background" />;
  }

  function segment(index: number) {
    if (index < current) return "bg-success";
    if (index > current) return "bg-border";
    if (run.state === "Succeeded") return "bg-success";
    if (run.state === "Failed") return "bg-destructive";
    if (run.state === "Cancelled") return "bg-border";
    return "bg-warning";
  }

  return (
    <>
      <ol aria-label={t("run.stepper")} className="hidden flex-wrap items-center gap-2 md:flex">
        {labels.map((label, index) => (
          <li
            key={label}
            className="flex items-center gap-2"
            aria-current={index === current && !terminal ? "step" : undefined}
          >
            {index > 0 ? (
              <span
                className={cn(
                  "h-0.5 w-10 rounded-full",
                  index <= current ? "bg-success" : "bg-border",
                )}
                aria-hidden="true"
              />
            ) : null}
            {dot(index)}
            <span
              className={cn(
                "text-xs",
                index === current ? "font-semibold" : "text-muted-foreground",
              )}
            >
              {t(label)}
            </span>
          </li>
        ))}
      </ol>
      {/* The phone's stepper: the same stations as flex-1 progress segments; the state pill
          above carries the current name, so the strip needs no labels of its own. */}
      <div className="flex items-center gap-1 md:hidden" aria-hidden="true">
        {labels.map((label, index) => (
          <span key={label} className={cn("h-1 flex-1 rounded-full", segment(index))} />
        ))}
      </div>
    </>
  );
}

/** Relative for recency, absolute past a day — the content fundamentals' rule. */
function formatWhen(iso: string): string {
  const then = new Date(iso);
  const minutes = Math.round((Date.now() - then.getTime()) / 60000);

  if (minutes < 1) return new Intl.RelativeTimeFormat("en").format(0, "minute");
  if (minutes < 60) return new Intl.RelativeTimeFormat("en").format(-minutes, "minute");
  if (minutes < 60 * 24) {
    return new Intl.RelativeTimeFormat("en").format(-Math.round(minutes / 60), "hour");
  }
  return then.toLocaleDateString("en", { dateStyle: "medium" });
}
