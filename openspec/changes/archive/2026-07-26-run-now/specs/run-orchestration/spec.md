# run-orchestration

## ADDED Requirements

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
