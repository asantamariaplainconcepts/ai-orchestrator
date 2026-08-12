# Capabilities

Actor-scoped, one capability each. Every backlog issue must trace to ≥1 UC
([RULE-003](08-backlog-shaping-rules.md)). States and constraints referenced from
[business rules](05-business-rules.md). IDs are carried from the old corpus unchanged,
including #316's resolution of the UC-024 collision: the file-changes review is **UC-028**
(numbered UC-024 until 2026-08-11), the grill keeps **UC-024**, and both keep their issue
trails.

The authority on current *behaviour* is `openspec/specs/` — this catalog says what exists and
for whom, not how it works.

## Identity & access (BC-005)

- **UC-001 — User signs in.** Via Entra ID as a BFF ([DEC-058](../mvp/10-locked-mvp-decisions.md)),
  landing on their project list.
- **UC-002 — Admin assigns roles.** ACT-001 grants Admin or Member on a project.

## Project configuration (BC-001)

- **UC-003 — Admin creates a Project.**
- **UC-004 — Admin configures the Connector.** Vendor choice (GitHub | AzDO), org/repo or
  org/project coordinates, the credential reference ([DEC-030](../mvp/10-locked-mvp-decisions.md),
  [BR-010](05-business-rules.md)). Saving verifies the credential with a live Connector call.
  The Connector also carries a **code source** — the vendor's repository (default), or a folder
  on the host in self-host ([BR-016](05-business-rules.md), #210); Stories always come from the
  vendor.
- **UC-005 — Admin creates an Automation.** Trigger label/state + action
  ([DEC-026](../mvp/10-locked-mvp-decisions.md), incl. project-written prompts,
  [DEC-057](../mvp/10-locked-mvp-decisions.md)) + runtime + timeout. A step that stops for a
  person marks the hold among its output labels ([BR-007](05-business-rules.md),
  [DEC-067](../mvp/10-locked-mvp-decisions.md)); there is no approval flag. Save fails on trigger
  overlap ([BR-003](05-business-rules.md)).
- **UC-006 — Admin edits, disables or deletes an Automation.** Disabling stops future matches,
  never touches active Runs. Deletion only while no Run has used it (BR-014, #84).

## Backlog (BC-002)

- **UC-007 — Member views the project backlog.** Mirrored stories with state, labels,
  active-run badge.
- **UC-008 — Member applies/removes a trigger label on a Story.** Written back to the vendor
  through the Connector; vendor-side labeling is equivalent ([DEC-027](../mvp/10-locked-mvp-decisions.md)).
- **UC-009 — System polls the backlog.** Per-project poll (default 60 s) refreshes the Mirror
  and emits normalized story events ([DEC-028](../mvp/10-locked-mvp-decisions.md)).
- **UC-010 — System ingests a vendor webhook.** Normalizes into the same event stream as
  polling ([BR-015](05-business-rules.md)).
- **UC-022 — Member opens a Story and reads its detail.** Vendor id, title, state, labels and
  the mirrored description rendered as markdown, sanitised at render, never at rest
  ([BR-008](05-business-rules.md)).
- **UC-023 — Member reads the documents attached to a Story's work.** The markdown its linked
  change adds or modifies, read live at that change's head through the Connector.
- **UC-028 — Member reviews the file changes a Run produced.** *(Numbered UC-024 until
  2026-08-11, #316.)* The files its pull request touched, with the vendor's unified patch.
  Binary and over-large patches state why rather than truncating.

## Dispatch (BC-003)

- **UC-011 — System matches an event and creates a Run.** Honoring one-active-run-per-story
  ([BR-001](05-business-rules.md)), the project cap ([BR-002](05-business-rules.md)) and the hold
  ([BR-007](05-business-rules.md)) — a held Story creates nothing. Every Run is single-phase and
  goes straight to dispatch.
- **UC-012 — Member triggers Run now.** Chosen Story + Automation, bypassing detection,
  honoring every rule ([BR-013](05-business-rules.md)). Also the re-run path for failures. May
  name the execution locus where a genuine choice exists (#210); [BR-016](05-business-rules.md)
  refuses a dirty folder.
- **UC-013 — *Retired.*** Was "Member/Admin reviews a Plan". The plan-then-approve shape it
  described is superseded by the hold ([DEC-067](../mvp/10-locked-mvp-decisions.md),
  [BR-007](05-business-rules.md)): work waits on the Story, and what a person reviews is the
  output a step produced rather than a plan it proposed. The id is retained, never reused
  (stable IDs, #316).
- **UC-014 — Member/Admin cancels a Run.** Queued → discarded; Executing → job terminated
  ([BR-012](05-business-rules.md)).

## Agent execution (BC-004)

- **UC-015 — *Retired.*** Was "Agent produces a Plan (phase 1)". There is no phase 1: every Run
  is single-phase ([DEC-067](../mvp/10-locked-mvp-decisions.md),
  [BR-007](05-business-rules.md)). The id is retained, never reused (stable IDs, #316).
- **UC-016 — Agent implements a Story → PR.** Clones the code source, implements, opens a PR,
  links it on the Run and the Story. On a Local run the output is a local branch — committed,
  never pushed, no PR ([BR-016](05-business-rules.md)).
- **UC-017 — Agent refines/comments a Story.** Analysis, refinement questions, or
  acceptance-criteria draft posted as a story comment.
- **UC-018 — Agent transitions a Story's state.** Via the Connector, per Automation config.
- **UC-019 — Agent estimates a Story.** Sets the estimate field with reasoning comment.
- **UC-020 — Agent reports usage at run end.** Tokens + cost persisted on the Run
  ([DEC-038](../mvp/10-locked-mvp-decisions.md), [BR-011](05-business-rules.md)).
- **UC-024 — Member grills a Story to ready.** A `GrillToReady` Automation interrogates a Story
  against the project's own readiness document ([DEC-048](../mvp/10-locked-mvp-decisions.md),
  seedable per [DEC-064](../mvp/10-locked-mvp-decisions.md)): unmet criteria become questions
  on the Story, answers resume the Run, a met bar becomes a ready label plus a verdict comment.
- **UC-025 — Member triggers a proposal for a ready Story.** A `ProposeSpec` Automation turns a
  ready Story into a documentation pull request through the same publishing pipeline as
  implementation. Chains from UC-024's ready label (#80).

## Observation (BC-003/BC-004)

- **UC-021 — Member views Runs.** Per project and per story: state, timestamps, runtime,
  output link, logs, cost — and where each Run executed: locus, working folder and branch for
  Local runs ([BR-016](05-business-rules.md), BR-014).
- **UC-026 — Member sees everything waiting on a human.** The Inbox: one list across projects
  of every Run awaiting an answer or a failure decision, newest wait first, with an ambient
  count in the shell (#94). *(The approval category is gone with
  [DEC-067](../mvp/10-locked-mvp-decisions.md). A held Story is a wait this list does not yet
  carry — a named follow-up, and until it lands the Inbox under-reports what waits on a
  person.)*
- **UC-027 — Member watches a Run's output while it executes.** The Run page shows the log
  growing with ≤5s lag, stops following on terminal states, serves the same full transcript
  afterwards; a crash preserves every line committed before it (#96,
  [DEC-050](../mvp/10-locked-mvp-decisions.md)).
- **UC-029 — Member opens a terminal on this machine's sandboxes.** Self-host only
  ([DEC-065](../mvp/10-locked-mvp-decisions.md), ADR-0021): a machine-scoped surface lists the
  sandboxes this product created on the machine, each openable in a shell; entering a stopped
  sandbox starts it, saying so beforehand. Every attach records who, when and which sandbox
  (#311).
- **UC-033 — Member sees every project's live work in one panel.** The shell's sidebar is a tree:
  each project a row, with its live work nested beneath it at every width — a Story appears when it
  carries the hold ([BR-007](05-business-rules.md),
  [DEC-067](../mvp/10-locked-mvp-decisions.md)) or has a Run in `Queued`, `Executing` or
  `AwaitingInput`, and that Story's Runs nest under it; a Run targeting an open change nests under
  the change instead. A project with nothing in flight renders as its row alone. Membership is
  derived at read time and bounded by [BR-001](05-business-rules.md) and
  [BR-002](05-business-rules.md); it is scoped by [BR-009](05-business-rules.md), so a project the
  projects list would not show is absent rather than empty. Extends UC-021 and sits **beside**
  UC-026 without replacing it: the tree answers "what is this project doing", the Inbox answers
  "what waits on me", so a held Story deliberately appears in both. Membership is never derived from
  the vendor's own state value, which [DEC-045](../mvp/10-locked-mvp-decisions.md) leaves
  un-normalised permanently (#335).

## Intended — grillable, not yet filed

Capabilities this corpus commits to as direction; each still owes a grill
([RULE-001](08-backlog-shaping-rules.md)..007) before any proposal. Sources and reasoning:
[07-roadmap.md](07-roadmap.md) and the [Orca study](../studies/2026-08-11-orca.md).

- **UC-030 — Member sees what the agent is doing right now.** During `Executing`, the Run
  shows the agent's own reported status — working / blocked / waiting / done — as a timeline,
  reported by the runtime's hook mechanism into the Run, never inferred from log output. Makes
  waits legible before they become `AwaitingInput`, and gives the approval gate its missing
  urgency signal. A runtime without hooks degrades to "no status", stated — never guessed.
- **UC-031 — The repository declares how its sandbox is prepared.** A file in the code source
  names the setup its Runs need (dependencies, services); the sandbox executes it before the
  agent starts. Because the file executes commands, an Admin trusts it **per version**, and a
  changed file requires re-trust; a failed setup ends the Run with a named refusal before the
  agent ever runs. Without this, any real "implement and make the tests pass" story fails on a
  bare sandbox.
- **UC-032 — Member runs one Story through several runtimes and compares.** One dispatch fans
  into sibling Runs — same Story, different runtime or model — each in its own sandbox; the
  portal shows the resulting PRs side by side with per-Run cost ([BR-011](05-business-rules.md)).
  **Opens a decision before it is proposable**: [BR-001](05-business-rules.md) says one active
  Run per Story, and siblings need either a named exception or a new aggregate that owns them
  ([RULE-006](08-backlog-shaping-rules.md)). Sequenced behind runtime/model resolution
  (#244, #245).
