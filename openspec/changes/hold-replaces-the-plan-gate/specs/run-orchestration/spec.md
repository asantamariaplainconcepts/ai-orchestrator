## MODIFIED Requirements

### Requirement: a matching story event creates a Run and dispatches it

When a `StoryChanged` event (Added or Updated) is handled and the Story's current labels and
state match an enabled Automation of its Project, and no rule refuses, the system SHALL create a
Run recording the story reference, the Automation, its creation timestamp and its state (BR-014
subset), and SHALL enqueue exactly one dispatch message carrying the Run id. Matching SHALL read
the Story and the Automations through Contracts read interfaces — current truth, never the event
payload (BR-015). A Removed event SHALL never match.

Matching SHALL NOT branch on an approval flag: every Automation is single-phase (BR-007 as
rewritten). A Story carrying the hold SHALL refuse creation instead — see *story-hold*.

#### Scenario: the loop closes

- **WHEN** a Story gains the trigger label of an enabled Automation and the `StoryChanged` event is
  handled
- **THEN** a Run exists in `Queued` referencing that Story and Automation, and one dispatch
  message carrying the Run id is on the queue

#### Scenario: no matching Automation

- **WHEN** an event is handled for a Story matching no enabled Automation of its Project
- **THEN** no Run is created and nothing is enqueued

#### Scenario: a held Story is refused

- **WHEN** the matching Story carries the hold
- **THEN** no Run is created and nothing is enqueued — the refusal is the hold, recorded like any
  other non-created outcome

### Requirement: a Member dispatches a Run on demand

The system SHALL let a Member create a Run for a chosen Story and enabled Automation via
`POST /api/projects/{projectId}/runs` (UC-012). The request SHALL take the same creation path
as event matching — BR-001, BR-002 and the hold enforced by the same code — and SHALL bypass only
trigger detection (BR-013): the Story need not carry the trigger label. Refusals SHALL answer the
human: an active Run yields a conflict naming BR-001; a held Story yields a refusal naming the
hold; an unknown Story or unavailable Automation yields a distinct validation error and nothing is
written. At the BR-002 cap the Run SHALL be created `Queued`, nothing enqueued, and the response
SHALL say so.

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

#### Scenario: the hold is not a bypass

- **WHEN** Run now targets a Story carrying the hold
- **THEN** the request is refused with the hold named and nothing is written

## REMOVED Requirements

### Requirement: an approval-gated Run pauses on its Plan and a human decides

**Reason**: DEC-062 accepted that BR-007's approval gate had become "a workflow control now, not a
containment control" — once an Automation runs the repository's own prompt, "a plan phase publishes
nothing" is a prompt-level promise, not a product guarantee. A workflow control does not need a
second Run phase and a review surface; the hold on the Story states the same thing where the work
is. This change supersedes DEC-039 and DEC-040 and rewrites BR-007 accordingly.

**Migration**: An Automation that was `requiresApproval = true` becomes an Automation that applies
the hold on success (see *story-hold*): the flow stops after it acts rather than pausing inside it.
The `Planning` and `AwaitingApproval` states, the plan-decision use case and the `Plan`/`ApprovedAt`
columns remain in the codebase but become unreachable — no code path produces a Plan or enters
either state — and are deleted in a named follow-up, following DEC-062's own precedent for the
dormant `AwaitingInput` wait. Existing Runs already recorded in those states are historical records
(BR-014: Runs are never deleted) and are not migrated.
