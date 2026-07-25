# skill-catalog Specification

## Purpose
TBD - created by archiving change ai-delivery-layer. Update Purpose after archive.
## Requirements
### Requirement: one responsibility per skill, and no skill calls another

Each skill SHALL live at `.claude/skills/<name>/SKILL.md`, SHALL do exactly one thing, and SHALL
NOT invoke another skill. Composition SHALL happen only in commands.

#### Scenario: composition stays in one layer

- **WHEN** a workflow step needs two skills' behaviour
- **THEN** a command invokes both in sequence, and neither skill references the other

### Requirement: mutating skills confirm before touching shared state

A skill that creates or modifies state visible to other people — issues, labels, pull requests,
branches, the retro log — SHALL state what it is about to do and obtain confirmation before doing
it. Read-only skills SHALL declare themselves read-only and SHALL NOT mutate anything.

#### Scenario: label change is confirmed

- **WHEN** `set-issue-status` is about to move an issue's `status:*` label
- **THEN** it names the issue, the current label, and the target label, and proceeds only after
  confirmation

#### Scenario: read-only skill stays read-only

- **WHEN** `read-issue` or `status` runs
- **THEN** no issue, label, branch, or PR is modified

### Requirement: refusals name the way forward

A skill or command that refuses because a precondition is unmet SHALL name the specific unmet
condition and the command that resolves it.

#### Scenario: a bare refusal is not acceptable

- **WHEN** work is attempted on an issue that is not `status:ready-for-proposal`
- **THEN** the refusal states the issue's actual status and instructs the reader to run
  `/aio:grill <issue>`

### Requirement: single source of truth for rubrics and lifecycles

`grill-to-ready` SHALL read the Definition of Ready document rather than restating its rubric, and
`set-issue-status` SHALL encode the lifecycle's legal transitions exactly once.

#### Scenario: the rubric changes in one place

- **WHEN** the Definition of Ready gains a required field
- **THEN** `grill-to-ready` enforces it with no edit to the skill

### Requirement: append-only history is never rewritten

`retro-entry` SHALL only append to the retro log. `write-adr` SHALL NOT edit an ADR that is
accepted; superseding it with a new ADR is the only permitted change.

#### Scenario: a prior retro entry is preserved

- **WHEN** a post-merge finding is recorded for a change that already has an entry
- **THEN** a new entry is appended and the earlier entry is left exactly as written

### Requirement: the authoring standard is vendored with its licence

`writing-great-skills` SHALL be vendored under `.claude/skills/` **with its `NOTICE` file**
recording MIT authorship and any local adaptation, and every skill authored in this repo SHALL be
reviewed against it.

#### Scenario: licence travels with the code

- **WHEN** the vendored skill is present in this public repository
- **THEN** its `NOTICE` file is present alongside it and names the original author and licence

