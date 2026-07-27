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
  "connector.vendor": "Vendor",
  "connector.vendor.gitHub": "GitHub",
  "connector.vendor.azureDevOps": "Azure DevOps",
  "connector.vendor.unexercised": "not yet run against a real organisation",
  "connector.owner": "Owner",
  "connector.organisation": "Organisation",
  "connector.project": "Project",
  "connector.ownerPlaceholder": "acme",
  "connector.repository": "Repository",
  "connector.repositoryPlaceholder": "portal",
  "connector.secretName": "Secret name",
  "connector.secretNamePlaceholder": "acme-github-pat",
  "connector.codeRepository": "Code repository",
  "connector.codeRepositoryPlaceholder": "portal-web",
  "connector.codeRepositoryHint":
    "Azure DevOps keeps code in repositories inside the project. Name the one Agents should open pull requests against; leave it empty if no Automation touches code.",
  "connector.secretHint":
    "The name of the secret holding the access token. The token itself is never stored here.",
  "connector.healthy": "Connected",
  "connector.unhealthy": "Last poll failed",
  "connector.save": "Configure connector",
  "connector.saving": "Verifying\u2026",
  "connector.saveFailed": "Could not save the Connector.",

  "project.back": "All projects",

  // Story detail (UC-022) — the description is where the requirement lives.
  "story.title.fallback": "Story",
  "story.crumb.backlog": "Backlog",
  "story.backToBacklog": "Back to backlog",
  "story.loading": "Loading the Story\u2026",
  "story.error": "Could not load the Story.",
  "story.noDescription": "This Story has no description in the repository.",
  // Documents (UC-023) — three absences, three messages (design D5).
  "story.documents.heading": "Specification",
  "story.documents.loading": "Looking for the change written for this Story\u2026",
  "story.documents.error": "Could not reach the repository to look for documents.",
  "story.documents.noChange": "No pull request references this Story yet.",
  "story.documents.noDocuments": "The linked change adds no markdown documents.",
  "story.documents.contentLoading": "Loading the document\u2026",
  "story.documents.contentError": "Could not read this document from the repository.",
  "story.documents.openChange": "Open the change",

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
    "All four actions execute: Implement \u2192 PR opens a pull request; Refine comments on the Story; Transition changes its state; Estimate labels it and explains itself.",
  "automations.table.trigger": "Trigger",
  "automations.table.action": "Action",
  "automations.table.approval": "Approval",
  "automations.table.timeout": "Timeout",
  "automations.approvalRequired": "Required",
  "automations.approvalNone": "Automatic",
  "automations.minutes": "min",
  // Editing (UC-006): disabling stops future matches; enabling can be refused by BR-003.
  "automations.enable": "Enable",
  "automations.disable": "Disable",
  "automations.disabled": "Disabled",
  "automations.enableFailed":
    "Could not enable it \u2014 its trigger now overlaps another Automation.",
  "automations.table.status": "Status",

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
  // BR-011: a Run that reported nothing is unknown, never zero — free models make 0.00 real.
  "runs.cost.unknown": "unknown",
  "runs.cost.heading": "Agent cost",
  "runs.cost.reported": "across Runs that reported",
  "runs.cost.excluded": "not reported",
  "run.field.tokens": "Tokens",
  "runs.table.openOutput": "View PR",
  "runs.table.open": "Open",
  // The approval gate (UC-013/UC-015).
  "run.title.fallback": "Run",
  "run.crumb.project": "Project",
  "run.loading": "Loading the Run\u2026",
  "run.error": "Could not load this Run.",
  "run.notFound": "This Run no longer exists.",
  "run.section.plan": "Plan",
  // File changes (UC-024) — the diff the Agent produced.
  "run.section.changes": "Changes",
  "run.changes.loading": "Reading the changed files\u2026",
  "run.changes.error": "Could not read the changes from the repository.",
  "run.changes.noChange": "This Run has not opened a pull request.",
  "run.changes.noFiles": "The pull request touched no files.",
  "run.changes.binary": "Binary file \u2014 no diff to show. Open the pull request to inspect it.",
  "run.changes.tooLarge":
    "This diff is too large to render here. Open the pull request to read it in full.",
  "run.plan.none": "This Run has no Plan \u2014 its Automation runs without approval.",
  "run.plan.waiting": "Waiting for your decision.",
  "run.approve": "Approve and run",
  "run.cancel": "Cancel Run",
  "run.cancelling": "Cancelling\u2026",
  "run.cancelFailed": "Could not cancel \u2014 this Run has already finished.",
  "run.reject": "Reject",
  "run.deciding": "Recording your decision\u2026",
  "run.decideFailed": "Could not record the decision.",
  "run.field.state": "State",
  "run.field.story": "Story",
  "run.field.created": "Created",
  "run.field.dispatched": "Dispatched",
  "run.field.approved": "Approved",
  "run.field.output": "Output",
  "run.field.failure": "Failure",

  // Table headers.
  "backlog.table.id": "#",
  "backlog.table.title": "Title",
  "backlog.table.labels": "Labels",
  "backlog.table.state": "State",
  // Trigger-label affordances (UC-008): the label text is vendor data; only the verbs and
  // glyphs are copy.
  "backlog.labels.apply": "Apply this trigger label \u2014 written back to the vendor",
  "backlog.labels.remove": "Remove this trigger label \u2014 written back to the vendor",
  "backlog.labels.failed": "Could not write the label to the vendor. The backlog is unchanged.",
  "backlog.labels.applyGlyph": "+",
  "backlog.labels.removeGlyph": "\u00d7",
  "backlog.table.runs": "Runs",
  "backlog.table.viewRuns": "View Runs",
  "backlog.table.runNow": "Run now",
  // Run now (UC-012): detection bypassed, rules kept — the copy says which rule refused.
  "runs.runNow.button": "Run now",
  "runs.runNow.pending": "Dispatching\u2026",
  "runs.runNow.pickAutomation": "Choose the Automation to run",
  "runs.runNow.conflict": "This Story already has an active Run \u2014 one active Run per Story.",
  "runs.runNow.failed": "Could not dispatch the Run.",
} as const;

export type TranslationKey = keyof typeof en;
