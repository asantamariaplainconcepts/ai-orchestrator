import { Link, useParams } from "react-router";
import { renderStoryMarkdown } from "@/features/backlog/markdown";
import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { RunChanges } from "./RunChanges";
import { useDecideOnPlan, useRuns } from "./useRuns";

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

  const run = runs.data?.find((candidate) => candidate.id === runId);
  const awaiting = run?.state === "AwaitingApproval";

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
            {run ? (
              <Link className="btn" to={`/projects/${projectId}/stories/${run.vendorStoryId}`}>
                {t("run.field.story")} #{run.vendorStoryId}
              </Link>
            ) : null}
          </div>

          {runs.isPending && <p className="state">{t("run.loading")}</p>}
          {runs.isError && (
            <p className="state state-error" role="alert">
              {t("run.error")}
            </p>
          )}
          {runs.data && !run && <p className="state">{t("run.notFound")}</p>}

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
