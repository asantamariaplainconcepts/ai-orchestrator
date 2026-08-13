# workflow-commands Specification

## Purpose
TBD - created by archiving change ai-delivery-layer. Update Purpose after archive.
## Requirements
### Requirement: the commands are the public API

Contributors SHALL drive the workflow through `/aio:grill`, `/aio:propose`, `/aio:implement`,
`/aio:sync`, `/aio:ship`, `/aio:refine`, and `/aio:status`. OpenSpec SHALL be reachable only through
`/opsx:*` primitives that skills wrap, so the spec engine stays replaceable without touching workflow
policy.

`/aio:ship` SHALL be an additional route, never a replacement: the staged commands keep their gates,
their refusals and their hold behaviour unchanged, and the staged path remains the default for work
whose spec or diff a person intends to read.

#### Scenario: swapping the spec engine

- **WHEN** the spec tool changes
- **THEN** the `/opsx:*` layer and its skills change, and the `/aio:*` gates do not

#### Scenario: the unattended route changes nothing about the staged one

- **WHEN** `/aio:ship` exists
- **THEN** `/aio:propose` and `/aio:implement` still apply the hold at their two review stages, and
  a contributor who never invokes `/aio:ship` observes the loop exactly as before

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

**In unattended mode** (invoked by `/aio:ship`) it SHALL advance the status **without** the hold, and
its refusals SHALL become halts that apply the hold and comment the reason. Nothing else about it
differs.

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

#### Scenario: unattended mode advances the status alone

- **WHEN** `/aio:propose` runs in unattended mode and opens the draft PR
- **THEN** the issue is `status:ready-for-implementation` and carries no hold, and the run continues
  into `/aio:implement` without waiting

### Requirement: implement respects the WIP cap and the same PR

`/aio:implement` SHALL refuse — **before** the WIP gate — while the issue carries the hold, so a held
issue consumes no WIP slot and never appears among the issues holding the cap. It SHALL then refuse
unless the issue is `status:ready-for-implementation`. It SHALL refuse when the number of issues
already `status:in-progress` has reached the configured WIP limit. It SHALL set `status:in-progress`
**before** the first commit, reuse the proposal's PR rather than opening a second, and warn — without
blocking — when the branch's file footprint overlaps another in-flight change.

On completion, when it marks the PR ready, it SHALL set `status:code-review` **and** apply the hold;
removing that hold is what lets `/aio:sync` run.

**In unattended mode** (invoked by `/aio:ship`) it SHALL set `status:code-review` **without** the
hold, and its refusals — the WIP cap included — SHALL become halts that apply the hold and comment
the reason. The cap itself SHALL be enforced unchanged, and the overlap warning SHALL remain
advisory.

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

#### Scenario: unattended mode hands the PR straight on

- **WHEN** `/aio:implement` runs in unattended mode and marks the PR ready
- **THEN** the issue is `status:code-review` with no hold, and the run continues into `/aio:sync`

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

**In unattended mode** (invoked by `/aio:ship`) the invocation SHALL be DEC-016's recorded go-ahead,
and the retro reflection points and the squash subject SHALL be derived and recorded without being
presented for confirmation, with the retro entry marking its reflections as unconfirmed. Every gate
and every ordering above SHALL apply unchanged; each refusal SHALL become a halt that applies the
hold and comments the reason.

#### Scenario: a held PR does not merge

- **WHEN** `/aio:sync` runs on a held issue whose PR is green and ready
- **THEN** it refuses naming the hold, and nothing is merged, archived or written to the retro log

#### Scenario: green is verified before the marker exists

- **WHEN** sync runs
- **THEN** the CI-green check happens before the `[skip ci]` commit is created, never after

#### Scenario: a red PR cannot sync

- **WHEN** any required check is failing or pending
- **THEN** sync refuses and names the failing check

#### Scenario: unattended mode asks nobody and skips no gate

- **WHEN** `/aio:sync` runs in unattended mode on a green, ready, unheld PR
- **THEN** it merges without presenting the retro or the title for confirmation, and the CI-green,
  overlap, retro, archive and squash-lint steps all still run in their defined order

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

`/aio:status` SHALL report an issue's lifecycle position, whether it carries the hold, its PR state,
and any drift between them, and SHALL NOT modify anything. It SHALL refuse nothing: a hold is a fact
it reports, and reporting it is precisely what a stalled reviewer needs.

