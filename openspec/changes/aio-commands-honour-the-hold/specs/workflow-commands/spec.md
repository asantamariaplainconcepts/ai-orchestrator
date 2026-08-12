## ADDED Requirements

### Requirement: the hold is a refusal, and no command ever clears it

Every mutating `/aio:*` command SHALL read the hold from the issue before it acts, and SHALL refuse
outright while the issue carries it. A refusal SHALL name the hold and say that a person clears it by
removing that one label, and SHALL leave every side effect unperformed — no branch, no commit, no
push, no PR, no label change, no retro entry, no merge.

The hold SHALL be compared case-insensitively, the way the vendor compares labels, so `HITL` and
`hitl` are one hold.

No command, script or workflow in this repository SHALL remove the hold. Clearing it is a person's
act, always — that is the whole mechanism, and an automation that could undo it would return the
reviewer to choosing among labels.

#### Scenario: a refusal changes nothing

- **WHEN** a mutating command is invoked on a held issue
- **THEN** it refuses, names the hold and who clears it, and no git or GitHub state changes

#### Scenario: the hold folds case

- **WHEN** the hold label is present spelled in different case from the configured value
- **THEN** the issue is still held

#### Scenario: nothing in the repository releases a hold

- **WHEN** the `/aio:*` commands and skills are searched for a removal of the hold label
- **THEN** none exists — only a person removes it

### Requirement: clearing the hold is the approval

At each of the two human review stages the reviewer's whole act SHALL be removing the hold. No
command SHALL require a further `status:*` change from the reviewer to unlock the next command, and
the command that runs next SHALL find the issue already in its gating state.

#### Scenario: spec review is released by one label

- **WHEN** a reviewer validates a proposal and removes the hold
- **THEN** `/aio:implement` proceeds on the already-set `status:ready-for-implementation`, and the
  reviewer set no label

#### Scenario: code review is released by one label

- **WHEN** a reviewer approves the implementation and removes the hold
- **THEN** `/aio:sync` proceeds on the already-set `status:code-review`, and the reviewer set no label

### Requirement: refine is untouched by the hold

`/aio:refine` SHALL behave identically whether or not the issue carries the hold. It runs after the
merge and gates nothing, so a hold has nothing left to stop.

#### Scenario: a held, merged issue still accepts a retro entry

- **WHEN** `/aio:refine` is invoked on a merged issue that carries the hold
- **THEN** it appends the follow-up retro entry exactly as it would without the hold

## MODIFIED Requirements

### Requirement: grill gates on the Definition of Ready

`/aio:grill` SHALL interrogate an idea field by field until it satisfies the Definition of Ready,
then create the issue with `status:ready-for-proposal`. For an existing issue it SHALL perform a
gap check and comment the unmet fields **by name** on the issue. It SHALL refuse to mark ready any
item that depends on an open decision (`OPN-*`), directing the reader to close that decision first.

On a **held** issue `/aio:grill` SHALL still evaluate and still comment, and SHALL NOT invoke
`set-issue-status`. A hold blocks advancing, not talking: the gap check is information a reviewer
wants while deciding whether to clear the hold.

#### Scenario: an item blocked by an open decision

- **WHEN** an item depends on an unresolved `OPN-*`
- **THEN** grill refuses to mark it ready and names the decision that blocks it

#### Scenario: grill talks to a held issue

- **WHEN** `/aio:grill` is invoked on a held issue
- **THEN** it evaluates the Definition of Ready and may comment, sets no `status:*` label, and
  reports that the hold is why the status was not advanced

### Requirement: propose opens a draft PR and nothing else

`/aio:propose` SHALL refuse unless the issue is `status:ready-for-proposal`, and SHALL refuse — before
the status gate's side effects and before any git operation — while the issue carries the hold. It
SHALL create a branch whose name **ends with the change's kebab-case slug**, verify the branch base
is current `origin/main` and that the PR targets the repository's real default branch, create the
OpenSpec change, and open a **draft** PR. It SHALL NOT write application code.

On completion it SHALL set `status:ready-for-implementation` **and** apply the hold, in that single
advance. It SHALL NOT set `status:proposal-review`: the draft PR plus the hold *is* the spec-review
stage, and the next state is already in place for the moment the reviewer releases it.

#### Scenario: wrong status

