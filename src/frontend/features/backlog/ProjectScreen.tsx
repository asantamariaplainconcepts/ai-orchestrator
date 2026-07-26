import { useState } from "react";
import { Link, useParams } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";
import { useBacklog, useConfigureConnector, useRefreshBacklog } from "./useBacklog";
import type { ConnectorView } from "./types";

/**
 * UC-004 + UC-007 on one page: the Connector configuration and the mirrored Stories.
 * Composed entirely from the design-system kit — no styles are declared here.
 */
export function ProjectScreen() {
  const { projectId = "" } = useParams();
  const backlog = useBacklog(projectId);
  const refresh = useRefreshBacklog(projectId);

  const connector = backlog.data?.connector ?? null;
  const stories = backlog.data?.stories ?? [];

  return (
    <div className="app-shell">
      <header className="app-header">
        <span className="app-title">{t("app.title")}</span>
        <div className="row">
          <Link className="btn" to="/">
            {t("project.back")}
          </Link>
          <ThemeToggle />
        </div>
      </header>

      <main className="app-main">
        <div className="stack">
          {/* Keyed on the stored Connector: the card mounts before the query settles, so without
              a remount its fields would stay empty once the saved values arrive. */}
          <ConnectorCard
            key={connector ? `${connector.owner}/${connector.repository}` : "unconfigured"}
            projectId={projectId}
            connector={connector}
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

            {/* The two empty states are different facts and must not look the same: a failed poll
                leaves the previous Stories readable and says so, rather than showing "empty". */}
            {connector?.lastFailure ? (
              <p className="state state-error" role="alert">
                {t("backlog.stale")}
              </p>
            ) : null}

            {/* Three distinguishable absences, not one: nothing connected, nothing there, and
                we could not look. Collapsing them is how an outage gets read as an empty
                repository. */}
            {backlog.data && !connector && <p className="state">{t("backlog.noConnector")}</p>}

            {backlog.data && connector && !connector.lastFailure && stories.length === 0 && (
              <p className="state">{t("backlog.empty")}</p>
            )}

            {stories.length > 0 && (
              <ul className="list">
                {stories.map((story) => (
                  <li className="list-row" key={story.vendorId}>
                    <div className="row">
                      <span className="mono">{story.vendorId}</span>
                      <span className="list-title">{story.title}</span>
                    </div>
                    <div className="row">
                      {story.labels.map((label) => (
                        <span className="badge badge-neutral" key={label}>
                          {label}
                        </span>
                      ))}
                      <span className="badge badge-info">{story.state}</span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      </main>
    </div>
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
              <span className="badge badge-danger">{t("connector.unhealthy")}</span>
            ) : (
              <span className="badge badge-ok">{t("connector.healthy")}</span>
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