#### Scenario: drift is reported, not corrected

- **WHEN** an issue is `status:in-progress` but its PR is still a draft with no commits
- **THEN** status reports the discrepancy and changes nothing

#### Scenario: a hold is reported, not obeyed

- **WHEN** `/aio:status` runs on a held issue
- **THEN** it reports the hold, names removing it as the next act and by whom, and refuses nothing

### Requirement: gating shell steps cannot be masked by a pipe

Any command step whose exit code gates a decision SHALL either set `pipefail` or check the exit
code explicitly, so a failure piped into another process is never read as success.

#### Scenario: a failing command piped to a formatter

- **WHEN** a gating command's output is piped
- **THEN** its failure still fails the gate

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

On the **reviewed path**, at each of the two human review stages the reviewer's whole act SHALL be
removing the hold. No command SHALL require a further `status:*` change from the reviewer to unlock
the next command, and the command that runs next SHALL find the issue already in its gating state.

An unattended run has no review stage to release: it applies no hold on its happy path, so there is
nothing for a reviewer to clear, and its single authorisation is the invocation. Where such a run
halts, the hold it applies is released the same way as any other — by a person, never by a command.

#### Scenario: spec review is released by one label

- **WHEN** a reviewer validates a proposal and removes the hold
- **THEN** `/aio:implement` proceeds on the already-set `status:ready-for-implementation`, and the
  reviewer set no label

#### Scenario: code review is released by one label

- **WHEN** a reviewer approves the implementation and removes the hold
- **THEN** `/aio:sync` proceeds on the already-set `status:code-review`, and the reviewer set no label

#### Scenario: an unattended run has no hold to clear

- **WHEN** an unattended run completes without halting
- **THEN** no reviewer ever cleared a hold for it, because none was applied — and the merge is
  nonetheless authorised, by the invocation

### Requirement: refine is untouched by the hold

`/aio:refine` SHALL behave identically whether or not the issue carries the hold. It runs after the
merge and gates nothing, so a hold has nothing left to stop.

#### Scenario: a held, merged issue still accepts a retro entry

- **WHEN** `/aio:refine` is invoked on a merged issue that carries the hold
- **THEN** it appends the follow-up retro entry exactly as it would without the hold

### Requirement: an unattended run carries a ready issue to main in one invocation

`/aio:ship <issue>` SHALL take an issue that is `status:ready-for-proposal` and carries no hold, and
without requesting human input at any point afterwards SHALL: create the branch whose name ends with
the change's kebab-case slug from current `origin/<default>`; generate the OpenSpec change; open the
PR; implement the change's tasks on that same branch; mark the PR ready; append the retro entry;
archive the change on the branch; verify the check rollup is green while the last implementation
commit is still the PR head; lint the squash subject and body; squash-merge; set `status:done`; and
report the deploy run the merge triggered.

It SHALL traverse every lifecycle state the reviewed path traverses —
`ready-for-implementation`, `in-progress`, `code-review`, `done` — leaving exactly one `status:*`
label at every moment, so a run that halts is resumable from its label alone.

It SHALL NOT apply the hold on this path, and consequently SHALL NOT need to remove one.

`/aio:ship` SHALL obtain its steps by running `/aio:propose`, `/aio:implement` and `/aio:sync` **in
unattended mode**, never by restating their steps. Each of those three SHALL carry an explicit
unattended clause naming everything that differs, and nothing else about them SHALL differ:

- `/aio:propose` and `/aio:implement` SHALL omit the hold from the `gh issue edit` that advances the
  status, so the status advances alone.
- `/aio:sync` SHALL treat the `/aio:ship` invocation as DEC-016's recorded go-ahead, and SHALL
  record the retro reflection points and the squash subject it derives **without** presenting them
  for confirmation, marking the retro entry's reflections as unconfirmed.
- Every refusal in all three SHALL become a halt as defined below.

Because reuse is by invocation, every guarantee the staged commands carry SHALL hold here by
construction and SHALL NOT be restated in `/aio:ship`: the worktree assertion before any mutation,
the branch-name and fresh-base requirements, one PR per issue, the CI-green check before the
`[skip ci]` close-out commit, the linted squash message, `[skip ci]` never reaching `main`, and
`pipefail` on any gating shell step.

