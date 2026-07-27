# run-orchestration Specification

## Purpose
TBD - created by archiving change story-automation-matching. Update Purpose after archive.
## Requirements
### Requirement: a matching story event creates a Run and dispatches it

When a `StoryChanged` event (Added or Updated) is handled and the Story's current labels and
state match an enabled Automation of its Project with `requiresApproval = false`, and no rule
refuses, the system SHALL create a Run recording the story reference, the Automation, its
creation timestamp and its state (BR-014 subset), and SHALL enqueue exactly one dispatch
message carrying the Run id (BR-007 single-phase). Matching SHALL read the Story and the
Automations through Contracts read interfaces — current truth, never the event payload
(BR-015). A Removed event SHALL never match.

#### Scenario: the loop closes

- **WHEN** a Story gains the trigger label of an enabled single-phase Automation and the
  `StoryChanged` event is handled
- **THEN** a Run exists in `Queued` referencing that Story and Automation, and one dispatch
  message carrying the Run id is on the queue

#### Scenario: no matching Automation

- **WHEN** an event is handled for a Story matching no enabled Automation of its Project
- **THEN** no Run is created and nothing is enqueued

#### Scenario: the two-phase lane is refused, loudly

- **WHEN** the matching Automation has `requiresApproval = true`
- **THEN** no Run is created, and the refusal is logged naming the Automation — this slice's
  stated limitation, not silence

### Requirement: one active Run per Story is a database constraint

BR-001 SHALL be enforced by a partial unique index over the Run's story reference across the
active states (`Queued`, `Planning`, `AwaitingApproval`, `Executing`). A match against a Story
that already has an active Run SHALL be ignored — no new Run, nothing enqueued, not queued for
later. The handler SHALL treat the index violation as "already done", never as an error.

#### Scenario: a second match while a Run is active

- **WHEN** a matching event is handled for a Story whose Run is in an active state
- **THEN** no second Run exists and no message was enqueued

#### Scenario: concurrent handling of the same Story

- **WHEN** two deliveries for the same Story are handled concurrently
- **THEN** exactly one Run exists afterwards — the index decides the race, and the loser
  reports success

### Requirement: duplicate delivery changes nothing

Delivery is at-least-once; the handler SHALL be idempotent. Handling the same `StoryChanged`
twice SHALL produce the same outcome as handling it once: one Run, one dispatch message.

#### Scenario: the same event delivered twice

- **WHEN** an identical `StoryChanged` is delivered a second time while the created Run is
  active
- **THEN** no second Run and no second dispatch message exist

### Requirement: the project cap holds at creation

BR-002 SHALL be evaluated when a Run is created: if the Project already has as many Runs in
`Planning`/`Executing` as its cap (default 2), the new Run SHALL remain `Queued` and no
dispatch message SHALL be enqueued. Promotion when capacity frees is explicitly out of this
slice — nothing can complete yet.

#### Scenario: a match at the cap

- **WHEN** a match occurs while the Project has cap-many Runs in `Planning`/`Executing`
- **THEN** the Run exists in `Queued` and the queue received nothing

### Requirement: cross-module reads happen through the second and third Contracts surfaces

The Runs module SHALL read Automations through `IAutomationCatalog` in
`AiOrchestrator.Modules.Projects.Contracts` and Stories through `IStoryReader` in
`AiOrchestrator.Modules.Backlog.Contracts`. The owning modules SHALL register the
implementations. The Runs module SHALL reference no other module's implementation assembly and
no messaging or cloud SDK — the existing guardrail suite SHALL verify it with these assemblies
in place.

#### Scenario: the boundary holds with three modules

- **WHEN** the guardrail suite runs with the Runs module present
- **THEN** implementation references between modules still fail, Contracts references pass,
  and the Runs module carries no infrastructure reference

### Requirement: Runs are observable per project and per Story

The system SHALL expose a project's Runs read-only at
`GET /api/projects/{projectId}/runs`, newest first, with an optional `vendorStoryId` filter
for the per-Story view (UC-021, DEC-031). Each Run SHALL expose exactly the BR-014 subset it
records today: id, vendor story id, automation id, state, created timestamp, dispatched
timestamp. The portal SHALL render the project's Runs and a per-Story filter reachable from
the backlog, joining automation details client-side from the automations endpoint; fields
DEC-031 names that have no producing feature yet (output link, logs, cost) SHALL render the
design system's empty value, and a project without Runs SHALL show the empty state.

