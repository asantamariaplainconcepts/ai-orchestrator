import { Link, useParams } from "react-router";
import { renderStoryMarkdown } from "@/features/backlog/markdown";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { RunChanges } from "./RunChanges";
import {
  formatCost,
  useCancelRun,
  useDecideOnPlan,
  useDismissFailure,
  useRunLog,
  useRuns,
} from "./useRuns";
import { useRunNow } from "./useRunNow";

/**
 * UC-013's review surface — the page the use case always assumed and #20 did not build. The
 * Plan is model output rendered in a browser, so it goes through the same sanitiser as a
 * Story's description and its documents (approval-gate D6).
 */
export function RunScreen() {
  const { projectId = "", runId = "" } = useParams();
  // The list is already the read model for Runs; one more endpoint for one row would be a
  // second source of the same truth.
  const runs = useRuns(projectId, null);
  const decide = useDecideOnPlan(projectId);
  const cancel = useCancelRun(projectId);
  // #145 — both decisions a failure can carry, where the failure is. Run again goes through the
  // Run-now path (design D1), so BR-001, BR-002 and the approval gate apply without this screen
  // knowing they exist.
  const runAgain = useRunNow(projectId);
  const dismiss = useDismissFailure(projectId);

  const run = runs.data?.find((candidate) => candidate.id === runId);
  const log = useRunLog(projectId, runId);
  const awaiting = run?.state === "AwaitingApproval";

  // Only an unfinished Run can be cancelled; the API refuses the rest, so the control follows.
  const cancellable =
    run !== undefined &&
    ["Queued", "Planning", "AwaitingApproval", "Executing"].includes(run.state);

  return (
    <AppShell
      crumbs={[
        { label: t("shell.crumb.projects"), to: "/projects" },
        { label: t("run.crumb.project"), to: `/projects/${projectId}` },
        { label: t("run.title.fallback") },
      ]}
      title={run ? `${t("run.title.fallback")} · #${run.vendorStoryId}` : t("run.title.fallback")}
    >
      <div className="stack">
        <section className="card">
          <div className="card-header">
            <div className="row">
              <h2>{t("run.title.fallback")}</h2>
              {run ? <span className="pill pill-neutral">{run.state}</span> : null}
            </div>
            <div className="row">
              {cancellable ? (
                <button
                  className="btn"
                  type="button"
                  disabled={cancel.isPending}
                  onClick={() => cancel.mutate(runId)}
                >
                  {cancel.isPending ? t("run.cancelling") : t("run.cancel")}
                </button>
              ) : null}
              {run?.state === "Failed" ? (
                <>
                  <button
                    className="btn btn-primary"
                    type="button"
                    disabled={runAgain.isPending}
                    onClick={() =>
                      runAgain.mutate({
                        vendorStoryId: run.vendorStoryId,
                        automationId: run.automationId,
                      })
                    }
                  >
                    {runAgain.isPending ? t("run.again.pending") : t("run.again")}
                  </button>
                  {run.dismissedAt ? (
                    <span className="pill pill-neutral">
                      {t("run.dismissed")} · {formatWhen(run.dismissedAt)}
                    </span>
                  ) : (
                    <button
                      className="btn"
                      type="button"
                      disabled={dismiss.isPending}
                      title={t("run.dismiss.hint")}
                      onClick={() => dismiss.mutate(runId)}
                    >
                      {dismiss.isPending ? t("run.dismiss.pending") : t("run.dismiss")}
                    </button>
                  )}
                </>
              ) : null}
              {run ? (
                <Link className="btn" to={`/projects/${projectId}/stories/${run.vendorStoryId}`}>
                  {t("run.field.story")} #{run.vendorStoryId}
                </Link>
              ) : null}
            </div>
          </div>

          {runs.isPending && <p className="state">{t("run.loading")}</p>}
          {runs.isError && (
            <p className="state state-error" role="alert">
              {t("run.error")}
            </p>
          )}
          {runs.data && !run && <p className="state">{t("run.notFound")}</p>}

          {/* The API's own reason: a re-run refused by BR-001 must say so in Run now's voice. */}
          {runAgain.isError && (
            <p className="state state-error" role="alert">
              {(runAgain.error instanceof ApiError && runAgain.error.detail) ||
                t("run.again.failed")}
            </p>
          )}

          {dismiss.isError && (
            <p className="state state-error" role="alert">
              {(dismiss.error instanceof ApiError && dismiss.error.detail) ||
                t("run.dismiss.failed")}
            </p>
          )}

          {cancel.isError && (
            <p className="state state-error" role="alert">
              {t("run.cancelFailed")}
            </p>
          )}

          {run && (
            <table className="table">
              <tbody>
                <Field label={t("run.field.created")} value={formatWhen(run.createdAt)} />
                <Field
                  label={t("run.field.dispatched")}
                  value={run.dispatchedAt ? formatWhen(run.dispatchedAt) : null}
                />
                <Field
                  label={t("run.field.approved")}
                  value={run.approvedAt ? formatWhen(run.approvedAt) : null}
                />
                <Field
                  label={t("run.field.output")}
                  value={
                    run.outputLink ? (
                      <a href={run.outputLink} target="_blank" rel="noreferrer">
                        {t("runs.table.openOutput")}
                      </a>
                    ) : null
                  }
                />
                <Field
                  label={t("runs.table.cost")}
                  value={
                    formatCost(run.costUsd) ?? (
                      <span className="empty-value">{t("runs.cost.unknown")}</span>
                    )
                  }
                />
                <Field
                  label={t("run.field.tokens")}
                  value={
                    run.inputTokens === null
                      ? null
                      : `${run.inputTokens.toLocaleString("en")} in / ${(run.outputTokens ?? 0).toLocaleString("en")} out`
                  }
                />
                <Field label={t("run.field.failure")} value={run.failureReason} />
              </tbody>
            </table>
          )}
        </section>

        {run && (
          <section className="card">
            <div className="card-header">
              <div className="row">
                <h2>{t("run.section.plan")}</h2>
                {awaiting ? <span className="pill pill-warn">{t("run.plan.waiting")}</span> : null}
              </div>
              {awaiting ? (
                <div className="row">
                  <button
                    className="btn btn-primary"
                    type="button"
                    disabled={decide.isPending}
                    onClick={() => decide.mutate({ runId, approve: true })}
                  >
                    {decide.isPending ? t("run.deciding") : t("run.approve")}
                  </button>
                  <button
                    className="btn"
                    type="button"
                    disabled={decide.isPending}
                    onClick={() => decide.mutate({ runId, approve: false })}
                  >
                    {t("run.reject")}
                  </button>
                </div>
              ) : null}
            </div>

            {decide.isError && (
              <p className="state state-error" role="alert">
                {t("run.decideFailed")}
              </p>
            )}

            {run.plan ? (
              <div
                className="prose"
                // Sanitised — a Plan is model output, as untrusted as any other text we did
                // not write (approval-gate D6).
                dangerouslySetInnerHTML={{ __html: renderStoryMarkdown(run.plan) }}
              />
            ) : (
              <p className="state">{t("run.plan.none")}</p>
            )}
          </section>
        )}

        {run && (
          <section className="card">
            <div className="card-header">
              <div className="row">
                <h2>{t("run.section.log")}</h2>
                {/* Live while it runs (UC-027): the poll stops itself on terminal (D3). */}
                {log.data && !log.data.complete ? (
                  <span className="pill pill-neutral">{t("run.log.live")}</span>
                ) : null}
              </div>
            </div>
            {log.isError && (
              <p className="state state-error" role="alert">
                {t("run.log.error")}
              </p>
            )}
            {log.data &&
              (log.data.content.length > 0 ? (
                <pre className="mono log-view">{log.data.content}</pre>
              ) : (
                <p className="state">
                  {log.data.complete ? t("run.log.none") : t("run.log.waitingForOutput")}
                </p>
              ))}
          </section>
        )}

        {run && <RunChanges projectId={projectId} runId={runId} />}
      </div>
    </AppShell>
  );
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <tr>
      <th>{label}</th>
      <td>{value ?? <span className="empty-value">—</span>}</td>
    </tr>
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