#### Scenario: a ready issue reaches main unattended

- **WHEN** `/aio:ship` is invoked on an unheld `status:ready-for-proposal` issue and nothing halts it
- **THEN** `main` gains exactly one squash commit carrying the implementation, the retro entry, the
  synced `openspec/specs/` and the archived bundle; the issue is `status:done`; and the run requested
  no human input after the invocation

#### Scenario: no hold is applied on the happy path

- **WHEN** an unattended run completes
- **THEN** the issue never carried the hold at any point during the run, so nothing had to clear one

#### Scenario: the ordering lives in one place

- **WHEN** `/aio:ship` is read
- **THEN** it names the three commands it runs and what unattended mode changes about each, and does
  not restate their gates, their orderings or their guarantees — so a fix to a staged command's
  ordering reaches the unattended route without a second edit

#### Scenario: the invocation is refused like any other command

- **WHEN** `/aio:ship` is invoked on a held issue, on an issue that is not
  `status:ready-for-proposal`, or from a session whose directory does not match
  `git rev-parse --show-toplevel`
- **THEN** it refuses before any git or GitHub mutation, names the reason and the way forward, and no
  branch, PR or label change exists afterwards

#### Scenario: the merge is still gated on green

- **WHEN** an unattended run reaches the close-out commit
- **THEN** the check rollup was verified green while the last implementation commit was still the PR
  head, exactly as `/aio:sync` requires, and the squash body reaching `main` contains no `[skip ci]`

### Requirement: an unattended halt applies the hold and hands back

An unattended run SHALL stop rather than guess or force, and SHALL stop on: a failing or genuinely
pending check rollup at the merge precondition; the WIP limit in `.claude/workflow.json` already
reached by `status:in-progress` issues when it reaches the implementation stage; and any question the
issue and its spec do not answer.

On stopping it SHALL apply the hold, comment the specific reason on the issue, leave the issue's
current `status:*` label in place, and perform no further mutation. Applying the hold is permitted;
removing one remains forbidden to every command, this one included.

A halted change SHALL be resumable by the ordinary staged command for its current label, once a
person clears the hold, with no repair step and no duplicated branch, PR or archive directory.

#### Scenario: a red rollup halts before the close-out commit

- **WHEN** the check rollup is failing or pending as the run reaches the merge precondition
- **THEN** no close-out commit is created, the issue stays `status:code-review` and carries the hold,
  and a comment names the failing or pending check

#### Scenario: the WIP cap halts the run, it does not bypass it

- **WHEN** the WIP limit is already reached as the run reaches the implementation stage
- **THEN** the run halts with the PR still a draft, the issue at `status:ready-for-implementation`
  and held, and a comment listing the issues holding the cap and naming `/aio:sync` as the way to
  free a slot

#### Scenario: an unanswered question halts instead of being guessed

- **WHEN** the run meets a question the issue and its spec do not answer
- **THEN** it applies the hold, comments the specific question, changes no `status:*` label, and
  makes no further commit

#### Scenario: a person resumes a halted run

- **WHEN** a person clears the hold on a halted change and invokes the staged command for its current
  label
- **THEN** that command proceeds from the existing branch and PR, and afterwards exactly one archive
  directory exists for the change

#### Scenario: even a halt never clears a hold

- **WHEN** the `/aio:*` commands and skills are searched for a removal of the hold label
- **THEN** none exists, `/aio:ship` included

### Requirement: an unattended change says so in its record

Because an unattended run has nobody to ask for DEC-016's in-session go-ahead, the invocation SHALL
be the recorded authorisation, and the record SHALL make the absence of review legible: the PR body
and the `docs/process/retro-log.md` entry SHALL each state that the change landed with no human
reading its spec or its diff, and SHALL name `/aio:ship` as what ran.

#### Scenario: the retro log distinguishes reviewed from unreviewed changes

- **WHEN** a change shipped unattended is read in `docs/process/retro-log.md`
- **THEN** its entry states that no human read the spec or the diff and names `/aio:ship`

#### Scenario: the PR carries the same statement

- **WHEN** the PR of an unattended run is read
- **THEN** its body states the change was shipped unattended by `/aio:ship`, so the merge commit's
  provenance is readable from GitHub alone

