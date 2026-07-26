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
} as const;

export type TranslationKey = keyof typeof en;
