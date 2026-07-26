/**
 * The English copy catalog — the single home for user-facing text (DEC-021).
 * Adding a key here is what makes it usable; the lint gate rejects literals in JSX.
 */
export const en = {
  "app.title": "AI Orchestrator",
  "projects.heading": "Projects",
  "projects.subtitle": "Each project connects a backlog and runs its Automations.",
  // Empty states name the absence, then the next action (content fundamentals).
  "projects.empty": "No projects yet. Create one to connect a backlog.",
  "projects.create.name": "Project name",
  "projects.create.placeholder": "Phoenix",
  "projects.create.submit": "Create project",
  "projects.create.pending": "Creating…",
  "projects.loading": "Loading projects…",
  "projects.error": "Could not load projects.",
  "projects.count.one": "project",
  "projects.count.other": "projects",
  "theme.toggle": "Switch theme",

  // Backlog — copy follows the content fundamentals: sentence case, verb-first buttons,
  // the documented empty/error patterns, and the locked vocabulary (Story, Connector).
  "backlog.heading": "Backlog",
  "backlog.loading": "Loading backlog\u2026",
  "backlog.error": "Could not load the backlog.",
  "backlog.empty": "No open Stories in this repository.",
  // Distinct from the above on purpose: nothing has been read yet, so "no Stories" would be a
  // claim we have not earned.
  "backlog.noConnector": "Nothing to show yet. Configure a Connector above to read Stories.",
  "backlog.stale": "Could not read the backlog. Showing the last Stories we saw.",
  "backlog.refresh": "Refresh backlog",
  "backlog.refreshing": "Refreshing\u2026",
  "backlog.count.one": "Story",
  "backlog.count.other": "Stories",
  "backlog.syncedAt": "Last synced",
  "backlog.neverSynced": "Never synced",

  "connector.heading": "Connector",
  "connector.none": "No backlog connected. Configure a Connector to read Stories.",
  "connector.owner": "Owner",
  "connector.ownerPlaceholder": "acme",
  "connector.repository": "Repository",
  "connector.repositoryPlaceholder": "portal",
  "connector.secretName": "Secret name",
  "connector.secretNamePlaceholder": "acme-github-pat",
  "connector.secretHint":
    "The name of the secret holding the access token. The token itself is never stored here.",
  "connector.healthy": "Connected",
  "connector.unhealthy": "Last poll failed",
  "connector.save": "Configure connector",
  "connector.saving": "Verifying\u2026",
  "connector.saveFailed": "Could not save the Connector.",

  "project.back": "All projects",

  // Automations — the locked vocabulary (Automation, Agent, Run) used exactly.
  "automations.heading": "Automations",
  "automations.count.one": "Automation",
  "automations.count.other": "Automations",
  "automations.loading": "Loading Automations\u2026",
  "automations.error": "Could not load Automations.",
  "automations.empty": "No Automations yet. Add one to make a labelled Story trigger an Agent.",
  "automations.add": "Add Automation",
  "automations.adding": "Saving\u2026",
  "automations.saveFailed": "Could not save the Automation.",
  "automations.trigger": "Trigger label",
  "automations.triggerPlaceholder": "ai:implement",
  "automations.state": "Story state",
  "automations.statePlaceholder": "any state",
  "automations.action": "Action",
  "automations.runtime": "Runtime",
  "automations.approval": "Needs approval",
  "automations.timeout": "Timeout (minutes)",
  "automations.anyState": "any state",
  // Honest about the catalogue: three actions are configurable and cannot execute yet, and
  // saying so is the whole reason for shipping the catalogue whole (design D3).
  "automations.actionNotExecutable": "Not executable yet",
  "automations.catalogueHint":
    "All four actions are configurable. Only Implement \u2192 PR can run today; the rest are recorded and will execute when their Agent lands.",
  "automations.table.trigger": "Trigger",
  "automations.table.action": "Action",
  "automations.table.approval": "Approval",
  "automations.table.timeout": "Timeout",
  "automations.approvalRequired": "Required",
  "automations.approvalNone": "Automatic",
  "automations.minutes": "min",

  // Shell — sidebar, top bar. The brand area is catalogue text only (DEC-021).
  "shell.nav.section": "Workspace",
  "shell.nav.projects": "Projects",
  // Honest placeholder: authentication does not exist yet (#12); no identity is invented.
  "shell.user.name": "Not signed in",
  "shell.user.hint": "Sign-in arrives with Entra ID",
  "shell.crumb.projects": "Projects",
  "shell.breadcrumbs": "Breadcrumbs",
  "project.title.fallback": "Project",

  // Backlog stat cards — every value is computed from the live response, nothing else.
  "backlog.stats.total": "Stories",
  "backlog.stats.open": "Open",
  // "Labelled", not "trigger-labelled": no Automation exists yet to define a trigger (#14).
  "backlog.stats.labelled": "Labelled",
  "backlog.stats.connector": "Connector",

  // Runs — UC-021: the loop's output, observable. Locked vocabulary (Run, Story, Automation).
  "runs.heading": "Runs",
  "runs.count.one": "Run",
  "runs.count.other": "Runs",
  "runs.loading": "Loading Runs\u2026",
  "runs.error": "Could not load Runs.",
  "runs.empty": "No Runs yet. They appear when a labelled Story matches an Automation.",
  "runs.emptyForStory": "No Runs for this Story yet.",
  "runs.filteredByStory": "Story",
  "runs.clearFilter": "Show all Runs",
  "runs.table.story": "Story",
  "runs.table.automation": "Automation",
  "runs.table.state": "State",
  "runs.table.created": "Created",
  "runs.table.dispatched": "Dispatched",
  "runs.table.output": "Output",
  "runs.table.cost": "Cost",

  // Table headers.
  "backlog.table.id": "#",
  "backlog.table.title": "Title",
  "backlog.table.labels": "Labels",
  "backlog.table.state": "State",
  "backlog.table.runs": "Runs",
  "backlog.table.viewRuns": "View Runs",
} as const;

export type TranslationKey = keyof typeof en;
