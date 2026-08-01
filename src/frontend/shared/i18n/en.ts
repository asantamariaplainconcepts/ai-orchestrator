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
  "ui.close": "Close",
  "shell.auth.signIn": "Sign in",
  "shell.auth.signOut": "Sign out",
  "shell.nav.collapse": "Collapse sidebar",
  "shell.nav.expand": "Expand sidebar",
  "shell.nav.openMenu": "Open navigation",

  // Backlog — copy follows the content fundamentals: sentence case, verb-first buttons,
  // the documented empty/error patterns, and the locked vocabulary (Story, Connector).
  "backlog.heading": "Backlog",
  "backlog.loading": "Loading backlog\u2026",
  "backlog.error": "Could not load the backlog.",
  "backlog.empty": "No open Stories in this repository.",
  // Distinct from the above on purpose: nothing has been read yet, so "no Stories" would be a
  // claim we have not earned.
  // "above" was true on the single-scroll page; the Connector now lives on Settings, and a
  // direction that no longer holds is worse than no direction.
  "backlog.noConnector":
    "Nothing to show yet. Connect a backlog on the Settings tab to read Stories.",
  "backlog.stale": "Could not read the backlog. Showing the last Stories we saw.",
  "backlog.refresh": "Refresh backlog",
  "backlog.refreshing": "Refreshing\u2026",
  "backlog.count.one": "Story",
  "backlog.count.other": "Stories",
  "backlog.syncedAt": "Last synced",
  "backlog.neverSynced": "Never synced",

  "inbox.heading": "Waiting on you",
  "inbox.loading": "Loading\u2026",
  "inbox.error": "Could not load the inbox.",
  "inbox.empty":
    "Nothing is waiting on you. Runs that need an approval, an answer or a decision will appear here.",
  "inbox.justNow": "just now",
  "inbox.reason.approval": "Approve a plan",
  "inbox.reason.input": "Answer a question",
  "inbox.reason.failure": "Decide about a failure",
  // Mid-sentence, after the project and Story id: "Phoenix · #491 · waiting 2h".
  "inbox.waitingFor": "waiting",
  // The verb each row leads with — what following it does, in the Run's own vocabulary.
  "inbox.action.approval": "Review plan",
  "inbox.action.input": "Answer",
  "inbox.action.failure": "Open Run",
  "inbox.table.story": "Story",
  "inbox.table.reason": "Needs",
  "inbox.table.waiting": "Waiting",
  "projects.health.healthy": "Connected",
  "projects.health.failing": "Failing",
  "projects.health.neverSynced": "Never synced",
  "projects.health.notConfigured": "No backlog",
  "projects.health.justNow": "just now",
  "run.section.log": "Output",
  "run.transcript.spend": "Spend so far",
  "run.transcript.unknown": "unknown",
  "run.transcript.in": "in",
  "run.transcript.out": "out",
  "run.log.live": "live",
  "run.log.error": "The log cannot be followed right now. The run itself is unaffected.",
  "run.log.none": "This run produced no output.",
  "run.log.waitingForOutput": "No output yet \u2014 following\u2026",
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

  // Pasting the token (#124). The product names and stores it; nobody has to visit a vault first.
  "connector.credential": "Access token",
  "connector.credential.paste": "Paste a token",
  "connector.credential.name": "Name an existing secret",
  "connector.accessToken": "Token",
  "connector.accessTokenPlaceholder": "github_pat_…",
  "connector.accessTokenHint":
    "Pasted once and stored in this deployment's secret store under a name we choose. It is never shown again, never logged, and never returned by the API — paste a new one to rotate it.",
  "connector.secretSetAt": "Token stored",
  "connector.secretManagedElsewhere": "Token managed outside this product",
  "connector.codeRepository": "Code repository",
  "connector.codeRepositoryPlaceholder": "portal-web",
  "connector.keepsStoredCredential":
    "Leave the credential blank to keep the one already stored — it is re-checked against the vendor before saving.",
  "connector.promptDirectory": "Prompts directory",
  "connector.promptDirectoryPlaceholder": "ai/prompts",
  "connector.promptDirectoryHint":
    "Where this project keeps the prompt files an Automation can name. Leave it blank for ai/prompts.",

  // The code source (#210/#211, mock 3a). "Local folder" is a code source, not a vendor —
  // Stories still come from issues; only where the Agent's working copy comes from changes.
  "connector.codeSource": "Code source",
  "connector.codeSource.repository": "Repository",
  "connector.codeSource.localFolder": "Local folder",
  "connector.codeSource.hint": "Stories still come from issues — only the code is local.",
  "connector.codeSource.folder": "Folder on this machine",
  "connector.codeSource.folderPlaceholder": "/home/you/repos/portal",
  "connector.codeSource.validating": "Checking the folder…",
  "connector.codeSource.notADirectory": "Not a directory on this machine.",
  "connector.codeSource.notAGitRepository": "Not a git repository.",
  "connector.codeSource.valid": "Git repository · branch",
  "connector.codeSource.cleanTree": "clean working tree",
  "connector.codeSource.dirtyTree": "uncommitted changes — a Local Run will refuse to start",
  "connector.codeSource.recent": "Recent folders",
  "connector.codeSource.usedBy": "used by",
  "connector.codeSource.podConstraint":
    "A local folder only works with the Local runtime — an Agent in a pod cannot see this machine's disk. Runs of this project default to Local.",

  // Where code executes (#211): one vocabulary for the projects list badge and the Run chip.
  "locus.local": "Local",
  "locus.pod": "Agent pod",

  // Run now's locus choice (#211, mock 3b) — each card states its consequences, and the primary
  // button repeats the choice so nobody is surprised about where work executed.
  "runs.runNow.dialogTitle": "Run now —",
  "runs.runNow.dialogHint": "Choose the Automation and where the Agent executes.",
  "runs.runNow.automation": "Automation",
  "runs.runNow.whereItExecutes": "Where it executes",
  "runs.locus.local.title": "On this machine",
  "runs.locus.local.description":
    "Runs the Agent as a local process against this folder. Fast, no image pull; uses your local CLI credentials:",
  "runs.locus.pod.title": "In an Agent pod",
  "runs.locus.pod.unavailable":
    "Container job with the repository cloned fresh. Requires a repository code source — unavailable for a local folder.",
  "runs.runNow.confirmLocal": "Run on this machine",
  "runs.runNow.confirmPod": "Run in a pod",

  // Run detail's Execution block (#211, mock 3c) — both kinds read the same page.
  "run.section.execution": "Execution",
  "run.field.runtimeKind": "Runtime",
  "run.field.workingFolder": "Working folder",
  "run.field.branchCreated": "Branch created",
  "run.execution.localProcess": "Local process on this machine",
  "run.execution.containerJob": "Container job in an Agent pod",
  "run.execution.localOutput": "Local branch — no pull request; review it in your editor.",
  "run.execution.podOutput": "Pull request — see Details.",

  // The self-host posture, stated on every screen while it applies (#211, mock 3d).
  "shell.localOwner":
    "Running as local owner — every action is administrator, no sign-in. Trusted networks only.",

  // Close the loop (#211, mock 3d): three derived steps, gone once any Run reaches terminal.
  "onboarding.title": "Close the loop on this machine",
  "onboarding.explainer": "Three steps and a labelled Story becomes a Run you can watch.",
  "onboarding.step.connect": "Connect a backlog",
  "onboarding.step.code": "Point at your code — a repository, or a folder on this machine",
  "onboarding.step.automations": "Set up the Automations, then label a Story",
  "onboarding.state.done": "done",
  "onboarding.state.current": "next",
  "onboarding.state.later": "later",
  "onboarding.go": "Go",
  "connector.codeRepositoryHint":
    "Azure DevOps keeps code in repositories inside the project. Name the one Agents should open pull requests against; leave it empty if no Automation touches code.",
  "connector.secretHint":
    "The name of the secret holding the access token. The token itself is never stored here.",
  // Testing the stored credential (#132): what it can do, not merely whether it polls.
  "connector.test": "Test credential",
  "connector.testing": "Asking the vendor\u2026",
  "connector.test.satisfied": "This credential can do everything the pipeline needs.",
  "connector.test.refused": "This credential is missing something the pipeline needs.",
  "connector.test.failed": "Could not test the credential.",
  "connector.test.ok": "Allowed",
  "connector.test.no": "Refused",

  "connector.healthy": "Connected",
  "connector.unhealthy": "Last poll failed",
  "connector.save": "Configure connector",
  "connector.saving": "Verifying\u2026",
  "connector.saveFailed": "Could not save the Connector.",
  "connector.edit": "Edit Connector",
  "connector.cancel": "Cancel",

  // Retiring a project (#121): stops its work, keeps its history.
  "project.archive.heading": "Retire this project",
  "project.archive.hint":
    "Archiving stops its polling, its automations and any new run. Everything it already did stays readable, and you can restore it at any time.",
  "project.archive.confirmLabel": "Type the project's name to confirm",
  "project.archive.submit": "Archive project",
  "project.archive.pending": "Archiving\u2026",
  "project.archive.failed": "Could not archive the project.",
  "project.archived.notice": "This project is archived. It starts no new work.",
  "project.restore.submit": "Restore project",
  "project.restore.pending": "Restoring\u2026",
  "projects.archived.count": "archived",
  "projects.archived.show": "Show archived",
  "projects.archived.hide": "Hide archived",
  "projects.archived.badge": "Archived",

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
  "automations.edit": "Edit",
  "automations.save": "Save changes",
  "automations.saving": "Saving…",
  "automations.timeoutPlaceholder": "30",
  "automations.promptFile": "Prompt file",
  "automations.promptFilePlaceholder": "estimate.md",
  "automations.promptFileHint":
    "A file name inside this project's prompts directory, which is set on the Settings tab.",
  "automations.promptSuggestionsUnavailable": "Suggestions unavailable",
  "automations.promptSuggestionsEmpty": "No prompt files yet in",
  "automations.outputLabel": "Output labels",
  "automations.outputLabelPlaceholder": "the next step\u2019s trigger",
  "automations.outputLabelAdd": "Add",
  "automations.outputLabelRemove": "Remove",
  "automations.outputLabelHint":
    "Applied to the Story when a Run of this Automation succeeds \u2014 add as many as you need, or leave empty to end here.",
  "automations.delete": "Delete",
  "automations.delete.hint":
    "Only possible while no run has used it. Once one has, disable it instead \u2014 runs keep their automation for the audit trail.",
  "automations.delete.refused":
    "That automation has runs, so it cannot be deleted. Disable it instead: it stops triggering and its history stays intact.",
  "automations.heading": "Automations",
  "automations.count.one": "Automation",
  "automations.count.other": "Automations",
  "automations.loading": "Loading Automations\u2026",
  "automations.error": "Could not load Automations.",
  "automations.empty": "No Automations yet. Add one to make a labelled Story trigger an Agent.",
  "automations.add": "Add Automation",
  "automations.new": "New Automation",
  // The canvas (#116): the pipeline as a shape, where an edge is one Automation's output label
  // agreeing with another's trigger label.
  "canvas.hint":
    "Each Automation hands work to the next by writing its label. Where nobody does, a person must.",
  "canvas.block": "Human review",
  "canvas.block.hint": "Drag it into the flow to have a person review what a step produced.",
  "canvas.human": "A person continues",
  "canvas.handsTo": "Hands work to\u2026",
  "canvas.disconnect": "Require a person here",
  "canvas.approval.on": "A person approves the plan",
  "canvas.approval.off": "Runs without approval",
  // Branches (#165): two edges are not two Runs, and the picture cannot say that by itself.
  "canvas.branchesSerialize":
    "A step can hand on to several places, but they do not run at once \u2014 one run per story at a time, so a second match while one is running is skipped, not queued.",
  "canvas.branchFrom": "from",
  "canvas.dangling": "Writes a label nothing listens for:",
  // Two named things, not two views of one list (#136, DEC-053).
  "automations.catalogue": "Catalogue",
  "automations.catalogue.hint":
    "Every Automation this project has. One that hands work to another, or receives it, also appears in the workflow.",
  "automations.workflow": "Workflow",
  "automations.workflow.empty":
    "No Automation hands work to another yet. Give one an output label matching another's trigger and the flow appears here.",
  "automations.workflow.steps.one": "step",
  "automations.workflow.steps.other": "steps",
  "automations.workflow.stops.one": "human review",
  "automations.workflow.stops.other": "human reviews",

  "canvas.changeRefused": "That change was refused. Nothing was saved.",
  "automations.new.close": "Close form",
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
  "shell.nav.inbox": "Inbox",
  // Shown only while the answer has not arrived: sign-in exists now (#12), and what the block says
  // once it has comes from the server, never from here.
  "shell.user.name": "Not signed in",
  "shell.user.hint": "Checking who you are…",
  // Where you have standing, not what it is (#13): roles are per project, so one line in the shell
  // could only be one project's answer.
  "shell.user.project": "project",
  "shell.user.projects": "projects",
  // Roles on a project (UC-002, #13). Never the two bundle names themselves — those come from the
  // server's enum, so a third bundle cannot appear in the form and nowhere else.
  "roles.title": "People",
  "roles.explainer":
    "Admins configure this project. Members watch it, apply trigger labels, start runs, approve plans and cancel them.",
  "roles.nobody": "Nobody has been given a role here yet.",
  "roles.noCandidates":
    "Everybody this deployment has seen already has a role here. Someone new appears once they have signed in.",
  "roles.person": "Person",
  "roles.choosePerson": "Choose someone…",
  "roles.bundle": "Role",
  "roles.grant": "Give role",
  "roles.remove": "Remove",
  "roles.changeFor": "Change role",
  "roles.failed": "Could not change roles.",
  // Asking an agent (#166). Never called a Run anywhere: it occupies nothing and blocks nothing,
  // and copy that borrowed the Run vocabulary would teach the opposite of the design.
  "conversation.title": "Ask the agent",
  "conversation.explainer":
    "Ask about this project, or about one story in it. Each message runs one agent pass and costs what that pass costs.",
  "conversation.subject": "Story (optional)",
  "conversation.subjectPlaceholder": "leave empty to ask about the project",
  "conversation.start": "Start",
  "conversation.about": "About story",
  "conversation.aboutProject": "About this project",
  "conversation.message": "Message",
  "conversation.messagePlaceholder": "why did this fail?",
  "conversation.send": "Send",
  "conversation.thinking": "Thinking\u2026",
  "conversation.you": "You",
  "conversation.agent": "Agent",
  "conversation.passFailed": "This pass failed",
  "conversation.costUnknown": "cost unknown",
  "conversation.atLeast": "at least",
  "conversation.failed": "Could not reach the agent.",
  // Trying a prompt before committing it (#189). The copy carries two things nothing else can:
  // that the text is not saved, and what a trial does not reproduce (design D4). Both would
  // otherwise be learned from a surprise.
  "scratchpad.title": "Try a prompt",
  "scratchpad.prompt": "Prompt",
  "scratchpad.promptPlaceholder": "Paste or write the prompt you are about to commit\u2026",
  "scratchpad.subject": "Story (optional)",
  "scratchpad.subjectPlaceholder": "leave empty to try it against the project",
  "scratchpad.run": "Run once",
  "scratchpad.running": "Running\u2026",
  "scratchpad.explainer":
    "Run prompt text once against this project's repository and read what comes back. One agent pass, and it costs what that pass costs.",
  "scratchpad.notSaved":
    "This text is not saved anywhere. When it does what you want, commit it as a file in the project's prompts directory and name that file on an Automation.",
  "scratchpad.limits":
    "A trial is not quite a Run: an Automation that requires approval runs its prompt in a planning phase this does not reproduce, and a timeout belongs to an Automation this does not have.",
  "scratchpad.answer": "Answer",
  "scratchpad.passFailed": "This pass failed",
  "scratchpad.costUnknown": "cost unknown",
  "scratchpad.failed": "Could not reach the agent.",
  // The starter set (#190). The copy carries the tiering, because a prerequisite read before it is
  // needed is the whole difference between a starter set and a methodology somebody took by mistake.
  "starters.title": "Starter prompts",
  "starters.explainer":
    "Prompts you can put in your repository to get going. Copy the file in yourself, or install one \u2014 it opens a draft pull request you review and merge.",
  "starters.requires": "Requires:",
  "starters.assumes": "Assumes:",
  "starters.saveTo": "Save as",
  "starters.pathUnknown": "Configure a Connector to see where this goes in your repository.",
  "starters.alreadyPresent": "You already have this",
  "starters.show": "Show",
  "starters.hide": "Hide",
  "starters.copy": "Copy",
  "starters.copied": "Copied",
  // Install (#214): verb-first, and the outcome names the human's next step — review the PR.
  "starters.install": "Install",
  "starters.installing": "Opening a pull request…",
  "starters.installed": "Draft pull request opened — review and merge it:",
  "starters.installFailed": "Could not install the starter.",
  "project.tab.ask": "Ask",
  "shell.crumb.projects": "Projects",
  "shell.breadcrumbs": "Breadcrumbs",
  "project.title.fallback": "Project",
  // The project page's tabs (dashboard-tabs): operating and configuring are different jobs.
  "project.tab.operate": "Operate",
  // The board (#110): dropping a card into a column IS applying its trigger label (UC-008).
  "board.showBoard": "Board view",
  "board.showList": "List view",
  // The wait given a place (#128): a step finished and nobody has carried the work on.
  "board.human": "Waiting for a person",
  "board.requirePerson": "Require a person after this step",
  "board.human.empty": "Nothing waiting here.",
  "board.human.hint":
    "This step hands work to nobody, so a person decides whether it continues. Give it an output label on the Automations tab to close the chain.",
  "board.waitedFor": "waiting",
  "board.untouched": "Untouched",
  "board.columnEmpty": "Nothing here.",
  "board.moveTo": "Move to\u2026",
  "board.cardActions": "Card actions",
  "board.gated": "Approval",
  "board.gated.hint": "Dropping here starts a plan for a human to approve.",
  // Composed after the step's own mono label, which is vendor data and cannot live here.
  "board.human.explainer":
    "finished \u2014 a person carries these on. Cards cannot be dropped here.",
  "board.dropToApply": "Drop to apply",
  "board.viewActiveRun": "View the active Run",
  "board.moveFailed": "The vendor refused the move. Nothing changed.",
  "board.refusedActiveRun": "This Story already has an active Run — one at a time.",
  "board.run.executing": "Executing",
  "board.run.question": "Question",
  "board.run.approval": "Plan awaits",
  "board.run.failed": "Failed",
  "board.run.succeeded": "Done",
  "project.tab.runs": "Runs",
  "project.tab.automations": "Automations",
  "project.tab.settings": "Settings",

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
  // Pulse (#108) — the Operate strip. Every number links to the list it summarises.
  "pulse.heading": "Operate",
  "pulse.window": "last 7 days",
  "pulse.waiting.approval": "awaiting approval",
  "pulse.waiting.input": "awaiting an answer",
  "pulse.waiting.failure": "failed, undecided",
  "pulse.executing": "executing",
  "pulse.runs": "Runs",
  "pulse.successRate": "Success rate",
  "pulse.cost": "Agent cost",
  "pulse.timing": "Queue · run",
  "pulse.timing.hint": "mean wait · mean duration",
  "pulse.neverRun.one": "story never run",
  "pulse.neverRun.other": "stories never run",
  "pulse.coverage.full": "every story has run",
  "pulse.terminal.one": "finished run",
  "pulse.terminal.other": "finished runs",
  "pulse.fired": "fired",
  "pulse.failed": "failed",
  "pulse.unused": "unused — delete?",

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
  // Deciding about a failure where the failure is (#145).
  "run.again": "Run again",
  "run.again.pending": "Starting\u2026",
  "run.again.failed": "Could not start another Run.",
  "run.dismiss": "Dismiss this failure",
  "run.dismiss.pending": "Dismissing\u2026",
  "run.dismiss.failed": "Could not dismiss the failure.",
  "run.dismissed": "Dismissed",
  "run.dismiss.hint":
    "Says a person decided this needs no re-run. It leaves your inbox; the Run stays failed and nothing runs again.",
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
  "run.section.details": "Details",
  // The lifecycle as copy: the stepper's stations and the state pill share these names, so the
  // enum's internal spelling never reaches a reader.
  "run.stepper": "Run progress",
  "run.state.queued": "Queued",
  "run.state.planning": "Planning",
  "run.state.awaitingApproval": "Awaiting approval",
  "run.state.executing": "Executing",
  "run.state.awaitingInput": "Waiting for an answer",
  "run.state.succeeded": "Succeeded",
  "run.state.failed": "Failed",
  "run.state.cancelled": "Cancelled",
  "run.step.done": "Done",
  "run.decision.explainer":
    "The plan below is waiting for your decision — nothing runs until you act.",

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
