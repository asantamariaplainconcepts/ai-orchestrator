# backlog-mirror

## ADDED Requirements

### Requirement: the vendor is the source of truth and the mirror is a projection

Stories SHALL be persisted as a read model of what the vendor holds — vendor id, title, state,
labels, and the time last seen (DEC-029). The application SHALL NOT treat the mirror as
authoritative, and SHALL NOT modify a Story except by re-reading it from the vendor (BR-008).

#### Scenario: a Story changes at the vendor

- **WHEN** a Story's title, state or labels change in the repository and a poll runs
- **THEN** the mirror reflects the new values, and no duplicate Story is created

#### Scenario: a Story leaves the repository

- **WHEN** a Story that was mirrored is closed or deleted at the vendor and a poll runs
- **THEN** the mirror reflects its absence rather than retaining it as current

#### Scenario: identity survives a rename

- **WHEN** a Story's title changes
- **THEN** it remains the same Story, because identity is the vendor's id and never the title

### Requirement: a poll is a full reconciliation

Each poll SHALL fetch the repository's current open Stories and reconcile the mirror against that
result in full — upserting what is present and marking absent what is not.

#### Scenario: repeated polls are idempotent

- **WHEN** two polls run with nothing changed at the vendor
- **THEN** the mirror is identical after the second, with no duplicates and no churn

### Requirement: polling runs on a schedule and on demand

The system SHALL poll each configured Connector on its project's interval, defaulting to 60
seconds (BR-015, DEC-028), and SHALL additionally expose an explicit refresh that polls
immediately without waiting for the interval.

#### Scenario: scheduled polling

- **WHEN** the application is running with a configured Connector
- **THEN** polls occur on the configured interval without user action

#### Scenario: on-demand refresh

- **WHEN** a user triggers a refresh
- **THEN** a poll runs immediately and the mirror reflects the result

#### Scenario: a project with no Connector

- **WHEN** the poller reaches a Project that has no Connector
- **THEN** it skips that Project without error

### Requirement: a failed poll degrades to stale, never to empty

When a poll fails — the vendor is unreachable, rate-limited, or rejects the credential — the
previously mirrored Stories SHALL remain readable, and the failure SHALL be recorded against the
Connector so it can be surfaced. A failure SHALL NOT empty the mirror and SHALL NOT be silent.

#### Scenario: the vendor is unreachable

- **WHEN** a poll fails to reach the vendor
- **THEN** previously mirrored Stories are still returned to clients, and the failure is recorded
  with its time and reason

#### Scenario: distinguishing empty from broken

- **WHEN** a client views a backlog that has no Stories
- **THEN** it can tell "the repository has no open Stories" from "the last poll failed", because
  the two are represented differently

### Requirement: the backlog is visible in the application

A Project's page SHALL show its Connector configuration and its mirrored Stories, and SHALL handle
the empty, loading and error states defined by the design system.

#### Scenario: viewing a connected project

- **WHEN** a user opens a Project that has a Connector and mirrored Stories
- **THEN** the Connector's coordinates and the Stories are shown, with vendor ids in monospace

#### Scenario: viewing an unconnected project

- **WHEN** a user opens a Project with no Connector
- **THEN** the page states that no backlog is connected and offers to configure one
