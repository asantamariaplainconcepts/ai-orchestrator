import { Link } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Card, CardContent } from "@/shared/ui/card";
import { formatCost, usePulse, useRuns } from "./useRuns";

/**
 * The Operate strip: the project's 7-day pulse, every figure linking to the list it summarises
 * (project-pulse design D2 — a metric that cannot be audited is decoration). On the Platform
 * theme since dashboard-tabs migrated the page whole, the second restyle its design bought.
 */
export function OperateStrip({
  projectId,
  onShowRuns,
}: {
  projectId: string;
  onShowRuns: () => void;
}) {
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
    <div className="flex flex-col gap-4">
      {(waitingTotal > 0 || executing.length > 0) && (
        <div className="flex flex-wrap items-center gap-2">
          {data.waiting.approval > 0 && (
            <Link to="/inbox">
              <Badge className="bg-warning text-warning-foreground">
                {data.waiting.approval} {t("pulse.waiting.approval")}
              </Badge>
            </Link>
          )}
          {data.waiting.input > 0 && (
            <Link to="/inbox">
              <Badge className="bg-warning text-warning-foreground">
                {data.waiting.input} {t("pulse.waiting.input")}
                {data.oldestOpenQuestionSeconds !== null
                  ? ` · ${formatAge(data.oldestOpenQuestionSeconds)}`
                  : ""}
              </Badge>
            </Link>
          )}
          {data.waiting.failure > 0 && (
            <Link to="/inbox">
              <Badge variant="destructive">
                {data.waiting.failure} {t("pulse.waiting.failure")}
              </Badge>
            </Link>
          )}
          {executing.map((run) => (
            <Link key={run.id} to={`/projects/${projectId}/runs/${run.id}`}>
              <Badge className="bg-info text-info-foreground">
                {t("pulse.executing")} · {run.vendorStoryId}
              </Badge>
            </Link>
          ))}
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <MetricCard
          label={t("pulse.runs")}
          value={String(data.runsStarted)}
          hint={
            data.storiesNeverRun > 0
              ? tCount(data.storiesNeverRun, "pulse.neverRun.one", "pulse.neverRun.other")
              : t("pulse.coverage.full")
          }
          onClick={onShowRuns}
        />
        <MetricCard
          label={t("pulse.successRate")}
          value={data.successRate === null ? "—" : `${Math.round(data.successRate * 100)}%`}
          hint={tCount(data.terminalRuns, "pulse.terminal.one", "pulse.terminal.other")}
          onClick={onShowRuns}
        />
        <MetricCard
          label={t("pulse.cost")}
          value={formatCost(data.knownCostUsd) ?? "—"}
          // Never a bare total (BR-011): unknown is stated, not folded in as zero.
          hint={
            data.unknownCostRuns > 0
              ? `${data.unknownCostRuns} ${t("runs.cost.excluded")}`
              : `${data.reportedRuns} ${t("runs.cost.reported")}`
          }
          onClick={onShowRuns}
        />
        <MetricCard
          label={t("pulse.timing")}
          value={
            data.meanQueueWaitSeconds === null
              ? "—"
              : `${formatAge(data.meanQueueWaitSeconds)} · ${
                  data.meanDurationSeconds === null ? "—" : formatAge(data.meanDurationSeconds)
                }`
          }
          hint={t("pulse.timing.hint")}
          onClick={onShowRuns}
        />
      </div>

      {data.automations.length > 0 && (
        <Card>
          <CardContent>
            <ul className="divide-y">
              {data.automations.map((automation) => (
                <li
                  className="flex flex-wrap items-center justify-between gap-2 py-2 first:pt-0 last:pb-0"
                  key={automation.automationId}
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <Badge variant="secondary">{automation.triggerLabel}</Badge>
                    <span className="truncate text-xs text-muted-foreground">
                      {automation.action}
                    </span>
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    {automation.fired === 0 ? (
                      <Badge variant="outline">{t("pulse.unused")}</Badge>
                    ) : (
                      <>
                        <Badge className="bg-success text-success-foreground">
                          {automation.fired} {t("pulse.fired")}
                        </Badge>
                        {automation.failed > 0 && (
                          <Badge variant="destructive">
                            {automation.failed} {t("pulse.failed")}
                          </Badge>
                        )}
                      </>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/** A metric is a button: it goes to the list it summarises, or it is decoration. */
function MetricCard({
  label,
  value,
  hint,
  onClick,
}: {
  label: string;
  value: string;
  hint: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex flex-col gap-0.5 rounded-lg border bg-card p-3 text-left transition-colors hover:border-ring"
    >
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-lg font-bold">{value}</span>
      <span className="text-xs text-muted-foreground">{hint}</span>
    </button>
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
