# run-orchestration

## ADDED Requirements

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
