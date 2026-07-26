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

