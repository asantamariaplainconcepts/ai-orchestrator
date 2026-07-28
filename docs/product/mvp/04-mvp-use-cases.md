# MVP use cases

Actor-scoped, one capability each. Every backlog issue must trace to ≥1 UC
([RULE-003](08-backlog-shaping-rules.md)). States and constraints referenced from
[business rules](05-business-rules.md).

## Identity & access (BC-005)

- **UC-001 — User signs in.** Any user authenticates via Entra ID and lands on their
  project list. *(Gated by [OPN-002](07-open-decisions.md) until closed.)*
- **UC-002 — Admin assigns roles.** ACT-001 grants Admin or Member on a project.

## Project configuration (BC-001)

- **UC-003 — Admin creates a Project.**
- **UC-004 — Admin configures the Connector.** Vendor choice (GitHub | AzDO), org/repo
  or org/project coordinates, and the Key Vault reference of the project PAT
  ([DEC-030](10-locked-mvp-decisions.md)). Saving verifies the credential with a live
  Connector call (reality check, never assumed).
- **UC-005 — Admin creates an Automation.** Trigger label/state + action
  ([DEC-026](10-locked-mvp-decisions.md)) + runtime + `requiresApproval` + timeout.
  Save fails on trigger overlap ([BR-003](05-business-rules.md)).
- **UC-006 — Admin edits, disables or deletes an Automation.** Same validation; disabling stops
  future matches, never touches active Runs. Deletion is possible only while no Run has used it
  — once one has, the Automation is kept and disabling is the answer, because every Run records
  its Automation for the audit trail (BR-014, #84).

## Backlog (BC-002)

- **UC-007 — Member views the project backlog.** Mirrored stories with state, labels,
  active-run badge.
- **UC-008 — Member applies/removes a trigger label on a Story.** Written back to the
  vendor through the Connector; vendor-side labeling is equivalent
  ([DEC-027](10-locked-mvp-decisions.md)).
- **UC-022 — Member opens a Story and reads its detail.** Vendor id, title, state, labels
  and the mirrored description rendered as markdown; the body is mirrored by the same poll
  (DEC-028) and sanitised at render, never at rest (BR-008).
- **UC-023 — Member reads the documents attached to a Story's work.** The markdown its
  linked change adds or modifies, read live at that change's head through the Connector and
  rendered sanitised. Distinct from UC-013 (an Agent's Plan, which is data on a Run).
- **UC-024 — Member reviews the file changes a Run produced.** The files its pull request
  touched, with the vendor's unified patch, read live at the change and shown beside the Plan
  that was approved. Binary and over-large patches state why rather than truncating.
- **UC-009 — System polls the backlog.** Per-project poll (default 60 s, configurable)
  refreshes the Mirror and emits normalized story events ([DEC-028](10-locked-mvp-decisions.md)).
- **UC-010 — System ingests a vendor webhook.** GitHub/AzDO events normalize into the
  same event stream as polling ([BR-015](05-business-rules.md)). *(Lands after polling
  — [DEC-028](10-locked-mvp-decisions.md) build order.)*

## Dispatch & approval (BC-003)

- **UC-011 — System matches an event and creates a Run.** Honoring one-active-run-per-story
  ([BR-001](05-business-rules.md)) and the project cap ([BR-002](05-business-rules.md));
  `requiresApproval` routes the Run into the plan phase, otherwise straight to dispatch.
- **UC-012 — Member triggers Run now.** Chosen Story + Automation, bypassing detection,
  honoring every rule ([BR-013](05-business-rules.md)). Also the re-run path for failures.
- **UC-013 — Member/Admin reviews a Plan.** Reads the Agent's proposal on the run detail
  page and approves (→ execution is dispatched) or rejects (→ Run ends `Cancelled`),
  mirroring spec review ([DEC-040](10-locked-mvp-decisions.md)).
- **UC-014 — Member/Admin cancels a Run.** Queued/AwaitingApproval → discarded;
  Executing/Planning → job terminated ([BR-012](05-business-rules.md), [DEC-041](10-locked-mvp-decisions.md)).

## Agent execution (BC-004)

- **UC-015 — Agent produces a Plan (phase 1).** For approval-gated Runs: reads the
  Story, writes a plan proposal onto the Run, pauses it at `AwaitingApproval`.
- **UC-016 — Agent implements a Story → PR.** Clones the project code repo with the
  project PAT, implements, opens a PR, links it on the Run and the Story.
- **UC-017 — Agent refines/comments a Story.** Analysis, refinement questions, or
  acceptance-criteria draft posted as a story comment.
- **UC-018 — Agent transitions a Story's state.** Via the Connector, per Automation config.
- **UC-019 — Agent estimates a Story.** Sets the estimate field with reasoning comment.
- **UC-020 — Agent reports usage at run end.** Tokens + cost persisted on the Run
  ([DEC-038](10-locked-mvp-decisions.md), [BR-011](05-business-rules.md)).

## Observation (BC-003/BC-004)

- **UC-021 — Member views Runs.** Per project and per story: state, timestamps,
  runtime, output link, logs (fetched), cost ([DEC-031](10-locked-mvp-decisions.md)).

## Post-MVP use cases

- **UC-024 — Member grills a Story to ready.** A `GrillToReady` Automation interrogates a Story
  against the project's own readiness document, one pass per exchange: unmet criteria become
  questions on the Story, answers resume the Run, and a met bar becomes a configurable ready
  label plus a verdict comment. The ready label can trigger the next Automation (DEC-048,
  revising DEC-026; #79).
- **UC-025 — Member triggers a proposal for a ready Story.** A `ProposeSpec` Automation turns a
  ready Story into a documentation pull request — why, what changes, impact, tasks — through the
  same publishing pipeline as implementation. A Story with no body is refused (nothing to
  propose from), as is one whose linked change already exists. Chains from UC-024's ready label
  (#80; DEC-048).
- **UC-026 — Member sees everything waiting on a human.** One inbox across projects lists every
  Run awaiting an approval, an answer or a failure decision, newest wait first, with an ambient
  count in the shell. A failure leaves once a newer Run exists for its Story — it waits on
  nobody (#94; borrowed shape: Orbion's triage inbox).
