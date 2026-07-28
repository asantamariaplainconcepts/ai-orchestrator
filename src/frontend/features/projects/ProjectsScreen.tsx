import { useState } from "react";
import { Link } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { AppShell } from "@/shared/ui/AppShell";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { useCreateProject, useProjects } from "./useProjects";
import { healthOf, useConnectorHealth } from "./useConnectorHealth";
import type { ConnectorHealth, HealthState } from "./useConnectorHealth";

/**
 * First screen on the Platform theme, and the migration recipe (design D2): unwrapped shadcn
 * primitives, theme tokens only, not a kit class left. Screens that migrate after this one
 * copy this diff's shape.
 */
export function ProjectsScreen() {
  const [name, setName] = useState("");
  const [showArchived, setShowArchived] = useState(false);
  const projects = useProjects(showArchived);
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
      <div className="flex flex-col gap-4">
        <div className="flex items-center gap-2">
          <p className="text-sm text-muted-foreground">{t("projects.subtitle")}</p>
          {projects.data ? (
            <Badge variant="secondary">
              {tCount(projects.data.projects.length, "projects.count.one", "projects.count.other")}
            </Badge>
          ) : null}
          {/* Stated rather than silently dropped: a list that hides rows without saying so
              teaches its reader that things vanish (#121). */}
          {projects.data && projects.data.archivedCount > 0 ? (
            <Button
              variant="ghost"
              size="sm"
              type="button"
              aria-pressed={showArchived}
              onClick={() => setShowArchived((shown) => !shown)}
            >
              {projects.data.archivedCount} {t("projects.archived.count")} ·{" "}
              {showArchived ? t("projects.archived.hide") : t("projects.archived.show")}
            </Button>
          ) : null}
        </div>

        <Card>
          <CardContent>
            <form className="flex flex-col gap-3 sm:flex-row sm:items-end" onSubmit={submit}>
              <div className="flex flex-1 flex-col gap-2">
                <Label htmlFor="project-name">{t("projects.create.name")}</Label>
                <Input
                  id="project-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder={t("projects.create.placeholder")}
                />
              </div>
              <Button type="submit" disabled={createProject.isPending}>
                {createProject.isPending
                  ? t("projects.create.pending")
                  : t("projects.create.submit")}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card aria-label={t("projects.heading")}>
          <CardContent>
            {/* All four states, every time. */}
            {projects.isPending && (
              <p className="text-sm text-muted-foreground">{t("projects.loading")}</p>
            )}
            {projects.isError && (
              <p className="text-sm text-destructive" role="alert">
                {t("projects.error")}
              </p>
            )}
            {projects.data?.projects.length === 0 && (
              <p className="text-sm text-muted-foreground">{t("projects.empty")}</p>
            )}

            {projects.data && projects.data.projects.length > 0 && (
              <ul className="divide-y">
                {projects.data.projects.map((project) => (
                  <li
                    className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"
                    key={project.id}
                  >
                    <span className="flex min-w-0 items-center gap-2">
                      <Link
                        className="min-w-0 truncate text-sm font-medium transition-colors hover:text-primary"
                        to={`/projects/${project.id}`}
                      >
                        {project.name}
                      </Link>
                      {project.archivedAt ? (
                        <Badge variant="outline">{t("projects.archived.badge")}</Badge>
                      ) : null}
                    </span>
                    <span className="flex shrink-0 items-center gap-2">
                      <HealthBadge connector={byProject.get(project.id)} />
                      <span className="hidden font-mono text-xs text-muted-foreground sm:inline">
                        {project.id}
                      </span>
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
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
function HealthBadge({ connector }: { connector: ConnectorHealth | undefined }) {
  const state = healthOf(connector);

  return (
    <Badge
      variant={state === "failing" ? "destructive" : "secondary"}
      className={cn(state === "healthy" && "bg-success text-success-foreground")}
      title={connector?.lastFailure ?? undefined}
    >
      {t(HEALTH_COPY[state])}
      {state === "healthy" && connector?.lastSyncedAt ? ` · ${age(connector.lastSyncedAt)}` : ""}
    </Badge>
  );
}
