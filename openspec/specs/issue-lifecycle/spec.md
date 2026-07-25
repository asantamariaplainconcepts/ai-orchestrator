# issue-lifecycle Specification

## Purpose
TBD - created by archiving change ceremonies. Update Purpose after archive.
## Requirements
### Requirement: nine states, one label, one home

An issue's lifecycle state SHALL be represented by exactly one `status:*` label drawn from:
`backlog`, `needs-refinement`, `ready-for-proposal`, `proposal-review`,
`ready-for-implementation`, `in-progress`, `code-review`, `done`, and `blocked` (reachable from
any state). The label SHALL be the **sole** source of lifecycle truth; no other artifact — board
field, project column, spreadsheet — participates in it.

#### Scenario: exactly one status label

- **WHEN** a command advances an issue
- **THEN** it removes the previous `status:*` label as it adds the next, leaving exactly one

#### Scenario: a board edit is inert

- **WHEN** someone changes a GitHub Project card's status field
- **THEN** the issue's lifecycle state is unchanged, because nothing reconciles the board

### Requirement: labels are provisioned once, not by automation

The nine labels SHALL be created as a one-time repository bootstrap. No committed script or
workflow SHALL create, rename, or delete them, and no command SHALL invent a missing label.

#### Scenario: a missing label fails loudly

- **WHEN** a command needs a `status:*` label that does not exist on the repository
- **THEN** it stops and reports the missing label rather than creating one

### Requirement: two gates and two review stages

`ready-for-proposal` SHALL gate `/aio:propose`, and `ready-for-implementation` SHALL gate
`/aio:implement`. `proposal-review` and `code-review` SHALL each denote a human review stage on
the single PR — the spec as a draft, then the code once marked ready.

#### Scenario: gates are not skippable

- **WHEN** a command is invoked on an issue whose label is not its gating state
- **THEN** it refuses and names the command that advances the issue

### Requirement: the spec-less lane is a labelled exception, not an escape

Work that legitimately has no spec delta — hotfixes and pure infra or tooling changes — SHALL
carry `lane:spec-less` and SHALL skip only the propose stage. It SHALL still have an issue, a
branch, a PR, passing CI, and a retro entry at sync.

#### Scenario: a hotfix still leaves a record

- **WHEN** a `lane:spec-less` change is synced
- **THEN** there is no OpenSpec bundle to archive, and the retro log still gains an entry

