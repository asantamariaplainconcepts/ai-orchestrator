import { useState } from "react";
import { t, tCount } from "@/shared/i18n";
import { AUTOMATION_ACTIONS, AGENT_RUNTIMES, EXECUTABLE_ACTIONS } from "./types";
import type { AgentRuntime, AutomationAction } from "./types";
import {
  useApplyAutomationDefaults,
  useAutomations,
  useCreateAutomation,
  useDeleteAutomation,
  useSetAutomationEnabled,
} from "./useAutomations";

/**
 * UC-005 on the project page. No styles are declared here; the one control the kit lacked
 * (a checkbox) was added to the kit and regenerated rather than styled inline.
 */
export function AutomationsSection({ projectId }: { projectId: string }) {
  const automations = useAutomations(projectId);
  // Enabling can be refused by BR-003's re-check; disabling never is (design D2).
  const setEnabled = useSetAutomationEnabled(projectId);
  const create = useCreateAutomation(projectId);
  const defaults = useApplyAutomationDefaults(projectId);
  const remove = useDeleteAutomation(projectId);

  const [triggerLabel, setTriggerLabel] = useState("");
  const [triggerState, setTriggerState] = useState("");
  const [action, setAction] = useState<AutomationAction>("ImplementToPullRequest");
  const [runtime, setRuntime] = useState<AgentRuntime>("ClaudeCodeHeadless");
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [rubricPath, setRubricPath] = useState("");
  const [readyLabel, setReadyLabel] = useState("");

  // Only the grill converses with a rubric; the fields would be noise on every other action.
  const isGrill = action === "GrillToReady";

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!triggerLabel.trim()) return;

    create.mutate(
      {
        triggerLabel: triggerLabel.trim(),
        // Empty means "any state" — an unconstrained trigger, not an empty string to match.
        triggerState: triggerState.trim() === "" ? null : triggerState.trim(),
        action,
        runtime,
        requiresApproval,
        timeoutMinutes: null,
        rubricPath: isGrill && rubricPath.trim() ? rubricPath.trim() : null,
        readyLabel: isGrill && readyLabel.trim() ? readyLabel.trim() : null,
      },
      { onSuccess: () => setTriggerLabel("") },
    );
  }

  const rows = automations.data ?? [];

  return (
    <section className="card">
      <div className="card-header">
        <div className="row">
          <h2>{t("automations.heading")}</h2>
          {automations.data ? (
            <span className="badge badge-neutral">
              {tCount(rows.length, "automations.count.one", "automations.count.other")}
            </span>
          ) : null}
        </div>
        <button
          className="btn"
          type="button"
          onClick={() => defaults.mutate()}
          disabled={defaults.isPending}
          title={t("automations.defaults.hint")}
        >
          {defaults.isPending ? t("automations.defaults.applying") : t("automations.defaults")}
        </button>
      </div>

      {/* Partial success is the normal outcome, so the result is reported rather than reduced
          to success or failure (design D2). */}
      {/* The refusal carries the rule, so it gets its own line rather than a generic error. */}
      {remove.isError && (
        <p className="state state-error" role="alert">
          {t("automations.delete.refused")}
        </p>
      )}
      {defaults.isError && (
        <p className="state state-error" role="alert">
          {t("automations.defaults.failed")}
        </p>
      )}
      {defaults.data ? (
        <p className="card-hint">
          {defaults.data.created.length > 0
            ? `${defaults.data.created.length} ${t("automations.defaults.created")}`
            : t("automations.defaults.nothingNew")}
          {defaults.data.skipped.length > 0
            ? ` · ${defaults.data.skipped.length} ${t("automations.defaults.skipped")}`
            : ""}
          {/* A label that never reached the vendor is not selectable there, which is the whole
              point of the action — so it is said, not implied. */}
          {defaults.data.labelNote ? ` · ${t("automations.defaults.labels")}` : ""}
        </p>
      ) : null}

      <form className="stack" onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label className="label" htmlFor="trigger-label">
              {t("automations.trigger")}
            </label>
            <input
              id="trigger-label"
              className="input"
              value={triggerLabel}
              onChange={(event) => setTriggerLabel(event.target.value)}
              placeholder={t("automations.triggerPlaceholder")}
            />
          </div>
          <div className="field">
            <label className="label" htmlFor="trigger-state">
              {t("automations.state")}
            </label>
            <input
              id="trigger-state"
              className="input"
              value={triggerState}
              onChange={(event) => setTriggerState(event.target.value)}
              placeholder={t("automations.statePlaceholder")}
            />
          </div>
          <div className="field">
            <label className="label" htmlFor="action">
              {t("automations.action")}
            </label>
            <select
              id="action"
              className="input"
              value={action}
              onChange={(event) => setAction(event.target.value as AutomationAction)}
            >
              {AUTOMATION_ACTIONS.map((candidate) => (
                <option key={candidate} value={candidate}>
                  {candidate}
                  {EXECUTABLE_ACTIONS.includes(candidate)
                    ? ""
                    : ` — ${t("automations.actionNotExecutable")}`}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label className="label" htmlFor="runtime">
              {t("automations.runtime")}
            </label>
            <select
              id="runtime"
              className="input"
              value={runtime}
              onChange={(event) => setRuntime(event.target.value as AgentRuntime)}
            >
              {AGENT_RUNTIMES.map((candidate) => (
                <option key={candidate} value={candidate}>
                  {candidate}
                </option>
              ))}
            </select>
          </div>
          {isGrill ? (
            <>
              <div className="field">
                <label className="label" htmlFor="rubric-path">
                  {t("automations.rubricPath")}
                </label>
                <input
                  id="rubric-path"
                  className="input"
                  value={rubricPath}
                  onChange={(event) => setRubricPath(event.target.value)}
                  placeholder={t("automations.rubricPathPlaceholder")}
                />
              </div>
              <div className="field">
                <label className="label" htmlFor="ready-label">
                  {t("automations.readyLabel")}
                </label>
                <input
                  id="ready-label"
                  className="input"
                  value={readyLabel}
                  onChange={(event) => setReadyLabel(event.target.value)}
                  placeholder={t("automations.readyLabelPlaceholder")}
                />
              </div>
            </>
          ) : null}
          <div className="field-inline">
            <input
              id="requires-approval"
              className="checkbox"
              type="checkbox"
              checked={requiresApproval}
              onChange={(event) => setRequiresApproval(event.target.checked)}
            />
            <label className="label" htmlFor="requires-approval">
              {t("automations.approval")}
            </label>
          </div>
          <button className="btn btn-primary" type="submit" disabled={create.isPending}>
            {create.isPending ? t("automations.adding") : t("automations.add")}
          </button>
        </div>

        <p className="card-hint">{t("automations.catalogueHint")}</p>

        {create.isError && (
          <p className="state state-error" role="alert">
            {t("automations.saveFailed")}

            {setEnabled.isError && (
              <p className="state state-error" role="alert">
                {t("automations.enableFailed")}
              </p>
            )}
          </p>
        )}
      </form>

      {automations.isPending && <p className="state">{t("automations.loading")}</p>}
      {automations.isError && (
        <p className="state state-error" role="alert">
          {t("automations.error")}
        </p>
      )}
      {automations.data && rows.length === 0 && <p className="state">{t("automations.empty")}</p>}

      {rows.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>{t("automations.table.trigger")}</th>
              <th>{t("automations.table.action")}</th>
              <th>{t("automations.table.approval")}</th>
              <th className="table-num">{t("automations.table.timeout")}</th>
              <th>{t("automations.table.status")}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((automation) => (
              <tr key={automation.id}>
                <td>
                  <span className="row">
                    <span className="pill pill-neutral">{automation.triggerLabel}</span>
                    {automation.triggerState ? (
                      <span className="pill pill-info">{automation.triggerState}</span>
                    ) : (
                      <span className="empty-value">{t("automations.anyState")}</span>
                    )}
                  </span>
                </td>
                <td>
                  <span className="row">
                    <span className="list-title">{automation.action}</span>
                    {EXECUTABLE_ACTIONS.includes(automation.action) ? null : (
                      <span className="pill pill-warn">{t("automations.actionNotExecutable")}</span>
                    )}
                  </span>
                </td>
                <td>
                  <span
                    className={automation.requiresApproval ? "pill pill-info" : "pill pill-neutral"}
                  >
                    {automation.requiresApproval
                      ? t("automations.approvalRequired")
                      : t("automations.approvalNone")}
                  </span>
                </td>
                <td className="table-num">
                  {automation.timeoutMinutes} {t("automations.minutes")}
                </td>
                <td>
                  <span className="row">
                    {automation.enabled ? null : (
                      <span className="pill pill-neutral">{t("automations.disabled")}</span>
                    )}
                    <button
                      className="btn"
                      type="button"
                      disabled={setEnabled.isPending}
                      onClick={() =>
                        setEnabled.mutate({
                          id: automation.id,
                          enabled: !automation.enabled,
                        })
                      }
                    >
                      {automation.enabled ? t("automations.disable") : t("automations.enable")}
                    </button>
                    <button
                      className="btn"
                      type="button"
                      disabled={remove.isPending}
                      onClick={() => remove.mutate(automation.id)}
                      title={t("automations.delete.hint")}
                    >
                      {t("automations.delete")}
                    </button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
