import { Link } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { formatCost, usePulse, useRuns } from "./useRuns";

/**
 * #108 — the Operate strip: the project's 7-day pulse, every figure linking to the list it
 * summarises (design D2 — a metric that cannot be audited is decoration). Kit vocabulary only
 * (design D3): this page is still a kit screen; #109 migrates it whole.
 */
export function OperateStrip({ projectId }: { projectId: string }) {
  const pulse = usePulse(projectId);
  const runs = useRuns(projectId, null);

  if (!pulse.data) return null;
  const data = pulse.data;

  // Live work comes from the runs list the page already polls — the pulse stays a summary.
  const executing = (runs.data ?? []).filter(
    (run) => run.state === "Executing" || run.state === "Planning",
  );
  const waitingTotal = data.waiting.approval + data.waiting.input + data.waiting.failure;

  return (
    <section className="card" aria-label={t("pulse.heading")}>
      <div className="card-header">
        <div className="row">
          <h2>{t("pulse.heading")}</h2>
          <span className="badge badge-neutral">{t("pulse.window")}</span>
        </div>
      </div>

      <div className="stack">
        {(waitingTotal > 0 || executing.length > 0) && (
          <div className="row">
            {data.waiting.approval > 0 && (
              <Link className="pill pill-warn" to="/inbox">
                {data.waiting.approval} {t("pulse.waiting.approval")}
              </Link>
            )}
            {data.waiting.input > 0 && (
              <Link className="pill pill-warn" to="/inbox">
                {data.waiting.input} {t("pulse.waiting.input")}
                {data.oldestOpenQuestionSeconds !== null
                  ? ` · ${formatAge(data.oldestOpenQuestionSeconds)}`
                  : ""}
              </Link>
            )}
            {data.waiting.failure > 0 && (
              <Link className="pill pill-danger" to="/inbox">
                {data.waiting.failure} {t("pulse.waiting.failure")}
              </Link>
            )}
            {executing.map((run) => (
              <Link
                className="pill pill-info"
                key={run.id}
                to={`/projects/${projectId}/runs/${run.id}`}
              >
                {t("pulse.executing")} · {run.vendorStoryId}
              </Link>
            ))}
          </div>
        )}

        <div className="row">
          <a className="stat-card stat-card-brand" href="#runs-section">
            <span className="stat-card-label">{t("pulse.runs")}</span>
            <span className="stat-card-value">{data.runsStarted}</span>
            <span className="card-hint">
              {data.storiesNeverRun > 0
                ? tCount(data.storiesNeverRun, "pulse.neverRun.one", "pulse.neverRun.other")
                : t("pulse.coverage.full")}
            </span>
          </a>
          <a className="stat-card stat-card-ok" href="#runs-section">
            <span className="stat-card-label">{t("pulse.successRate")}</span>
            <span className="stat-card-value">
              {data.successRate === null ? "—" : `${Math.round(data.successRate * 100)}%`}
            </span>
            <span className="card-hint">
              {tCount(data.terminalRuns, "pulse.terminal.one", "pulse.terminal.other")}
            </span>
          </a>
          <a className="stat-card" href="#runs-section">
            <span className="stat-card-label">{t("pulse.cost")}</span>
            <span className="stat-card-value">{formatCost(data.knownCostUsd) ?? "—"}</span>
            {/* Never a bare total (BR-011): unknown is stated, not folded in as zero. */}
            <span className="card-hint">
              {data.unknownCostRuns > 0
                ? `${data.unknownCostRuns} ${t("runs.cost.excluded")}`
                : `${data.reportedRuns} ${t("runs.cost.reported")}`}
            </span>
          </a>
          <a className="stat-card stat-card-info" href="#runs-section">
            <span className="stat-card-label">{t("pulse.timing")}</span>
            <span className="stat-card-value-text">
              {data.meanQueueWaitSeconds === null
                ? "—"
                : `${formatAge(data.meanQueueWaitSeconds)} · ${
                    data.meanDurationSeconds === null ? "—" : formatAge(data.meanDurationSeconds)
                  }`}
            </span>
            <span className="card-hint">{t("pulse.timing.hint")}</span>
          </a>
        </div>

        {data.automations.length > 0 && (
          <ul className="list">
            {data.automations.map((automation) => (
              <li className="list-row" key={automation.automationId}>
                <span className="row">
                  <span className="badge badge-neutral">{automation.triggerLabel}</span>
                  <span className="card-hint">{automation.action}</span>
                </span>
                <span className="row">
                  {automation.fired === 0 ? (
                    <a className="pill pill-neutral" href="#automations-section">
                      {t("pulse.unused")}
                    </a>
                  ) : (
                    <>
                      <a className="pill pill-ok" href="#runs-section">
                        {automation.fired} {t("pulse.fired")}
                      </a>
                      {automation.failed > 0 && (
                        <a className="pill pill-danger" href="#runs-section">
                          {automation.failed} {t("pulse.failed")}
                        </a>
                      )}
                    </>
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

/** Seconds into the roughest honest unit — a pulse reads at a glance or not at all. */
function formatAge(seconds: number): string {
  if (seconds < 90) return `${Math.round(seconds)}s`;
  const minutes = seconds / 60;
  if (minutes < 90) return `${Math.round(minutes)}m`;
  const hours = minutes / 60;
  if (hours < 36) return `${Math.round(hours * 10) / 10}h`;
  return `${Math.round(hours / 24)}d`;
}