#### Scenario: a member sees what the loop produced

- **WHEN** a Member opens the Runs view of a project where matching has created Runs
- **THEN** each Run lists its Story reference, its Automation's trigger/action/runtime, its
  state and its timestamps, newest first

#### Scenario: the per-Story view isolates one Story's history

- **WHEN** the Member follows a backlog row to its Runs
- **THEN** only Runs whose vendor story id matches that Story are listed

#### Scenario: absent data is shown as absent

- **WHEN** a Run's output link, logs or cost have no producing feature, or its Automation no
  longer exists in current configuration
- **THEN** those cells render the design system's empty value — never a blank, a zero, or an
  invented value

#### Scenario: no Runs yet

- **WHEN** a Member opens the Runs view of a project where nothing has ever matched
- **THEN** the design-system empty state explains that Runs appear when an Automation matches

### Requirement: a Member dispatches a Run on demand

The system SHALL let a Member create a Run for a chosen Story and enabled Automation via
`POST /api/projects/{projectId}/runs` (UC-012). The request SHALL take the same creation path
as event matching — BR-001, BR-002 and the BR-007 lane split enforced by the same code — and
SHALL bypass only trigger detection (BR-013): the Story need not carry the trigger label.
Refusals SHALL answer the human: an active Run yields a conflict naming BR-001; a two-phase
Automation yields the stated limitation; an unknown Story or unavailable Automation yields a
distinct validation error and nothing is written. At the BR-002 cap the Run SHALL be created
`Queued`, nothing enqueued, and the response SHALL say so.

#### Scenario: run now without the label

- **WHEN** a Member triggers Run now for a Story that does not carry the Automation's trigger
  label
- **THEN** a Run exists and one dispatch message carries its id — identical in shape to a
  matched event's Run

#### Scenario: the rules answer instead of ignoring

- **WHEN** Run now targets a Story with an active Run
- **THEN** the response is a conflict naming the one-active-Run rule and no Run was created

#### Scenario: the cap speaks

- **WHEN** Run now fires while the Project is at its concurrency cap
- **THEN** the Run exists in `Queued`, the queue received nothing, and the response states the
  Run is waiting

#### Scenario: the gate is not a bypass

- **WHEN** Run now targets a `requiresApproval = true` Automation
- **THEN** the request is refused with the two-phase stated limitation and nothing is written

### Requirement: an approval-gated Run pauses on its Plan and a human decides

A Run whose Automation has `requiresApproval = true` SHALL produce a Plan, store it on the Run
and pause at `AwaitingApproval` without publishing anything (BR-007, DEC-040). Approving SHALL
stamp the approval, return the Run to `Queued` and re-enqueue it for execution; rejecting SHALL
end the Run `Cancelled` — terminal, freeing the Story (BR-001). A Run awaiting approval SHALL
be subject to no timeout (BR-006) and SHALL NOT count toward the project cap (BR-002), while
still holding its Story against a second Run (BR-001). The Plan and the decision SHALL be part
of the Run's record (BR-014). No code path SHALL any longer refuse the two-phase lane as
unimplemented.

#### Scenario: the Agent proposes and the Run waits

- **WHEN** an approval-gated Run executes
- **THEN** its Plan is stored, its state is `AwaitingApproval`, and no branch or pull request
  was created

#### Scenario: approval resumes into execution

- **WHEN** the Plan is approved
- **THEN** the Run is re-enqueued, executes the implement path, and ends `Succeeded` with a
  pull request — as the single-phase lane does

#### Scenario: rejection ends it

- **WHEN** the Plan is rejected
- **THEN** the Run ends `Cancelled`, nothing is enqueued, and the Story can run again

#### Scenario: waiting is free and untimed

- **WHEN** a Run sits in `AwaitingApproval`
- **THEN** no timeout applies to it and the project's concurrency cap is unaffected, yet a new
  match on the same Story still creates no second Run

### Requirement: a Run's detail is readable, with its Plan

The portal SHALL offer a Run detail view reachable from the Runs table showing state,
timestamps, usage, output link and — when present — the Plan rendered as sanitised markdown,
with controls to approve or reject while the Run awaits approval.

#### Scenario: the reviewer reads the Plan where the decision is made

- **WHEN** a Member opens a Run awaiting approval
- **THEN** the Plan renders and both decisions are available; hostile markdown in the Plan is
  inert

