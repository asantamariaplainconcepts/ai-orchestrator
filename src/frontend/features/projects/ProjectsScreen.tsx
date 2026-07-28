import { useState } from "react";
import { Link } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { useCreateProject, useProjects } from "./useProjects";
import { healthOf, useConnectorHealth } from "./useConnectorHealth";
import type { ConnectorHealth, HealthState } from "./useConnectorHealth";

export function ProjectsScreen() {
  const [name, setName] = useState("");
  const projects = useProjects();
  const createProject = useCreateProject();
  const health = useConnectorHealth();
  const byProject = new Map<string, ConnectorHealth>(
    (health.data ?? []).map((connector) => [connector.projectId, connector]),
  );

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!name.trim()) return;
    createProject.mutate({ name }, { onSuccess: () => setName("") });
  }

  return (
    <AppShell crumbs={[{ label: t("shell.crumb.projects") }]} title={t("projects.heading")}>
      <div className="stack">
        <div className="row">
          <p className="card-hint">{t("projects.subtitle")}</p>
          {projects.data ? (
            <span className="badge badge-neutral">
              {tCount(projects.data.length, "projects.count.one", "projects.count.other")}
            </span>
          ) : null}
        </div>

        <section className="card">
          <form className="row" onSubmit={submit}>
            <div className="field" style={{ flex: 1 }}>
              <label className="label" htmlFor="project-name">
                {t("projects.create.name")}
              </label>
              <input
                id="project-name"
                className="input"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t("projects.create.placeholder")}
              />
            </div>
            <button className="btn btn-primary" type="submit" disabled={createProject.isPending}>
              {createProject.isPending ? t("projects.create.pending") : t("projects.create.submit")}
            </button>
          </form>
        </section>

        <section className="card" aria-label={t("projects.heading")}>
          {/* All four states, every time — the kit provides each one. */}
          {projects.isPending && <p className="state">{t("projects.loading")}</p>}
          {projects.isError && (
            <p className="state state-error" role="alert">
              {t("projects.error")}
            </p>
          )}
          {projects.data?.length === 0 && <p className="state">{t("projects.empty")}</p>}

          {projects.data && projects.data.length > 0 && (
            <ul className="list">
              {projects.data.map((project) => (
                <li className="list-row" key={project.id}>
                  <Link className="list-title" to={`/projects/${project.id}`}>
                    {project.name}
                  </Link>
                  <span className="row">
                    <HealthPill connector={byProject.get(project.id)} />
                    <span className="mono">{project.id}</span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </AppShell>
  );
}

/** Relative for recency, absolute past a day — the content fundamentals' rule. */
function age(iso: string): string {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return t("projects.health.justNow");
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return new Date(iso).toLocaleDateString();
}

const HEALTH_COPY = {
  healthy: "projects.health.healthy",
  failing: "projects.health.failing",
  neverSynced: "projects.health.neverSynced",
  notConfigured: "projects.health.notConfigured",
} as const satisfies Record<HealthState, string>;

/**
 * Four states, not a boolean (#97): failing carries its stored sentence as the title, so the
 * reason is one hover away without leaving the list. A healthy pill shows the sync age —
 * stale-but-not-failing is a state a Member should be able to notice (BR-008).
 */
function HealthPill({ connector }: { connector: ConnectorHealth | undefined }) {
  const state = healthOf(connector);
  const pillClass =
    state === "healthy"
      ? "pill pill-ok"
      : state === "failing"
        ? "pill pill-danger"
        : "pill pill-neutral";

  return (
    <span className={pillClass} title={connector?.lastFailure ?? undefined}>
      {t(HEALTH_COPY[state])}
      {state === "healthy" && connector?.lastSyncedAt
        ? ` \u00b7 ${age(connector.lastSyncedAt)}`
        : ""}
    </span>
  );
}