- **WHEN** `/aio:propose` runs on an issue labelled `status:backlog`
- **THEN** it refuses, states the actual label, and instructs the reader to run `/aio:grill`

#### Scenario: a held issue gets no branch

- **WHEN** `/aio:propose` runs on a held issue
- **THEN** it refuses naming the hold, and no branch and no PR exist afterwards

#### Scenario: stale base

- **WHEN** the branch base is behind `origin/main`
- **THEN** propose refuses until the branch is rebased onto a fresh base

#### Scenario: the draft state is the gate

- **WHEN** the proposal PR is opened
- **THEN** it is a draft, the issue is `status:ready-for-implementation` and held, and it stays a
  draft until a human removes the hold and `/aio:implement` runs

### Requirement: implement respects the WIP cap and the same PR

`/aio:implement` SHALL refuse — **before** the WIP gate — while the issue carries the hold, so a held
issue consumes no WIP slot and never appears among the issues holding the cap. It SHALL then refuse
unless the issue is `status:ready-for-implementation`. It SHALL refuse when the number of issues
already `status:in-progress` has reached the configured WIP limit. It SHALL set `status:in-progress`
**before** the first commit, reuse the proposal's PR rather than opening a second, and warn — without
blocking — when the branch's file footprint overlaps another in-flight change.

On completion, when it marks the PR ready, it SHALL set `status:code-review` **and** apply the hold;
removing that hold is what lets `/aio:sync` run.

#### Scenario: the hold is checked before the cap

- **WHEN** `/aio:implement` runs on a held issue while the WIP limit is already reached
- **THEN** it refuses naming the hold, not the cap, and the held issue is counted in no WIP tally

#### Scenario: WIP cap reached

- **WHEN** the WIP limit is 2 and two issues are already in progress
- **THEN** implement refuses, lists the in-progress issues, and names `/aio:sync` as the way to
  free a slot

#### Scenario: overlap is advisory

- **WHEN** the branch touches files another in-flight change also touches
- **THEN** implement warns and continues

#### Scenario: the ready PR is held

- **WHEN** implement marks the PR ready for review
- **THEN** the issue is `status:code-review` and carries the hold, written as one advance

### Requirement: tunable process values have one home

The WIP limit, the hold label, and any other tunable process value SHALL be defined once, in
`.claude/workflow.json`, and read from there by every command. No command file, skill file or
document SHALL hardcode the hold's name; each SHALL refer to it as the value of `holdLabel`.

#### Scenario: raising the WIP limit

- **WHEN** the WIP limit changes
- **THEN** exactly one file changes and every command observes the new value

#### Scenario: the hold's name has one home

- **WHEN** the repository is searched for the literal hold label outside `.claude/workflow.json`
- **THEN** no command or document contains it

### Requirement: sync verifies green before suppressing any signal

`/aio:sync` SHALL refuse while the issue carries the hold, before any other check, so nothing is
merged, archived or appended to the retro log. It SHALL then, in this order: refuse a draft PR;
refuse when the check rollup is failing or pending; **verify CI is green while the last
implementation commit is still the PR head**; re-run the overlap check against both
`status:in-progress` issues and open `status:code-review` PRs; append the retro entry; archive the
OpenSpec change on the branch; and only then create the close-out commit that carries `[skip ci]`.

#### Scenario: a held PR does not merge

- **WHEN** `/aio:sync` runs on a held issue whose PR is green and ready
- **THEN** it refuses naming the hold, and nothing is merged, archived or written to the retro log

#### Scenario: green is verified before the marker exists

- **WHEN** sync runs
- **THEN** the CI-green check happens before the `[skip ci]` commit is created, never after

#### Scenario: a red PR cannot sync

- **WHEN** any required check is failing or pending
- **THEN** sync refuses and names the failing check

### Requirement: status is read-only

`/aio:status` SHALL report an issue's lifecycle position, whether it carries the hold, its PR state,
and any drift between them, and SHALL NOT modify anything. It SHALL refuse nothing: a hold is a fact
it reports, and reporting it is precisely what a stalled reviewer needs.

#### Scenario: drift is reported, not corrected

- **WHEN** an issue is `status:in-progress` but its PR is still a draft with no commits
- **THEN** status reports the discrepancy and changes nothing

#### Scenario: a hold is reported, not obeyed

- **WHEN** `/aio:status` runs on a held issue
- **THEN** it reports the hold, names removing it as the next act and by whom, and refuses nothing
