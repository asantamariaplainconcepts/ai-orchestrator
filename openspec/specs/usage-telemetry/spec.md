# usage-telemetry Specification

## Purpose
TBD - created by archiving change ai-delivery-layer. Update Purpose after archive.
## Requirements
### Requirement: attribution joins on session id

A session-start hook SHALL append `{ts, session_id, cwd, branch, change, project}` to
`sessions.jsonl`, and usage reporting SHALL attribute work to a change by joining telemetry on
`session.id`. Attribution SHALL NOT depend on environment-variable tagging or on inferring the
change from a branch name.

#### Scenario: a single session covers the whole loop

- **WHEN** one session runs grill, propose, implement, and sync for the same change
- **THEN** its usage is attributed to that change, even though the change did not exist when the
  process started

#### Scenario: branch named differently from its change

- **WHEN** a branch name does not contain the change slug
- **THEN** attribution is still correct, because it never consulted the branch name

### Requirement: the collector is the system of record

An OpenTelemetry Collector, version-pinned in compose, SHALL receive the agent's metrics and logs
and write `usage.jsonl` with **append semantics**. Dashboards SHALL be treated as disposable
viewers over that file.

#### Scenario: restarting the collector preserves history

- **WHEN** the Collector restarts
- **THEN** `usage.jsonl` retains every prior record rather than being truncated

#### Scenario: losing the dashboard loses nothing

- **WHEN** the visualisation stack is removed
- **THEN** all usage history remains available in `usage.jsonl`

### Requirement: the collector stamps the project tag

The Collector SHALL stamp `project=ai-orchestrator` on received telemetry server-side. Sender-side
resource attributes SHALL NOT be relied upon, because some clients drop them.

#### Scenario: a client that drops resource attributes

- **WHEN** telemetry arrives without a project resource attribute
- **THEN** the stored record still carries `project=ai-orchestrator`

### Requirement: telemetry hooks fail soft

Session-start hooks SHALL be invoked by absolute path so they work from git worktrees, and SHALL
never block or fail a session: if the Collector is unreachable or a write fails, the hook exits
quietly.

#### Scenario: collector down

- **WHEN** a session starts and nothing is listening on the OTLP port
- **THEN** the session proceeds normally and the failure is not fatal

### Requirement: telemetry data never enters the repository

`usage.jsonl`, `sessions.jsonl`, and any collector storage SHALL be git-ignored. The repository is
public and this data carries user identifiers.

#### Scenario: telemetry cannot be committed accidentally

- **WHEN** `git status` runs after a session has produced telemetry
- **THEN** no telemetry file appears as untracked or staged

### Requirement: human time is recorded by humans

Reported human time SHALL be understood as a lower bound, and the PR "Time invested" section
SHALL remain the authoritative record of human effort. Retro entries SHALL state the source of
their numbers as telemetry or manual.

#### Scenario: an honest retro entry

- **WHEN** a retro entry records time invested
- **THEN** it names its source, so a near-zero human figure is read as an instrument limit rather
  than as a fact

