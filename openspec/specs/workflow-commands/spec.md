# workflow-commands Specification

## Purpose
TBD - created by archiving change ai-delivery-layer. Update Purpose after archive.
## Requirements
### Requirement: the commands are the public API

Contributors SHALL drive the workflow through `/aio:grill`, `/aio:propose`, `/aio:implement`,
`/aio:sync`, `/aio:refine`, and `/aio:status`. OpenSpec SHALL be reachable only through `/opsx:*`
primitives that skills wrap, so the spec engine stays replaceable without touching workflow policy.

#### Scenario: swapping the spec engine

- **WHEN** the spec tool changes
- **THEN** the `/opsx:*` layer and its skills change, and the `/aio:*` gates do not

### Requirement: every mutating command asserts its worktree first

Before any state-changing git operation, a command SHALL verify that
`git rev-parse --show-toplevel` matches the session's working directory, and SHALL abort if it
does not.

#### Scenario: a hijacked worktree aborts the batch

- **WHEN** the resolved repository root differs from the session's directory
- **THEN** the command aborts before mutating anything and reports both paths

### Requirement: grill gates on the Definition of Ready

`/aio:grill` SHALL interrogate an idea field by field until it satisfies the Definition of Ready,
then create the issue with `status:ready-for-proposal`. For an existing issue it SHALL perform a
gap check and comment the unmet fields **by name** on the issue. It SHALL refuse to mark ready any
item that depends on an open decision (`OPN-*`), directing the reader to close that decision first.

#### Scenario: an item blocked by an open decision

- **WHEN** an item depends on an unresolved `OPN-*`
- **THEN** grill refuses to mark it ready and names the decision that blocks it

### Requirement: propose opens a draft PR and nothing else

`/aio:propose` SHALL refuse unless the issue is `status:ready-for-proposal`. It SHALL create a
branch whose name **ends with the change's kebab-case slug**, verify the branch base is current
`origin/main` and that the PR targets the repository's real default branch, create the OpenSpec
change, and open a **draft** PR. It SHALL NOT write application code.

#### Scenario: wrong status

- **WHEN** `/aio:propose` runs on an issue labelled `status:backlog`
- **THEN** it refuses, states the actual label, and instructs the reader to run `/aio:grill`

#### Scenario: stale base

- **WHEN** the branch base is behind `origin/main`
- **THEN** propose refuses until the branch is rebased onto a fresh base

#### Scenario: the draft state is the gate

- **WHEN** the proposal PR is opened
- **THEN** it is a draft, and it stays a draft until a human moves the issue to
  `status:ready-for-implementation`

### Requirement: implement respects the WIP cap and the same PR

`/aio:implement` SHALL refuse unless the issue is `status:ready-for-implementation`. It SHALL
refuse when the number of issues already `status:in-progress` has reached the configured WIP limit.
It SHALL set `status:in-progress` **before** the first commit, reuse the proposal's PR rather than
opening a second, and warn — without blocking — when the branch's file footprint overlaps another
in-flight change.

#### Scenario: WIP cap reached

- **WHEN** the WIP limit is 2 and two issues are already in progress
- **THEN** implement refuses, lists the in-progress issues, and names `/aio:sync` as the way to
  free a slot

#### Scenario: overlap is advisory

- **WHEN** the branch touches files another in-flight change also touches
- **THEN** implement warns and continues

### Requirement: tunable process values have one home

The WIP limit and any other tunable process value SHALL be defined once, in
`.claude/workflow.json`, and read from there by every command.

#### Scenario: raising the WIP limit

- **WHEN** the WIP limit changes
- **THEN** exactly one file changes and every command observes the new value

### Requirement: sync verifies green before suppressing any signal

`/aio:sync` SHALL, in this order: refuse a draft PR; refuse when the check rollup is failing or
pending; **verify CI is green while the last implementation commit is still the PR head**; re-run
the overlap check against both `status:in-progress` issues and open `status:code-review` PRs;
append the retro entry; archive the OpenSpec change on the branch; and only then create the
close-out commit that carries `[skip ci]`.

#### Scenario: green is verified before the marker exists

- **WHEN** sync runs
- **THEN** the CI-green check happens before the `[skip ci]` commit is created, never after

#### Scenario: a red PR cannot sync

- **WHEN** any required check is failing or pending
- **THEN** sync refuses and names the failing check

### Requirement: sync validates the squash message before merging

`/aio:sync` SHALL lint the exact subject and body it is about to use for the squash commit against
the repository's commit conventions, and SHALL refuse to merge if they do not pass. The squash
subject SHALL be the PR title, and the body SHALL NOT contain `[skip ci]`.

#### Scenario: an over-long body is caught while the merge is still preventable

- **WHEN** the intended squash body contains a line longer than the configured limit
- **THEN** sync refuses to merge and reports the violation, rather than the violation reaching
  `main` where no hook can prevent it

#### Scenario: the marker never reaches main

- **WHEN** the branch's close-out commit contains `[skip ci]`
- **THEN** the squash body written to `main` does not

### Requirement: main receives exactly one commit per change

A synced change SHALL appear on `main` as a single squash commit whose subject is the PR title,
with the change's specs folded into `openspec/specs/`, its bundle archived under
`openspec/changes/archive/`, and its retro entry present.

#### Scenario: post-merge state

- **WHEN** a change is synced
- **THEN** `main` gains one commit, no active change bundle remains, and the retro log has a new
  entry

### Requirement: status is read-only

`/aio:status` SHALL report an issue's lifecycle position, its PR state, and any drift between the
two, and SHALL NOT modify anything.

#### Scenario: drift is reported, not corrected

- **WHEN** an issue is `status:in-progress` but its PR is still a draft with no commits
- **THEN** status reports the discrepancy and changes nothing

### Requirement: gating shell steps cannot be masked by a pipe

Any command step whose exit code gates a decision SHALL either set `pipefail` or check the exit
code explicitly, so a failure piped into another process is never read as success.

#### Scenario: a failing command piped to a formatter

- **WHEN** a gating command's output is piped
- **THEN** its failure still fails the gate

