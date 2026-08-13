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
import { LocusChip } from "@/shared/ui/locus";
import { useDeploymentCapabilities } from "@/features/backlog/useBacklog";
import { useCreateProject, useProjects } from "./useProjects";
import { healthOf, useConnectorHealth } from "./useConnectorHealth";
import type { ConnectorHealth, HealthState } from "./useConnectorHealth";
import type { FolderOutcome } from "./types";

/**
 * First screen on the Platform theme, and the migration recipe (design D2): unwrapped shadcn
 * primitives, theme tokens only, not a kit class left. Screens that migrate after this one
 * copy this diff's shape.
 */
export function ProjectsScreen() {
  const [name, setName] = useState("");
  const [folder, setFolder] = useState("");
  const [outcome, setOutcome] = useState<FolderOutcome | null>(null);
  const [showArchived, setShowArchived] = useState(false);
  const projects = useProjects(showArchived);
  const createProject = useCreateProject();
  const capabilities = useDeploymentCapabilities();
  const health = useConnectorHealth();
  const byProject = new Map<string, ConnectorHealth>(
    (health.data ?? []).map((connector) => [connector.projectId, connector]),
  );

  // The habitat decides whether a folder can be named, and it is asked rather than inferred
  // (#247, ADR-0010). A posture derived in the browser would be a second answer to a question the
  // server already answers, and the two would drift. Undefined while the read is in flight, so the
  // input appears when the answer arrives rather than flickering on a guess.
  const folderOffered = capabilities.data ? capabilities.data.localFolderReason === null : false;

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!name.trim()) return;

    const named = folderOffered && folder.trim().length > 0;

    createProject.mutate(named ? { name, folder: folder.trim() } : { name }, {
      onSuccess: (created) => {
        setName("");
        setFolder("");
        // Held so the Admin can read what the folder yielded — or which single check stopped it —
        // after the form has cleared. Null where no folder was named, which is the ordinary path.
        setOutcome(created.connector ?? null);
      },
    });
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
            <form className="flex flex-col gap-3" onSubmit={submit}>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
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
              </div>

              {/* The folder is offered only where the habitat says it can work (#347). A deployment
                  runs the orchestrator in a container that cannot see this machine's disk, so the
                  input is ABSENT there rather than present and refusing — exactly as the code source
                  is absent from the Connector form. */}
              {folderOffered ? (
                <div className="flex flex-col gap-2">
                  <Label htmlFor="project-folder">{t("projects.create.folder")}</Label>
                  <Input
                    id="project-folder"
                    value={folder}
                    onChange={(event) => setFolder(event.target.value)}
                    placeholder={t("projects.create.folderPlaceholder")}
                  />
                  <p className="text-sm text-muted-foreground">
                    {t("projects.create.folderExplanation")}
                  </p>
                  {/* D6: what THIS CONFIGURATION requires, never what the credential holds. The
                      credential-helper protocol carries no scope and no capability, so the product
                      cannot tell an operator what they granted — and must say so rather than let the
                      form imply it checked. */}
                  <p className="text-sm text-muted-foreground">
                    {t("projects.create.folderPermissions")}
                  </p>
                </div>
              ) : null}
            </form>

            {/* What the folder yielded, after the fact — the four checks have four different fixes,
                so the failing one is named rather than collapsed into "that folder didn't work". */}
            {outcome ? <FolderResult outcome={outcome} /> : null}
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
                      {/* Mock 3e (#211): the same chip vocabulary as the Run locus chip, so
                          "local" looks identical everywhere it appears. */}
                      {byProject.get(project.id)?.codeSource === "LocalFolder" ? (
                        <LocusChip locus="Local" />
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

/**
 * What naming a folder produced (#347): the coordinates it derived, or the one check that stopped it.
 *
 * The four checks are rendered as four different sentences on purpose. "That folder didn't work"
 * would be true of all of them and useful for none — a path that is not a directory, a directory that
 * is not a repository, a repository with no `origin`, and an `origin` neither vendor recognises have
 * four different fixes, and the failing one is the only thing that says which to make.
 *
 * The coordinates are shown rather than made editable here: they are already stored on the Connector,
 * and the Connector's own form is where they are changed. A second editable copy would be a second
 * source of truth for the same three fields.
 */
function FolderResult({ outcome }: { outcome: FolderOutcome }) {
  if (outcome.configured) {
    return (
      <div className="mt-3 flex flex-col gap-1 border-t pt-3" role="status">
        <p className="text-sm font-medium">{t("projects.create.folderDerived")}</p>
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm text-muted-foreground">
          <dt>{t("projects.create.folderVendor")}</dt>
          <dd>{outcome.vendor}</dd>
          <dt>{t("projects.create.folderOwner")}</dt>
          <dd>{outcome.owner}</dd>
          <dt>{t("projects.create.folderRepository")}</dt>
          <dd>{outcome.repository}</dd>
          {outcome.codeRepository ? (
            <>
              <dt>{t("projects.create.folderCodeRepository")}</dt>
              <dd>{outcome.codeRepository}</dd>
            </>
          ) : null}
        </dl>
        <p className="text-sm text-muted-foreground">{t("projects.create.folderEditable")}</p>
      </div>
    );
  }

  return (
    <div className="mt-3 border-t pt-3" role="status">
      <p className="text-sm text-muted-foreground">
        {outcome.failedCheck && outcome.failedCheck in FOLDER_FAILURES
          ? t(FOLDER_FAILURES[outcome.failedCheck as keyof typeof FOLDER_FAILURES])
          : t("projects.create.folderFailed.unknownVendor")}{" "}
        {t("projects.create.folderTypeThemIn")}
      </p>
    </div>
  );
}

/** The four checks the create handler makes, in the order a person would make them. */
const FOLDER_FAILURES = {
  notADirectory: "projects.create.folderFailed.notADirectory",
  notAGitRepository: "projects.create.folderFailed.notAGitRepository",
  noOrigin: "projects.create.folderFailed.noOrigin",
  unknownVendor: "projects.create.folderFailed.unknownVendor",
} as const;

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
