import { useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { AutomationsSection } from "@/features/automations/AutomationsSection";
import { ConversationPanel } from "@/features/conversations/ConversationPanel";
import { RolesPanel } from "@/features/identity/RolesPanel";
import { OperateStrip } from "@/features/runs/OperateStrip";
import { RunsSection } from "@/features/runs/RunsSection";
import { useAutomations } from "@/features/automations/useAutomations";
import { useRunNow } from "@/features/runs/useRunNow";
import { formatCost, useProjectCost } from "@/features/runs/useRuns";
import { useArchiveProject, useProjects, useRestoreProject } from "@/features/projects/useProjects";
import { t, tCount } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/shared/ui/tabs";
import {
  useBacklog,
  useConfigureConnector,
  useRefreshBacklog,
  useTestConnector,
  useWriteStoryLabel,
} from "./useBacklog";
import { ApiError } from "@/shared/http/client";
import { useRememberedPreference } from "@/shared/lib/useRememberedPreference";
import { useProjectRole } from "@/shared/identity/useCurrentPrincipal";
import { KanbanBoard } from "./KanbanBoard";
import type { BoardAutomation } from "./KanbanBoard";
import { BACKLOG_VENDORS } from "./types";
import type { BacklogVendor, ConnectorView, StoryView } from "./types";

const TABS = ["operate", "runs", "automations", "ask", "settings"] as const;
type Tab = (typeof TABS)[number];

type BacklogViewMode = "list" | "board";
const VIEW_PREFERENCE = "aio:backlog-view";

/**
 * Unlike the landing tab (design D3), which is derived because the project's state decides it,
 * list-or-board is a genuine preference: nothing about the project implies an answer.
 *
 * The lazy read and the guarded write moved into `useRememberedPreference` when the sidebar became the
 * second of them (#126, design D3). Behaviour is unchanged — that hook is this code, extracted.
 */
function isViewMode(value: string): value is BacklogViewMode {
  return value === "list" || value === "board";
}

/**
 * UC-004 + UC-007, separated: operating lives on its own tab, configuring on its own. The page
 * is on the Platform theme — dashboard-tabs rebuilds every section it renders, so it migrates
 * whole rather than screen-by-half (adopt-foundations D2).
 */
export function ProjectScreen() {
  const { projectId = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const [runsStoryFilter, setRunsStoryFilter] = useState<string | null>(null);
  const [view, setView] = useRememberedPreference<BacklogViewMode>(
    VIEW_PREFERENCE,
    "list",
    isViewMode,
  );
  const backlog = useBacklog(projectId);
  // Including archived ones: this screen must find its project whatever its state, or an
  // archived project loses its name, its notice and its restore button — reading stays open
  // (#121 design D2), and the detail page is reading.
  const projects = useProjects(true);
  const automations = useAutomations(projectId);
  const cost = useProjectCost(projectId);
  // What this caller may do *here* (#13): asked per project, because that is the only scope in
  // which the question has an answer.
  const role = useProjectRole(projectId);

  const connector = backlog.data?.connector ?? null;
  const stories = backlog.data?.stories ?? [];

  // The page title is a real fact from the live projects response — never invented.
  const project = projects.data?.projects.find((candidate) => candidate.id === projectId);
  const title = project?.name ?? t("project.title.fallback");

  // The landing tab is derived, never stored (design D3): configuring IS the job on day one,
  // and never again. An explicit ?tab= always wins — a deep link is the user saying where to be.
  const requested = searchParams.get("tab");
  const asked = TABS.find((candidate) => candidate === requested);
  const landing: Tab = backlog.data && !connector ? "settings" : "operate";
  const tab = asked ?? landing;

  function selectTab(next: string) {
    // Every choice is marked, including operate. Leaving operate unmarked made it unreachable
    // on an unconfigured project: clearing the parameter handed control back to the derived
    // landing, which sent the user straight back to settings. The absence of a parameter means
    // "the user has not chosen yet" — once they have, the URL says so.
    const params = new URLSearchParams(searchParams);
    params.set("tab", next);
    setSearchParams(params, { replace: true });
  }

  return (
    <AppShell
      crumbs={[{ label: t("shell.crumb.projects"), to: "/projects" }, { label: title }]}
      title={title}
      actions={
        <div className="hidden items-center gap-2 sm:flex">
          {/* Cost and health moved out of the retired stat cards into the header line. */}
          <span className="text-xs text-muted-foreground">
            {formatCost(cost.data?.totalCostUsd ?? null) ?? "—"}
          </span>
          {connector ? <ConnectorHealthBadge connector={connector} /> : null}
        </div>
      }
    >
      <Tabs value={tab} onValueChange={selectTab} className="gap-6">
        {/* Bottom-docked below md so a thumb reaches it; inline from md up (design D6). */}
        <TabsList className="fixed inset-x-0 bottom-0 z-40 w-full justify-around rounded-none border-t bg-card p-1 md:static md:inset-auto md:w-fit md:justify-start md:rounded-lg md:border-0 md:bg-muted">
          <TabsTrigger value="operate">{t("project.tab.operate")}</TabsTrigger>
          <TabsTrigger value="runs">{t("project.tab.runs")}</TabsTrigger>
          <TabsTrigger value="automations">{t("project.tab.automations")}</TabsTrigger>
          <TabsTrigger value="ask">{t("project.tab.ask")}</TabsTrigger>
          <TabsTrigger value="settings">{t("project.tab.settings")}</TabsTrigger>
        </TabsList>

        {/* The docked tab bar would otherwise sit on top of the last row. */}
        <div className="pb-16 md:pb-0">
          <TabsContent value="operate" className="flex flex-col gap-6">
            {connector ? (
              <OperateStrip projectId={projectId} onShowRuns={() => selectTab("runs")} />
            ) : null}
            <BacklogPanel
              projectId={projectId}
              connector={connector}
              stories={stories}
              isPending={backlog.isPending}
              isError={backlog.isError}
              hasResponse={Boolean(backlog.data)}
              automations={automations.data ?? []}
              onViewRuns={(vendorStoryId) => {
                setRunsStoryFilter(vendorStoryId);
                selectTab("runs");
              }}
              view={view}
              onViewChange={setView}
            />
          </TabsContent>

          <TabsContent value="runs">
            <RunsSection
              projectId={projectId}
              storyFilter={runsStoryFilter}
              onClearFilter={() => setRunsStoryFilter(null)}
            />
          </TabsContent>

          <TabsContent value="automations">
            <AutomationsSection projectId={projectId} />
          </TabsContent>

          {/* Its own tab, because a conversation is its own thing (#166): not a Run, so not
              beside them, and not configuration, so not in Settings. */}
          <TabsContent value="ask">
            <ConversationPanel projectId={projectId} />
          </TabsContent>

          <TabsContent value="settings" className="flex flex-col gap-6">
            <ConnectorPanel
              key={connector ? `${connector.owner}/${connector.repository}` : "unconfigured"}
              projectId={projectId}
              connector={connector}
            />
            {/* Who may do what here (#13, UC-002). Admin-only, and the server refuses the read
                too — this decides what is worth showing, never what is allowed. */}
            <RolesPanel projectId={projectId} canManage={role === "Admin"} />
            <RetirementPanel
              projectId={projectId}
              projectName={project?.name ?? null}
              archivedAt={project?.archivedAt ?? null}
            />
          </TabsContent>
        </div>
      </Tabs>
    </AppShell>
  );
}

function ConnectorHealthBadge({ connector }: { connector: ConnectorView }) {
  return connector.lastFailure ? (
    <Badge variant="destructive" title={connector.lastFailure}>
      {t("connector.unhealthy")}
    </Badge>
  ) : (
    <Badge className="bg-success text-success-foreground">{t("connector.healthy")}</Badge>
  );
}

/**
 * The daily surface: stories with their per-row actions. One tree, two layouts (design D6) —
 * a row from md up, a stacked card below it, so every action exists at both widths.
 */
function BacklogPanel({
  projectId,
  connector,
  stories,
  isPending,
  isError,
  hasResponse,
  automations,
  onViewRuns,
  view,
  onViewChange,
}: {
  projectId: string;
  connector: ConnectorView | null;
  stories: StoryView[];
  isPending: boolean;
  isError: boolean;
  hasResponse: boolean;
  automations: BoardAutomation[];
  onViewRuns: (vendorStoryId: string) => void;
  view: BacklogViewMode;
  onViewChange: (next: BacklogViewMode) => void;
}) {
  const refresh = useRefreshBacklog(projectId);
  const writeLabel = useWriteStoryLabel(projectId);
  const runNow = useRunNow(projectId);

  // UC-012: chosen Story + Automation. UC-008's UI scope (design D4 of backlog): only enabled
  // Automations' trigger labels are actionable.
  const enabledAutomations = automations.filter((automation) => automation.enabled);
  const triggerLabels = [
    ...new Set(enabledAutomations.map((automation) => automation.triggerLabel)),
  ];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-base font-semibold">{t("backlog.heading")}</h2>
          {connector ? (
            <Badge variant="secondary">
              {tCount(stories.length, "backlog.count.one", "backlog.count.other")}
            </Badge>
          ) : null}
          {connector?.lastSyncedAt ? (
            <span className="text-xs text-muted-foreground">
              {t("backlog.syncedAt")} {formatWhen(connector.lastSyncedAt)}
            </span>
          ) : connector ? (
            <span className="text-xs text-muted-foreground">{t("backlog.neverSynced")}</span>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {/* A genuine preference, unlike the landing tab: nothing about the project derives it,
              so this one is remembered (#110). */}
          <Button
            variant="outline"
            type="button"
            aria-pressed={view === "board"}
            onClick={() => onViewChange(view === "board" ? "list" : "board")}
          >
            {view === "board" ? t("board.showList") : t("board.showBoard")}
          </Button>
          <Button
            variant="outline"
            type="button"
            onClick={() => refresh.mutate()}
            disabled={!connector || refresh.isPending}
          >
            {refresh.isPending ? t("backlog.refreshing") : t("backlog.refresh")}
          </Button>
        </div>
      </div>

      {isPending && <p className="text-sm text-muted-foreground">{t("backlog.loading")}</p>}
      {isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("backlog.error")}
        </p>
      )}

      {/* Three distinguishable absences, not one: nothing connected, nothing there, and we
          could not look. Collapsing them is how an outage reads as an empty repository. */}
      {connector?.lastFailure ? (
        <p className="text-sm text-destructive" role="alert">
          {t("backlog.stale")}
        </p>
      ) : null}
      {/* A refused write-back must be visible: the mirror did not change. */}
      {writeLabel.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("backlog.labels.failed")}
        </p>
      )}
      {/* BR-001's refusal is an answer, not a defect — say the rule. */}
      {runNow.isError && (
        <p className="text-sm text-destructive" role="alert">
          {runNow.error instanceof ApiError && runNow.error.status === 409
            ? t("runs.runNow.conflict")
            : t("runs.runNow.failed")}
        </p>
      )}

      {hasResponse && !connector && (
        <p className="text-sm text-muted-foreground">{t("backlog.noConnector")}</p>
      )}
      {hasResponse && connector && !connector.lastFailure && stories.length === 0 && (
        <p className="text-sm text-muted-foreground">{t("backlog.empty")}</p>
      )}

      {stories.length > 0 && view === "board" && (
        <KanbanBoard projectId={projectId} stories={stories} automations={automations} />
      )}

      {stories.length > 0 && view === "list" && (
        <Card>
          <CardContent>
            <ul className="divide-y">
              {stories.map((story) => (
                <li
                  key={story.vendorId}
                  className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 lg:flex-row lg:items-start lg:justify-between"
                >
                  <div className="flex min-w-0 flex-col gap-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs text-muted-foreground">
                        #{story.vendorId}
                      </span>
                      <Badge variant={story.state === "open" ? "secondary" : "outline"}>
                        {story.state}
                      </Badge>
                    </div>
                    <Link
                      className="text-sm font-medium transition-colors hover:text-primary"
                      to={`/projects/${projectId}/stories/${story.vendorId}`}
                    >
                      {story.title}
                    </Link>
                    <div className="flex flex-wrap items-center gap-1.5">
                      {story.labels.map((label) =>
                        triggerLabels.includes(label) ? (
                          <button
                            key={label}
                            type="button"
                            disabled={writeLabel.isPending}
                            title={t("backlog.labels.remove")}
                            onClick={() =>
                              writeLabel.mutate({
                                vendorStoryId: story.vendorId,
                                label,
                                apply: false,
                              })
                            }
                          >
                            <Badge className="bg-success text-success-foreground">
                              {label} {t("backlog.labels.removeGlyph")}
                            </Badge>
                          </button>
                        ) : (
                          <Badge variant="secondary" key={label}>
                            {label}
                          </Badge>
                        ),
                      )}
                      {triggerLabels
                        .filter((label) => !story.labels.includes(label))
                        .map((label) => (
                          <button
                            key={label}
                            type="button"
                            disabled={writeLabel.isPending}
                            title={t("backlog.labels.apply")}
                            onClick={() =>
                              writeLabel.mutate({
                                vendorStoryId: story.vendorId,
                                label,
                                apply: true,
                              })
                            }
                          >
                            <Badge variant="outline">
                              {t("backlog.labels.applyGlyph")} {label}
                            </Badge>
                          </button>
                        ))}
                    </div>
                  </div>

                  <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      type="button"
                      onClick={() => onViewRuns(story.vendorId)}
                    >
                      {t("backlog.table.viewRuns")}
                    </Button>
                    <RunNowControl
                      automations={enabledAutomations}
                      pending={runNow.isPending}
                      onRun={(automationId) =>
                        runNow.mutate({ vendorStoryId: story.vendorId, automationId })
                      }
                    />
                  </div>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function RunNowControl({
  automations,
  pending,
  onRun,
}: {
  automations: { id: string; triggerLabel: string }[];
  pending: boolean;
  onRun: (automationId: string) => void;
}) {
  const [selected, setSelected] = useState(automations[0]?.id ?? "");
  const chosen = automations.find((automation) => automation.id === selected) ?? automations[0];

  if (automations.length === 0) return null;

  return (
    <span className="flex items-center gap-2">
      {automations.length > 1 && (
        <NativeSelect
          className="w-auto"
          value={chosen?.id ?? ""}
          onChange={(event) => setSelected(event.target.value)}
          aria-label={t("runs.runNow.pickAutomation")}
        >
          {automations.map((automation) => (
            <option key={automation.id} value={automation.id}>
              {automation.triggerLabel}
            </option>
          ))}
        </NativeSelect>
      )}
      <Button
        size="sm"
        type="button"
        disabled={pending || !chosen}
        onClick={() => chosen && onRun(chosen.id)}
      >
        {pending ? t("runs.runNow.pending") : t("runs.runNow.button")}
      </Button>
    </span>
  );
}

/**
 * Settings: one line when configured, the full form when absent or editing. Six permanently
 * expanded fields told every visitor this page was about setup — true once, false thereafter.
 */
function ConnectorPanel({
  projectId,
  connector,
}: {
  projectId: string;
  connector: ConnectorView | null;
}) {
  const configure = useConfigureConnector(projectId);
  const [editing, setEditing] = useState(false);
  const [vendor, setVendor] = useState<BacklogVendor>(
    (connector?.vendor as BacklogVendor) ?? "GitHub",
  );
  const [owner, setOwner] = useState(connector?.owner ?? "");
  const [repository, setRepository] = useState(connector?.repository ?? "");
  const [secretName, setSecretName] = useState(connector?.secretName ?? "");
  const [codeRepository, setCodeRepository] = useState(connector?.codeRepository ?? "");
  // Where this project keeps its prompt files (#150). Blank means the convention, so the
  // placeholder shows the default rather than pre-filling a value nobody chose.
  const [promptDirectory, setPromptDirectory] = useState(connector?.promptDirectory ?? "");

  // Pasting is the default (#124), because the operator who already manages secrets knows to
  // switch and the first-time user does not know a vault exists. A Connector whose secret this
  // product never wrote opens on the naming path, which is the one it is actually using.
  const [credentialMode, setCredentialMode] = useState<"paste" | "name">(
    connector && !connector.secretSetAt ? "name" : "paste",
  );
  const [accessToken, setAccessToken] = useState("");
  const pasting = credentialMode === "paste";

  // The two coordinates mean different things per vendor — organisation/project on Azure
  // DevOps, owner/repository on GitHub. Labelling both "Owner" would ask an Admin to translate.
  const coordinateLabels =
    vendor === "AzureDevOps"
      ? { owner: t("connector.organisation"), repository: t("connector.project") }
      : { owner: t("connector.owner"), repository: t("connector.repository") };

  // On GitHub the backlog and the code are one repository, so there is nothing to name.
  const needsCodeRepository = vendor === "AzureDevOps";
  const open = !connector || editing;

  // With a Connector already configured, leaving the credential blank means "keep the one you have"
  // (#160): the product holds it, verified, under this Connector's own name, and asking for it again to
  // change a path is what trained people to keep PATs lying around.
  const keepingStored = Boolean(connector) && (pasting ? !accessToken.trim() : !secretName.trim());

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!owner.trim() || !repository.trim()) return;
    // Only a first connect needs one: there is nothing stored to fall back on.
    if (!connector && (pasting ? !accessToken.trim() : !secretName.trim())) return;
    configure.mutate(
      {
        owner,
        repository,
        // Never both, as the API requires; neither is the reuse path, and the API decides whether
        // that is allowed — the form no longer refuses on its behalf.
        secretName: keepingStored || pasting ? null : secretName,
        accessToken: keepingStored || !pasting ? null : accessToken,
        vendor,
        codeRepository: needsCodeRepository && codeRepository.trim() ? codeRepository : null,
        promptDirectory: promptDirectory.trim() ? promptDirectory.trim() : null,
      },
      {
        onSuccess: () => {
          setEditing(false);
          // Held only as long as the request needed it — the value does not survive the save,
          // in the browser any more than on the server.
          setAccessToken("");
        },
      },
    );
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-semibold">{t("connector.heading")}</h2>
          {connector ? (
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">{connector.vendor}</Badge>
              <ConnectorHealthBadge connector={connector} />
            </div>
          ) : null}
        </div>

        {connector && !editing ? <CredentialTest projectId={projectId} /> : null}

        {connector && !editing ? (
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span className="flex flex-wrap items-center gap-2 text-sm">
              <span className="font-mono">
                {connector.owner}/{connector.repository}
              </span>
              <span className="text-xs text-muted-foreground">
                {/* The name and when the product wrote it — never the value (BR-010/DEC-052). */}
                {connector.secretSetAt
                  ? `${t("connector.secretSetAt")} ${formatWhen(connector.secretSetAt)}`
                  : t("connector.secretManagedElsewhere")}
              </span>
              <span className="text-xs text-muted-foreground">
                {connector.lastSyncedAt
                  ? `${t("backlog.syncedAt")} ${formatWhen(connector.lastSyncedAt)}`
                  : t("backlog.neverSynced")}
              </span>
            </span>
            <Button variant="outline" type="button" onClick={() => setEditing(true)}>
              {t("connector.edit")}
            </Button>
          </div>
        ) : null}

        {!connector && <p className="text-sm text-muted-foreground">{t("connector.none")}</p>}

        {open ? (
          <form className="flex flex-col gap-4" onSubmit={submit}>
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
              <div className="flex flex-col gap-2">
                <Label htmlFor="vendor">{t("connector.vendor")}</Label>
                <NativeSelect
                  id="vendor"
                  value={vendor}
                  onChange={(event) => setVendor(event.target.value as BacklogVendor)}
                >
                  {BACKLOG_VENDORS.map((candidate) => (
                    <option key={candidate} value={candidate}>
                      {candidate === "AzureDevOps"
                        ? `${t("connector.vendor.azureDevOps")} — ${t("connector.vendor.unexercised")}`
                        : t("connector.vendor.gitHub")}
                    </option>
                  ))}
                </NativeSelect>
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="owner">{coordinateLabels.owner}</Label>
                <Input
                  id="owner"
                  value={owner}
                  onChange={(event) => setOwner(event.target.value)}
                  placeholder={t("connector.ownerPlaceholder")}
                />
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="repository">{coordinateLabels.repository}</Label>
                <Input
                  id="repository"
                  value={repository}
                  onChange={(event) => setRepository(event.target.value)}
                  placeholder={t("connector.repositoryPlaceholder")}
                />
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="credential-mode">{t("connector.credential")}</Label>
                <NativeSelect
                  id="credential-mode"
                  value={credentialMode}
                  onChange={(event) => setCredentialMode(event.target.value as "paste" | "name")}
                >
                  <option value="paste">{t("connector.credential.paste")}</option>
                  <option value="name">{t("connector.credential.name")}</option>
                </NativeSelect>
              </div>
              {pasting ? (
                <div className="flex flex-col gap-2">
                  <Label htmlFor="access-token">{t("connector.accessToken")}</Label>
                  <Input
                    id="access-token"
                    type="password"
                    autoComplete="off"
                    value={accessToken}
                    onChange={(event) => setAccessToken(event.target.value)}
                    placeholder={t("connector.accessTokenPlaceholder")}
                  />
                </div>
              ) : (
                <div className="flex flex-col gap-2">
                  <Label htmlFor="secret-name">{t("connector.secretName")}</Label>
                  <Input
                    id="secret-name"
                    value={secretName}
                    onChange={(event) => setSecretName(event.target.value)}
                    placeholder={t("connector.secretNamePlaceholder")}
                  />
                </div>
              )}
              {needsCodeRepository ? (
                <div className="flex flex-col gap-2">
                  <Label htmlFor="code-repository">{t("connector.codeRepository")}</Label>
                  <Input
                    id="code-repository"
                    value={codeRepository}
                    onChange={(event) => setCodeRepository(event.target.value)}
                    placeholder={t("connector.codeRepositoryPlaceholder")}
                  />
                </div>
              ) : null}
              {/* Every vendor has one: prompts live in the repository whatever hosts it. */}
              <div className="flex flex-col gap-2">
                <Label htmlFor="prompt-directory">{t("connector.promptDirectory")}</Label>
                <Input
                  id="prompt-directory"
                  value={promptDirectory}
                  onChange={(event) => setPromptDirectory(event.target.value)}
                  placeholder={t("connector.promptDirectoryPlaceholder")}
                />
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Button type="submit" disabled={configure.isPending}>
                {configure.isPending ? t("connector.saving") : t("connector.save")}
              </Button>
              {connector ? (
                <Button variant="ghost" type="button" onClick={() => setEditing(false)}>
                  {t("connector.cancel")}
                </Button>
              ) : null}
            </div>

            <p className="text-xs text-muted-foreground">
              {keepingStored
                ? t("connector.keepsStoredCredential")
                : pasting
                  ? t("connector.accessTokenHint")
                  : t("connector.secretHint")}
            </p>
            {needsCodeRepository ? (
              <p className="text-xs text-muted-foreground">{t("connector.codeRepositoryHint")}</p>
            ) : null}
            <p className="text-xs text-muted-foreground">{t("connector.promptDirectoryHint")}</p>

            {configure.isError && (
              <p className="text-sm text-destructive" role="alert">
                {/* The API's own reason when it gave one: a refusal that names the remedy is
                    the answer, and replacing it with a generic line throws the answer away. */}
                {(configure.error instanceof ApiError && configure.error.detail) ||
                  t("connector.saveFailed")}
              </p>
            )}
          </form>
        ) : null}
      </CardContent>
    </Card>
  );
}

/**
 * #132 — what the stored credential can actually do, asked on demand. The same probe that gates
 * saving, so the answer here and the answer at the gate cannot diverge.
 *
 * Deliberately not run on mount: it costs live vendor calls, and a permission that can be revoked
 * at any time makes a cached reassurance worse than none. The Admin asks when they want to know.
 */
function CredentialTest({ projectId }: { projectId: string }) {
  const test = useTestConnector(projectId);
  const result = test.data ?? null;

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-dashed p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Button
          variant="secondary"
          type="button"
          disabled={test.isPending}
          onClick={() => test.mutate()}
        >
          {test.isPending ? t("connector.testing") : t("connector.test")}
        </Button>
        {result ? (
          <span className="text-sm">
            {result.satisfied ? t("connector.test.satisfied") : t("connector.test.refused")}
          </span>
        ) : null}
      </div>

      {test.isError && (
        <p className="text-sm text-destructive" role="alert">
          {(test.error instanceof ApiError && test.error.detail) || t("connector.test.failed")}
        </p>
      )}

      {result ? (
        <ul className="flex flex-col gap-1">
          {result.capabilities.map((capability) => (
            <li key={capability.capability} className="flex flex-wrap items-baseline gap-2 text-sm">
              <Badge variant={capability.succeeded ? "secondary" : "destructive"}>
                {capability.succeeded ? t("connector.test.ok") : t("connector.test.no")}
              </Badge>
              <span>{capability.capability}</span>
              {/* The vendor's own sentence, which names the missing permission better than we can. */}
              {capability.reason ? (
                <span className="text-xs text-muted-foreground">{capability.reason}</span>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

/**
 * Retiring a project (#121). Archiving stops its work and keeps its history; typing the name is
 * the guard, proportionate to an act that is reversible but easy to do by accident (design D4).
 */
function RetirementPanel({
  projectId,
  projectName,
  archivedAt,
}: {
  projectId: string;
  projectName: string | null;
  archivedAt: string | null;
}) {
  const archive = useArchiveProject();
  const restore = useRestoreProject();
  const [confirmName, setConfirmName] = useState("");

  if (archivedAt) {
    return (
      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3">
          <span className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{t("project.archived.notice")}</span>
            <span className="text-xs text-muted-foreground">{formatWhen(archivedAt)}</span>
          </span>
          <Button
            variant="outline"
            type="button"
            disabled={restore.isPending}
            onClick={() => restore.mutate(projectId)}
          >
            {restore.isPending ? t("project.restore.pending") : t("project.restore.submit")}
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("project.archive.heading")}</h2>
          <p className="text-xs text-muted-foreground">{t("project.archive.hint")}</p>
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="archive-confirm">{t("project.archive.confirmLabel")}</Label>
          <Input
            id="archive-confirm"
            value={confirmName}
            onChange={(event) => setConfirmName(event.target.value)}
            placeholder={projectName ?? ""}
          />
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="destructive"
            type="button"
            // Enabled only on an exact match: the button itself teaches what the server checks.
            disabled={archive.isPending || confirmName !== projectName}
            onClick={() => archive.mutate({ projectId, confirmName })}
          >
            {archive.isPending ? t("project.archive.pending") : t("project.archive.submit")}
          </Button>
        </div>

        {archive.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("project.archive.failed")}
          </p>
        )}
      </CardContent>
    </Card>
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
