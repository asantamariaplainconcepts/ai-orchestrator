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

The nine `status:*` labels **and the hold** SHALL be created as a one-time repository bootstrap. No
committed script or workflow SHALL create, rename, or delete them, and no command SHALL invent a
missing label.

#### Scenario: a missing label fails loudly

- **WHEN** a command needs a `status:*` label that does not exist on the repository
- **THEN** it stops and reports the missing label rather than creating one

#### Scenario: a missing hold fails loudly

- **WHEN** a command needs the hold label and the repository does not have it
- **THEN** it stops and reports the missing label rather than creating it

### Requirement: two gates and two review stages

`ready-for-proposal` SHALL gate `/aio:propose`, and `ready-for-implementation` SHALL gate
`/aio:implement`. The two human review stages on the single PR SHALL each be marked by the **hold**
rather than by a state a reviewer must remember to set: the spec as a draft PR, held at
`status:ready-for-implementation`; then the code once marked ready, held at `status:code-review`. In
both, removing the hold is the reviewer's whole act, and the command that follows finds the issue
already in its gating state.

The **gating states are unconditional**; the **review stages are a property of the reviewed path**.
An unattended run (`/aio:ship`) SHALL still require `ready-for-proposal` to start and SHALL still
pass through `ready-for-implementation`, `in-progress` and `code-review` in order, leaving exactly one
`status:*` label at every moment — it applies no hold, so those states carry no review stage. A state
therefore says where the work is; whether a person is expected to look at it is said by the hold, and
by nothing else.

#### Scenario: gates are not skippable

- **WHEN** a command is invoked on an issue whose label is not its gating state
- **THEN** it refuses and names the command that advances the issue

#### Scenario: a review stage is a hold, not a state to set

- **WHEN** a reviewer finishes either review
- **THEN** they remove the hold and set no label, and the next command runs against the state its
  predecessor already applied

#### Scenario: an unattended run traverses the states without the stages

- **WHEN** `/aio:ship` carries an issue from `ready-for-proposal` to `done`
- **THEN** every intermediate state is set in order and the issue carries exactly one `status:*`
  label throughout, while no hold is applied and no review stage occurs

#### Scenario: a halted unattended run is indistinguishable from work awaiting a person

- **WHEN** an unattended run halts and applies the hold
- **THEN** the issue carries its current `status:*` label plus the hold, exactly as an issue parked
  at a review stage does, and the same act — a person removing the hold — releases it

### Requirement: the spec-less lane is a labelled exception, not an escape

Work that legitimately has no spec delta — hotfixes and pure infra or tooling changes — SHALL
carry `lane:spec-less` and SHALL skip only the propose stage. It SHALL still have an issue, a
branch, a PR, passing CI, and a retro entry at sync.

#### Scenario: a hotfix still leaves a record

- **WHEN** a `lane:spec-less` change is synced
- **THEN** there is no OpenSpec bundle to archive, and the retro log still gains an entry

### Requirement: the hold is a fact about an issue, not a state of it

An issue MAY carry a **hold** — the label named by `holdLabel` in `.claude/workflow.json` — meaning a
person must act before anything else does. The hold SHALL NOT be a `status:*` label and SHALL NOT be
one of the nine lifecycle states. A held issue SHALL still carry exactly one `status:*` label, and
reading its lifecycle state SHALL be unaffected by the hold's presence.

The two facts answer different questions: the `status:*` label says where the work is; the hold says
whether anyone may take it further. Collapsing them into one label is what forced a reviewer to pick
among nine.

#### Scenario: a held issue still has exactly one state

- **WHEN** an issue carries the hold
- **THEN** it also carries exactly one `status:*` label, and that label is its lifecycle state

#### Scenario: the hold is never a lifecycle value

- **WHEN** a command transitions an issue's status
- **THEN** it never sets or removes a `status:*` label named for the hold, because none exists

### Requirement: the structured issue read returns the hold

The repository's issue-reading skill (`.claude/skills/read-issue/SKILL.md`) SHALL return the hold's
presence in its structured result, alongside the `status:*` label, the change/spec ID and the
acceptance criteria. It already fetches the issue's `labels`; extracting the hold SHALL add no
additional call.

Reading SHALL remain read-only: the skill SHALL neither apply nor remove the hold.

#### Scenario: a held issue reads as held

- **WHEN** `read-issue` runs against an issue carrying the hold
- **THEN** its result reports the hold as present, alongside the issue's single `status:*` label

#### Scenario: an unheld issue reads as unheld

- **WHEN** `read-issue` runs against an issue with no hold label
- **THEN** its result reports the hold as absent, rather than omitting the field

