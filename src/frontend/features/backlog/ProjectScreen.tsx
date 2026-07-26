import { useState } from "react";
import { useParams } from "react-router";
import { AutomationsSection } from "@/features/automations/AutomationsSection";
import { RunsSection } from "@/features/runs/RunsSection";
import { useProjects } from "@/features/projects/useProjects";
import { t, tCount } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { useBacklog, useConfigureConnector, useRefreshBacklog } from "./useBacklog";
import type { ConnectorView } from "./types";

/**
 * UC-004 + UC-007 on one page: the Connector configuration and the mirrored Stories.
 * Composed entirely from the design-system kit — no styles are declared here.
 */
export function ProjectScreen() {
  const { projectId = "" } = useParams();
  const [runsStoryFilter, setRunsStoryFilter] = useState<string | null>(null);
  const backlog = useBacklog(projectId);
  const refresh = useRefreshBacklog(projectId);
  const projects = useProjects();

  const connector = backlog.data?.connector ?? null;
  const stories = backlog.data?.stories ?? [];

  // The page title is a real fact from the live projects response — never invented. Until the
  // list resolves (or if the id is unknown), the honest fallback is the generic noun.
  const project = projects.data?.find((candidate) => candidate.id === projectId);
  const title = project?.name ?? t("project.title.fallback");

  const openCount = stories.filter((story) => story.state === "open").length;
  const labelledCount = stories.filter((story) => story.labels.length > 0).length;

  return (
    <AppShell
      crumbs={[{ label: t("shell.crumb.projects"), to: "/projects" }, { label: title }]}
      title={title}
    >
      <div className="stack">
        {/* Keyed on the stored Connector: the card mounts before the query settles, so without
            a remount its fields would stay empty once the saved values arrive. */}
        <ConnectorCard
          key={connector ? `${connector.owner}/${connector.repository}` : "unconfigured"}
          projectId={projectId}
          connector={connector}
        />

        {/* Stat cards: every value computable from the current response, nothing else. */}
        {connector && (
          <div className="row">
            <div className="stat-card stat-card-brand">
              <span className="stat-card-label">{t("backlog.stats.total")}</span>
              <span className="stat-card-value">{stories.length}</span>
            </div>
            <div className="stat-card stat-card-info">
              <span className="stat-card-label">{t("backlog.stats.open")}</span>
              <span className="stat-card-value">{openCount}</span>
            </div>
            <div className="stat-card stat-card-warn">
              <span className="stat-card-label">{t("backlog.stats.labelled")}</span>
              <span className="stat-card-value">{labelledCount}</span>
            </div>
            <div
              className={
                connector.lastFailure ? "stat-card stat-card-danger" : "stat-card stat-card-ok"
              }
            >
              <span className="stat-card-label">{t("backlog.stats.connector")}</span>
              <span className="stat-card-value-text">
                {connector.lastFailure ? t("connector.unhealthy") : t("connector.healthy")}
              </span>
            </div>
          </div>
        )}

        <AutomationsSection projectId={projectId} />

        <RunsSection
          projectId={projectId}
          storyFilter={runsStoryFilter}
          onClearFilter={() => setRunsStoryFilter(null)}
        />

        <section className="card">
          <div className="card-header">
            <div className="row">
              <h2>{t("backlog.heading")}</h2>
              {connector ? (
                <span className="badge badge-neutral">
                  {tCount(stories.length, "backlog.count.one", "backlog.count.other")}
                </span>
              ) : null}
            </div>
            <div className="row">
              {connector?.lastSyncedAt ? (
                <span className="card-hint">
                  {t("backlog.syncedAt")} {formatWhen(connector.lastSyncedAt)}
                </span>
              ) : connector ? (
                <span className="card-hint empty-value">{t("backlog.neverSynced")}</span>
              ) : null}
              <button
                className="btn"
                type="button"
                onClick={() => refresh.mutate()}
                disabled={!connector || refresh.isPending}
              >
                {refresh.isPending ? t("backlog.refreshing") : t("backlog.refresh")}
              </button>
            </div>
          </div>

          {backlog.isPending && <p className="state">{t("backlog.loading")}</p>}
          {backlog.isError && (
            <p className="state state-error" role="alert">
              {t("backlog.error")}
            </p>
          )}

          {/* Three distinguishable absences, not one: nothing connected, nothing there, and
              we could not look. Collapsing them is how an outage gets read as an empty
              repository. */}
          {connector?.lastFailure ? (
            <p className="state state-error" role="alert">
              {t("backlog.stale")}
            </p>
          ) : null}

          {backlog.data && !connector && <p className="state">{t("backlog.noConnector")}</p>}

          {backlog.data && connector && !connector.lastFailure && stories.length === 0 && (
            <p className="state">{t("backlog.empty")}</p>
          )}

          {stories.length > 0 && (
            <table className="table">
              <thead>
                <tr>
                  <th className="table-num">{t("backlog.table.id")}</th>
                  <th>{t("backlog.table.title")}</th>
                  <th>{t("backlog.table.labels")}</th>
                  <th>{t("backlog.table.state")}</th>
                  <th>{t("backlog.table.runs")}</th>
                </tr>
              </thead>
              <tbody>
                {stories.map((story) => (
                  <tr key={story.vendorId}>
                    <td className="table-num mono">{story.vendorId}</td>
                    <td className="list-title">{story.title}</td>
                    <td>
                      {story.labels.length === 0 ? (
                        <span className="empty-value">—</span>
                      ) : (
                        <span className="row">
                          {story.labels.map((label) => (
                            <span className="pill pill-neutral" key={label}>
                              {label}
                            </span>
                          ))}
                        </span>
                      )}
                    </td>
                    <td>
                      <span
                        className={story.state === "open" ? "pill pill-ok" : "pill pill-neutral"}
                      >
                        {story.state}
                      </span>
                    </td>
                    <td>
                      {/* UC-021's per-Story view: jump to the Runs section filtered to this
                          Story — an anchor, because the section lives on this same page. */}
                      <a
                        className="btn"
                        href="#runs"
                        onClick={() => setRunsStoryFilter(story.vendorId)}
                      >
                        {t("backlog.table.viewRuns")}
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </AppShell>
  );
}

function ConnectorCard({
  projectId,
  connector,
}: {
  projectId: string;
  connector: ConnectorView | null;
}) {
  const configure = useConfigureConnector(projectId);
  const [owner, setOwner] = useState(connector?.owner ?? "");
  const [repository, setRepository] = useState(connector?.repository ?? "");
  const [secretName, setSecretName] = useState(connector?.secretName ?? "");

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!owner.trim() || !repository.trim() || !secretName.trim()) return;
    configure.mutate({ owner, repository, secretName });
  }

  return (
    <section className="card">
      <div className="card-header">
        <h2>{t("connector.heading")}</h2>
        {connector ? (
          <div className="row">
            <span className="badge badge-neutral">{connector.vendor}</span>
            {/* Vendor and health are two different facts; one badge cannot carry both. */}
            {connector.lastFailure ? (
              <span className="pill pill-danger">{t("connector.unhealthy")}</span>
            ) : (
              <span className="pill pill-ok">{t("connector.healthy")}</span>
            )}
          </div>
        ) : null}
      </div>

      {!connector && <p className="card-hint">{t("connector.none")}</p>}

      <form className="stack" onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label className="label" htmlFor="owner">
              {t("connector.owner")}
            </label>
            <input
              id="owner"
              className="input"
              value={owner}
              onChange={(event) => setOwner(event.target.value)}
              placeholder={t("connector.ownerPlaceholder")}
            />
          </div>
          <div className="field">
            <label className="label" htmlFor="repository">
              {t("connector.repository")}
            </label>
            <input
              id="repository"
              className="input"
              value={repository}
              onChange={(event) => setRepository(event.target.value)}
              placeholder={t("connector.repositoryPlaceholder")}
            />
          </div>
          <div className="field">
            <label className="label" htmlFor="secret-name">
              {t("connector.secretName")}
            </label>
            <input
              id="secret-name"
              className="input"
              value={secretName}
              onChange={(event) => setSecretName(event.target.value)}
              placeholder={t("connector.secretNamePlaceholder")}
            />
          </div>
          <button className="btn btn-primary" type="submit" disabled={configure.isPending}>
            {configure.isPending ? t("connector.saving") : t("connector.save")}
          </button>
        </div>

        <p className="card-hint">{t("connector.secretHint")}</p>

        {configure.isError && (
          <p className="state state-error" role="alert">
            {t("connector.saveFailed")}
          </p>
        )}
      </form>
    </section>
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
