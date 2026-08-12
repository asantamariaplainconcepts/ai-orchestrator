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
  // Shared verbs. "Cancel" belongs to no feature: every dialog that can be walked away from needs
  // it, and a per-feature copy of the same word is how two dialogs end up disagreeing about it.
  "common.cancel": "Cancel",
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
  // The review queue (inbox-open-prs). A different kind of wait from a Run's: answered on the
  // vendor, so the action verb says it leaves the product.
  "inbox.changes.heading": "Waiting for review",
  "inbox.changes.review": "Review",
  "inbox.changes.byARun": "by a Run",
  "inbox.changes.refused": "could not read the repository —",
  // Launching a Run on a change (run-on-a-pr): the instruction is the whole point, so the
  // dialog leads with it, and the explainer says where the work lands.
  "inbox.changes.run": "Run on this\u2026",
  "inbox.changes.runTitle": "Run on change",
  "inbox.changes.runExplainer":
    "An Agent will work on this change's own branch and push to it \u2014 the same pull request updates. One active Run per change.",
  "inbox.changes.instruction": "What should it do?",
  "inbox.changes.instructionPlaceholder": "apply the review comments about naming\u2026",
  "inbox.changes.launch": "Launch the Run",
  "inbox.changes.launching": "Launching\u2026",
  "inbox.changes.launchFailed": "Could not launch the Run.",
  "run.field.change": "Change",
  "run.section.instruction": "Instruction",
  // The failure banner (turn 7): the reason with its decisions and, when mapped, its remedy.
  "run.failure.title": "This Run failed",
  "run.failure.unknown": "No reason was recorded.",
  "run.failure.remedy.settings": "Add it in Connector settings \u2192",
  "run.failure.remedy.automations": "Check the prompt on the Automations tab \u2192",
  // The diff at reading width (turn 7): per-file collapse and honest pagination.
  "run.changes.collapse": "Collapse",
  "run.changes.expand": "Expand",
  "run.changes.showMore": "Show",
  "run.changes.moreLine.one": "more line",
  "run.changes.moreLine.other": "more lines",
  "run.changes.file.one": "file",
  "run.changes.file.other": "files",
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
  // A line the writer cut at its column width (RunLogWriter): the Agent wrote more than this
  // record can hold, so the fragment is labelled rather than passed off as the whole of it.
  "run.transcript.truncated": "truncated",
  "run.transcript.truncatedTitle":
    "This line was longer than one log entry can hold, so its end was cut.",
  // Steps (turn 10 ②): step_start/step_finish stopped being rows and now delimit a block. A step
  // that never spoke is named by its position, because inventing a description would be a claim.
  "run.transcript.step": "Step",
  "run.transcript.stepCount.one": "step",
  "run.transcript.stepCount.other": "steps",
  "run.transcript.toolCount.one": "tool call",
  "run.transcript.toolCount.other": "tool calls",
  "run.transcript.toolsInStep.one": "tool",
  "run.transcript.toolsInStep.other": "tools",
  "run.transcript.stepFailed": "failed",
  // The verbatim view (turn 10 ④): completeness is kept available, never imposed (design D5).
  "run.transcript.viewReadable": "Readable",
  "run.transcript.viewRaw": "Raw",
  "run.transcript.viewLabel": "How to show the output",
  "run.log.live": "live",
  "run.log.error": "The log cannot be followed right now. The run itself is unaffected.",
  "run.log.none": "This run produced no output.",
  // The preview (run-previews): live while the Run is, gone with it. The copy names whose code
  // is being rendered, because a Member is looking at an application an Agent wrote — and a
  // preview is not a place to type anything you would not hand to it.
  "run.preview.heading": "Preview",
  "run.preview.whose": "the Agent's own application, running in its sandbox",
  // Said once, when it ends under somebody's eyes: the window is gone because its sandbox is,
  // and the Run's own record is what remains.
  "run.preview.ended":
    "This Run finished, so its preview closed with the sandbox it ran in. Its output and file changes are below.",
  // The terminal (#304): a shell beside the agent, in the Run's own sandbox. Each refusal has its
  // own sentence because each has its own remedy — asking for access does not help a habitat that
  // hosts no terminal, and no habitat helps a Run that has already finished.
  "run.terminal.heading": "Terminal",
  "run.terminal.whose": "a shell in this run's sandbox, beside the agent",
  "run.terminal.open": "Open a terminal",
  "run.terminal.connecting": "Opening a shell\u2026",
  "run.terminal.unhosted":
    "Terminals are not hosted here. A run's sandbox can be opened when the agent runs on this machine, not when it runs in the cloud.",
  "run.terminal.forbidden":
    "You do not have permission to open a terminal on this run. Its output and file changes are still yours to read.",
  // Said once, when it ends under somebody's eyes — the same moment the preview has, for the same
  // reason: the shell is gone because its sandbox is.
  "run.terminal.ended":
    "This run finished, so its sandbox and your shell went with it. Its output and file changes are below.",
  "run.terminal.fixedSize":
    "Sized to this window when it opened \u2014 resizing the window will not reflow it, but reopening will.",
  // Said by the pane itself, about the shell rather than about the Run \u2014 a shell ends for reasons a
  // Run knows nothing about (it exited, the sandbox went, the connection dropped), and until this
  // existed the footer said "sized to this window" over a terminal that was already dead.
  "run.terminal.shellEnded": "This shell has ended. Open a terminal again to start a new one.",
  // A refusal, not an ending. The hub's own sentence is already in the terminal above and says what
  // to do about it, so this only has to stop the footer claiming a live shell.
  "run.terminal.notOpened": "The terminal could not be opened — the reason is above.",
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
  // The essentials-first form (#220): everything with a default or a minority audience folds
  // behind one disclosure, and the credential's second path is a link rather than a peer field.
  "connector.advanced": "Advanced",
  "connector.advanced.locked":
    "A local folder needs its path, so these settings stay open while one is chosen.",
  "connector.credential.useName": "Name an existing secret instead",
  "connector.credential.usePaste": "Paste a token instead",
  "connector.credential.cannotStore":
    "This deployment cannot store a pasted token, so name one it can already resolve.",
  "connector.codeRepository.notApplicable":
    "No code repository: a local Run leaves a branch on this machine and opens no pull request.",

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
  // #247 — the habitat withheld the locus; the declared sentence follows this label verbatim.
  "connector.codeSource.unavailable": "Local folders are not available on this deployment:",
  "connector.codeSource.sandboxConstraint":
    "A local folder only works with the Local runtime — an Agent in a sandbox cannot see this machine's disk. Runs of this project default to Local.",

  // Where code executes (#211): one vocabulary for the projects list badge and the Run chip.
  "locus.local": "Local",
  "locus.sandbox": "In a sandbox",

  // Run now's locus choice (#211, mock 3b) — each card states its consequences, and the primary
  // button repeats the choice so nobody is surprised about where work executed.
  "runs.runNow.dialogTitle": "Run now —",
  "runs.runNow.dialogHint": "Choose the Automation and where the Agent executes.",
  "runs.runNow.automation": "Automation",
  "runs.runNow.whereItExecutes": "Where it executes",
  "runs.locus.local.title": "On this machine",
  "runs.locus.local.description":
    "Runs the Agent as a local process against this folder. Fast, no image pull; uses your local CLI credentials:",
  "runs.locus.sandbox.title": "In a sandbox",
  "runs.locus.sandbox.unavailable":
    "An isolated machine of its own, with the repository cloned fresh. Requires a repository code source — unavailable for a local folder.",
  "runs.runNow.confirmLocal": "Run on this machine",
  "runs.runNow.confirmSandbox": "Run in a sandbox",

  // Run detail's Execution block (#211, mock 3c) — both kinds read the same page. Since turn 10b
  // these rows live in Details: one card, no heading spent on a single fact.
  "run.section.execution": "Execution",
  "run.field.runtimeKind": "Runtime",
  // Turn 10b: "Where" carries what the Execution card's heading and Runtime row said between them,
  // and Finished replaces Created + Dispatched with the answer a reader was subtracting them for.
  "run.field.where": "Where",
  "run.field.finished": "Finished",
  "run.field.started": "Started",
  "run.field.workingFolder": "Working folder",
  "run.field.branchCreated": "Branch created",
  "run.execution.localProcess": "Local process on this machine",
  "run.execution.containerJob": "In a sandbox of its own",
  "run.execution.localOutput": "Local branch — no pull request; review it in your editor.",
  "run.execution.sandboxOutput": "Pull request — see Details.",

  // The self-host posture as an environment chip (design review 5a) — the permanent banner
  // treated the primary mode as an anomaly; the chip states the facts once, in the sidebar's
  // footer, and the popover carries the warning said well.
  "env.thisMachine": "This machine",
  "env.ownerNoSignIn": "owner · no sign-in",
  "env.selfHostedTitle": "Self-hosted on this machine",
  "env.identity": "Identity",
  "env.identityValue": "Local owner — every action is admin",
  "env.listeningOn": "Listening on",
  "env.viewRuntimes": "View Agent runtimes",
  "env.viewSandboxes": "View this machine’s sandboxes",
  "env.networkWarning": "Keep this port off the internet — there is no sign-in to stop anyone.",
  // The banner that remains (5a): the real hazard, not the posture. Shown only when the page
  // was reached from another machine while every caller is the administrator.
  "env.exposedBanner":
    "Reached from another machine with no sign-in — anyone who can reach this port is the administrator. Keep it on trusted networks, or bind it to localhost.",
  "env.exposedDismiss": "Dismiss for this session",

  // The Agent runtimes page (#279).
  "runtimes.title": "Agent runtimes",
  "runtimes.onThisMachine": "on this machine",
  "runtimes.loading": "Loading…",
  "runtimes.error": "Could not load the Agent runtimes.",
  "runtimes.notHosted":
    "This deployment does not execute Runs on this machine, so there is nothing to watch here.",
  // The runtimes' machine question (#279): an Automation can be unable to run because the
  // still unable to run, because the runtime's CLI is absent or its named secret resolves to
  // nothing. The remedy command comes from the API — the same pinned sentence the failure
  // reason carries — so the panel and the failure cannot drift.
  "runtimes.heading": "Agent runtimes",
  "runtimes.empty": "No runtimes are registered.",
  "runtimes.ready": "Ready",
  "runtimes.cliMissing": "CLI not installed",
  "runtimes.cliMissingBody":
    "The command isn't on this machine's PATH. Runs using this runtime fail until it exists — install it once and they run again.",
  "runtimes.secretMissing": "Secret missing",
  "runtimes.secretMissingBody":
    "The named secret doesn't resolve in this machine's store. Add it, or clear the runtime's credential setting to use this machine's own session.",
  "runtimes.sessionAuth": "this machine's session",
  // #288: on a machine you are signed into, "secret missing" is the confusing half of the
  // truth. This state says the honest thing — the login exists, it just cannot be copied to
  // where the agent runs — and the reason itself comes from the host, which knows why.
  "runtimes.sessionCantTravel": "Session can't travel",
  "runtimes.secret": "secret",
  // Where the agents run is chosen when the server starts, and the rows above describe THAT
  // machine — so it is stated before them rather than left to inference. A host that isn't
  // ready makes every runtime below it moot, which is why its remedy comes first.
  "runtimes.hostLabel": "Agents run in",
  "runtimes.hostNotReady": "Not reachable",
  "runtimes.hostNotReadyBody":
    "Runs can't start until this is fixed — the runtimes below describe that machine, so their state is unknown while it can't answer.",
  // Composed: "Checked 20s · retries every 30s".
  "runtimes.checked": "Checked",
  "runtimes.retries": "retries every",

  // This machine's own sandboxes (#311) — the generalisation of the Run's terminal. Machine-scoped
  // like the runtimes panel beside it, because the sandbox most worth entering is the one a killed
  // process left behind, and that one belongs to no project.
  "sandboxes.title": "Sandboxes",
  "sandboxes.whose": "the sandboxes this product has created on this machine",
  "sandboxes.loading": "Loading…",
  "sandboxes.error": "Could not load this machine's sandboxes.",
  // The habitat's answer, not a permission: asking for access would not help (ADR-0021).
  "sandboxes.notHosted":
    "This deployment does not execute Runs on this machine, so it hosts no terminal. A sandbox can be opened where the Agent runs on this machine, and not where it runs in the cloud.",
  "sandboxes.forbidden":
    "You do not have permission to open a terminal on this machine's sandboxes.",
  "sandboxes.none": "No sandboxes are running on this machine.",
  "sandboxes.open": "Open a terminal",
  // Composed with the sandbox's name for the accessible name — a page of identical button labels
  // is a page a screen reader cannot tell apart.
  "sandboxes.openOn": "Open a terminal on sandbox",
  "sandboxes.itsRun": "used by a Run",
  "sandboxes.noRun": "no Run is using it",
  // Said before the click, never after: `sbx exec` on a stopped sandbox starts it, so an unwarned
  // reader would boot a virtual machine while looking for one.
  "sandboxes.startsIt": "This sandbox is stopped. Opening a terminal starts it.",

  // The queued Run's cross-link (5c): no destructive styling on the Run — the cause lives on
  // the panel, the Run only points at it.
  "run.queuedRuntimes": "Waiting for the Agent runtimes —",
  "run.queuedRuntimes.seeWhy": "see why",

  "ui.copy": "Copy",
  "ui.copied": "Copied",

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
  // #226 — neither allowed nor refused: the vendor will not answer without us performing it.
  "connector.test.unknown": "Not checkable",
  "connector.requiredPermissions": "Grant this token:",

  // BR-010's split, said where the name is typed (design review 5d): the name lives in the
  // product, the value in the host's environment — with the exact line and a live answer.
  "secret.envExplainer":
    "The name lives here; the value lives in the host's environment. Add this line beside your compose file, then restart:",
  "secret.checking": "Checking whether it resolves…",
  "secret.resolves": "Resolves on this machine.",
  "secret.notYet": "Doesn't resolve yet — add it to the environment and restart compose.",
  "secret.looksLikeToken":
    "This looks like the token itself. The name goes here; the value goes in the environment.",
  "secret.localNote":
    "Your working copy is your own — nothing here clones or pushes with this token.",

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
  // The claimed transition and the marks, named as the two things they became (#310). One
  // single-valued field for the transition, because a second one is unrepresentable; a set beside it
  // for labels that move nothing.
  "automations.toStage": "Next stage",
  "automations.toStagePlaceholder": "ai:propose",
  "automations.toStageHint":
    "The stage a Story reaches when this succeeds. Naming one that is not a stage yet adds it to the flow, right after this step.",
  "automations.marks": "Also label the Story",
  "automations.marksHint":
    "Labels applied alongside the move, for people and tools to read. They are not stages, so the board draws no column for them.",
  "automations.outputLabelPlaceholder": "needs-design",
  "automations.outputLabelAdd": "Add",
  "automations.outputLabelRemove": "Remove",
  "automations.delete": "Delete",
  // Two presses, not one (design review 6b). The first opens the question, the second answers it —
  // and the second says what it does, because "Confirm" beside "Delete" leaves the reader deducing
  // which of the two buttons is the dangerous one.
  "automations.delete.start": "Delete…",
  "automations.delete.confirm": "Delete it permanently",
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
  // Editing arrives in a dialog (design review 6b), so the title has to name what is being edited —
  // an inline form was identified by where it sat on the page, and a dialog has no "where".
  "automations.editTitle": "Edit",
  // What a catalogue row says about the flow (design review 6a). The relation, not a repeat of the
  // fields the row already shows — "is this on the flow?" is the question the catalogue answers, and
  // since #310 the answer is read from the stored lifecycle rather than derived from labels.
  "automations.inWorkflow": "in workflow",
  "automations.standalone": "standalone",
  // Occasional tools, not permanent content: first-run setup and a scratchpad are things you reach
  // for, so they live in the toolbar and open over the tab rather than pushing the flow down.
  "automations.tools.tryPrompt": "Try a prompt",
  "automations.tools.setup": "Set up from repo…",
  "automations.tools.more": "More tools",
  // The canvas is gone (#310, AC 11), and most of its copy with it: the block a person was dragged
  // into a gap, the branch chip, the serialisation warning, the dangling badge and the loop refusal
  // all described a drawing that no longer exists or a capability that is no longer representable.
  // What survives is what the read-only preview and the board's boundary still say.
  // Two named things, not two views of one list (#136, DEC-053).
  "automations.catalogue": "Catalogue",
  "automations.catalogue.hint":
    "Every Automation this project has. One that claims a transition of the flow also appears on the board, on the boundary it claims.",
  "automations.workflow": "Workflow",
  "automations.workflow.empty":
    "No Automation claims a transition yet, so this project has no flow. Give one a next stage and it appears here \u2014 and as a column on the board.",
  "automations.workflow.stages.one": "stage",
  "automations.workflow.stages.other": "stages",
  "automations.workflow.stops.one": "human review",
  "automations.workflow.stops.other": "human reviews",

  "automations.adding": "Saving\u2026",
  "automations.saveFailed": "Could not save the Automation.",
  "automations.trigger": "Trigger label",
  "automations.triggerPlaceholder": "ai:implement",
  "automations.state": "Story state",
  "automations.statePlaceholder": "any state",
  "automations.action": "Action",
  "automations.runtime": "Runtime",
  // The model an Automation's Runs think with (#291). Three states, because they are three
  // different things to learn: the runtime listed them, an operator declared them, or the machine
  // could not be asked — and only the last one is not an answer about the runtime.
  // Drag-to-chain (design review turn 8) moved to the board's boundary with the arrangement itself
  // (#310), and the sentences that described the canvas's own slots went with it. The one discipline
  // survives: every refusal is said BEFORE the drop, at the boundary the pointer is over, because a
  // rule learned from a toast afterwards is a rule learned too late.
  // Each refusal names the rule rather than the symptom, and each is said at the boundary before the
  // gesture rather than in a toast after it. The loop refusal that used to sit here is gone (#310): a
  // lifecycle is a linear ordered list of stages, so there is no arrangement a person can express
  // that leads back to where it started, and the sentence had nothing left to describe.
  "board.boundary.refuseShared":
    "already fires on another row — two enabled Automations cannot share a trigger (BR-003). Disable the other one or change its label.",
  "board.boundary.refuseSelf": "cannot hand work to itself.",
  // The read-only preview, now on the Automations tab (#310, design D7): the stored stage list
  // painted as the columns it becomes on the board.
  "preview.title": "Board preview",
  "preview.hint": "what this workflow makes of the Backlog tab",
  "preview.live": "updates live as you wire",
  "preview.untouched": "Untouched",
  "preview.untouchedHint": "where Stories start",
  "preview.noApproval": "runs without approval",
  "preview.gate": "plan approved by a person",
  "preview.person": "A person",
  "preview.personHint": "carries the work onward",
  "preview.show": "Show board preview",
  "preview.hide": "Hide board preview",
  "automations.model": "Model",
  "automations.modelDeploymentDefault": "Deployment default",
  "automations.modelPlaceholder": "Leave blank to inherit",
  "automations.modelPickRuntimeFirst": "Choose a runtime and its models appear here.",
  "automations.modelAsking": "Asking the machine that runs agents\u2026",
  "automations.modelEnumerated":
    "These are the models that machine can reach right now, asked of it directly.",
  "automations.modelDeclared":
    "This runtime cannot list its models, so these are the ones configured for it.",
  "automations.modelNoneDeclared":
    "This runtime cannot list its models and none are configured, so type one. Blank inherits.",
  "automations.modelCouldNotAsk":
    "The machine that runs agents could not be asked, so its models are unknown \u2014 this is not a runtime without models. Type one, or leave blank to inherit.",
  "automations.runtimeProjectDefault": "Project default",
  "automations.approval": "Needs approval",
  "automations.timeout": "Timeout (minutes)",
  "automations.anyState": "any state",
  // Honest about the catalogue: three actions are configurable and cannot execute yet, and
  // saying so is the whole reason for shipping the catalogue whole (design D3).
  "automations.actionNotExecutable": "Not executable yet",
  // The three questions (#231). Numbered in the Automation's own execution order — matching reads
  // the trigger, the executor reads the prompt, HandOn applies the labels — so filling the form top
  // to bottom walks a Run.
  "automations.q1": "When does it fire?",
  "automations.q2": "What does it do?",
  "automations.q3": "What happens after?",
  // The restatement, not a second validation channel (design D2): an incomplete form yields an
  // incomplete sentence naming what is missing, and the field refusals stay the only rejection.
  "automations.sentence.prefix": "When a story is labelled",
  "automations.sentence.anyState": "in any state",
  "automations.sentence.inState": "in state",
  "automations.sentence.runs": "an agent runs",
  "automations.sentence.on": "on",
  "automations.sentence.gated": "waits for a human to approve the plan, then",
  "automations.sentence.handsOn": "and moves the story on to",
  "automations.sentence.stops": "and the flow stops there",
  // The marks as their own clause (#310): the move is one fact, a label the vendor carries is
  // another, and one sentence saying both is how the two stopped being told apart.
  "automations.sentence.marks": "also labelling it",
  "automations.sentence.missingTrigger": "\u2026 (name a trigger label)",
  "automations.sentence.missingStage": "\u2026 (name the stage it moves on to)",
  "automations.sentence.missingPrompt": "\u2026 (name a prompt file)",
  // Approval states its consequence beside the execution it gates, not beside Save.
  "automations.approvalExplainer":
    "The agent plans, stops, and waits in the Inbox. Nothing executes until someone approves.",
  // An absence made into an answer (design D4): the stored value is a claim on no transition, which
  // is what "stop" has always meant.
  "automations.after.handOn": "Move the story to the next stage",
  "automations.after.handOnHint":
    "It claims the transition out of its own trigger, so the next Automation picks the story up there.",
  "automations.after.stop": "Stop \u2014 a person takes over",
  "automations.after.stopHint": "The story stays where it is until somebody moves it on.",
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
  // The whole workflow in one press (#229). The copy carries the propose-then-confirm shape,
  // because a button that reconfigured a repository on the first click would be the wrong button.
  "workflowSetup.title": "Set up the whole workflow",
  "workflowSetup.explainer":
    "Look for the prompts your repository already has and wire an Automation to each one. To install a workflow you do not have yet, turn it on below — its prompts and the documents they read arrive together as one draft pull request.",
  "workflowSetup.look": "Look for a pipeline",
  "workflowSetup.looking": "Reading the repository\u2026",
  "workflowSetup.lookFailed": "Could not read the repository.",
  "workflowSetup.foundNothing": "No prompt files found. Looked in:",
  "workflowSetup.wires": "Wires:",
  "workflowSetup.notWired": "Found, not wired:",
  "workflowSetup.choose": "Use this",
  "workflowSetup.chosen": "Using this",
  // The plan, said before the button (#233). It replaces a checkbox that was standing in for a
  // preview: the rows say which files get installed, so the toggle had nothing left to communicate.
  "workflowSetup.planTitle": "What this will create",
  "workflowSetup.wireTo": "wires",
  "workflowSetup.foundInRepo": "already in the repository",
  "workflowSetup.installStarter": "installs a starter",
  "workflowSetup.exists": "In repo",
  "workflowSetup.gate": "a person approves",
  "workflowSetup.planMore": "more",
  "workflowSetup.planFewer": "Show fewer",
  "workflowSetup.draftSafety":
    "Starters land on a branch as a draft pull request \u2014 nothing is committed to your default branch.",
  // Selecting what actually gets built (#262). Every row starts selected: the plan is a checklist
  // of what will happen, and a preview you cannot change is a notice rather than a decision.
  "workflowSetup.includeStep": "Include",
  // The plan speaks in transitions since #310: a wired step claims one, and the stages a project's
  // lifecycle is made of come into existence as a consequence of claiming.
  "workflowSetup.movesTo": "moves stories on to",
  "workflowSetup.flowEnds": "the flow ends here",
  "workflowSetup.handoffBroken": "nobody moves stories into this \u2014 a person will",
  "workflowSetup.nothingSelected": "Select at least one step to build.",
  "workflowSetup.build": "Build the workflow",
  "workflowSetup.building": "Building\u2026",
  "workflowSetup.buildFailed": "Could not build the workflow.",
  "workflowSetup.readsFrom": "Prompts read from",
  "workflowSetup.created": "Created",
  "workflowSetup.skipped": "Skipped",
  // Its own fact, never folded into "Skipped": that word means the project already had it, and
  // this one means you chose otherwise.
  "workflowSetup.excluded": "Excluded",
  "workflowSetup.found": "Found, not wired",
  "workflowSetup.missing": "No file yet",
  "workflowSetup.installed": "Draft pull request opened \u2014 review and merge it:",
  "workflowSetup.installFailed": "Could not install the missing starters:",
  // Consenting to a workflow (#269). Off by default and stated before it is given, because this
  // control authorises writes *outside* the prompt directory \u2014 which no plan row names, and which is
  // why it is not the "confirmation of a confirmation" #262 deleted.
  "workflowSetup.adopt": "Install this workflow",
  "workflowSetup.adoptNeeds": "What it needs, and what this writes:",
  "workflowSetup.adoptWritesWhereAbsent":
    "Written only where your repository has no file at that path \u2014 anything you already have is left exactly as it is.",
  "workflowSetup.nothingToBuild":
    "Nothing to build yet \u2014 install a workflow above, or point this at a directory that already holds prompts.",
  "workflowSetup.prerequisites": "Also written",
  "workflowSetup.prerequisitesKept": "Already yours, left alone",
  "project.tab.ask": "Ask",
  "shell.crumb.projects": "Projects",
  "shell.breadcrumbs": "Breadcrumbs",
  "project.title.fallback": "Project",
  // The project page's tabs (dashboard-tabs): operating and configuring are different jobs.
  "project.tab.operate": "Operate",
  // The board (#110): dropping a card into a column IS applying its trigger label (UC-008).
  "board.showBoard": "Board view",
  "board.showList": "List view",
  // The boundary between two columns (#310): the transition into the right-hand stage, which at most
  // one Automation claims. The human wait used to be a column of its own; it is a boundary now,
  // because the Story does not move until somebody moves it \u2014 it stays in the stage it is in.
  //
  // BR-006 governs every word here: an unclaimed boundary states who acts, never that something is
  // wrong and never how long it has been waiting.
  "board.boundary.person": "A person",
  "board.boundary.personHint":
    "carries the work across here. Nothing moves a story on until somebody does.",
  "board.boundary.firstHint":
    "starts the flow by labelling a story. Put an Automation here to have one move stories in.",
  "board.boundary.claimed": "moves stories into this stage when it succeeds.",
  "board.boundary.assign": "Move an Automation here\u2026",
  // The same control, said short enough to fit the boundary's lane. Observed clipped to "Move an
  // Automation he" in the browser at every width (ADR-0001); the accessible name keeps the sentence.
  "board.boundary.assignShort": "Move one here\u2026",
  "board.boundary.clear": "Require a person here",
  "board.boundary.move": "Drag to another boundary, or use the control below",
  "board.boundary.wouldMoveTo": "would move stories on to",
  "board.boundary.refuseAlready": "already claims this transition \u2014 it is where it is.",
  "board.flowEnds": "The flow ends here.",
  "board.arrangeFailed": "That change was refused. Nothing was saved.",
  "board.untouched": "Untouched",
  "board.columnEmpty": "Nothing here.",
  "board.moveTo": "Move to\u2026",
  "board.cardActions": "Card actions",
  "board.gated": "Approval",
  "board.gated.hint": "Dropping here starts a plan for a human to approve.",
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
  // Beside the cost, because a cost figure cannot be compared to another without it (#291).
  "run.field.model": "Model",
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
  // #244: the runtime choice at launch. "Project default" is the honest pre-selection for an
  // Automation with no explicit runtime — the default itself resolves at execution time.
  // The launch's model, "for this Run only" like the runtime beside it (#291). Empty means the
  // resolution — the Automation's model, or the deployment's — decided at execution time.
  "runs.runNow.resolvedModel": "As resolved",
  "runs.runNow.projectDefaultRuntime": "Project default",
  // The Project's runtime settings (#244). Names, never values (BR-010); Admin-only (BR-009).
  "projectRuntimes.heading": "Runtimes",
  "projectRuntimes.explainer":
    "The runtime an Automation without one resolves to, and the credential each runtime bills to. Changing the default changes future Runs \u2014 no Automation is edited.",
  "projectRuntimes.loading": "Loading runtime settings\u2026",
  "projectRuntimes.unavailable": "Runtime settings are managed by project Admins.",
  "projectRuntimes.default": "Default runtime",
  "projectRuntimes.deploymentDefault": "Deployment default",
  "projectRuntimes.defaultHint":
    "Applies at execution time to every Automation whose runtime is \u2018Project default\u2019.",
  "projectRuntimes.credentials": "Credential names",
  "projectRuntimes.credentialPlaceholder": "secret-name-in-the-vault",
  "projectRuntimes.credentialHint":
    "A secret name this deployment can resolve \u2014 the value never passes through here. Blank falls back to the deployment\u2019s credential.",
  "projectRuntimes.save": "Save runtime settings",
  "projectRuntimes.saving": "Saving\u2026",
  "projectRuntimes.saveFailed": "Could not save the runtime settings.",
  "runs.runNow.confirm": "Run now",
} as const;

export type TranslationKey = keyof typeof en;
