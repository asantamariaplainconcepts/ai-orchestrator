import { useAutomations } from "@/features/automations/useAutomations";
import { t, tCount } from "@/shared/i18n";
import { useRuns } from "./useRuns";
import type { RunView } from "./types";

/**
 * UC-021 — the loop's output, observable. Automation columns are a client-side join with the
 * automations query (design D1): the Run records the id, current configuration supplies the
 * details, and a Run whose Automation is gone shows empty cells rather than a guess. Output,
 * logs and cost have no producing feature yet and render the empty value (design D2).
 */
export function RunsSection({
  projectId,
  storyFilter,
  onClearFilter,
}: {
  projectId: string;
  storyFilter: string | null;
  onClearFilter: () => void;
}) {
  const runs = useRuns(projectId, storyFilter);
  const automations = useAutomations(projectId);

  const rows = runs.data ?? [];
  const byId = new Map((automations.data ?? []).map((automation) => [automation.id, automation]));

  return (
    <section className="card" id="runs">
      <div className="card-header">
        <div className="row">
          <h2>{t("runs.heading")}</h2>
          <span className="badge badge-neutral">
            {tCount(rows.length, "runs.count.one", "runs.count.other")}
          </span>
          {storyFilter ? (
            <span className="pill pill-neutral">
              {t("runs.filteredByStory")} <span className="mono">#{storyFilter}</span>
            </span>
          ) : null}
        </div>
        {storyFilter ? (
          <button className="btn" type="button" onClick={onClearFilter}>
            {t("runs.clearFilter")}
          </button>
        ) : null}
      </div>

      {runs.isPending && <p className="state">{t("runs.loading")}</p>}
      {runs.isError && (
        <p className="state state-error" role="alert">
          {t("runs.error")}
        </p>
      )}

      {runs.data && rows.length === 0 && (
        <p className="state">{storyFilter ? t("runs.emptyForStory") : t("runs.empty")}</p>
      )}

      {rows.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th className="table-num">{t("runs.table.story")}</th>
              <th>{t("runs.table.automation")}</th>
              <th>{t("runs.table.state")}</th>
              <th>{t("runs.table.created")}</th>
              <th>{t("runs.table.dispatched")}</th>
              <th>{t("runs.table.output")}</th>
              <th>{t("runs.table.cost")}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((run) => {
              const automation = byId.get(run.automationId);
              return (
                <tr key={run.id}>
                  <td className="table-num mono">{run.vendorStoryId}</td>
                  <td>
                    {automation ? (
                      <span className="row">
                        <span className="pill pill-neutral">{automation.triggerLabel}</span>
                        <span className="card-hint">
                          {automation.action} · {automation.runtime}
                        </span>
                      </span>
                    ) : (
                      <span className="empty-value">—</span>
                    )}
                  </td>
                  <td>
                    <StatePill state={run.state} />
                  </td>
                  <td>{formatWhen(run.createdAt)}</td>
                  <td>
                    {run.dispatchedAt ? (
                      formatWhen(run.dispatchedAt)
                    ) : (
                      <span className="empty-value">—</span>
                    )}
                  </td>
                  {/* No producer yet (#19 output, #25 cost) — absent data shown as absent. */}
                  <td>
                    <span className="empty-value">—</span>
                  </td>
                  <td>
                    <span className="empty-value">—</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}

function StatePill({ state }: { state: RunView["state"] }) {
  const className =
    state === "Executing" || state === "Planning"
      ? "pill pill-ok"
      : state === "AwaitingApproval"
        ? "pill pill-warn"
        : "pill pill-neutral";
  return <span className={className}>{state}</span>;
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
