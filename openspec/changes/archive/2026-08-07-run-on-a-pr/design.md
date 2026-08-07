## Context

Mapped before designing (the execution-path exploration is the source for every claim here):
`Run.Create` has exactly one production caller (`RunCreator`), BR-001 is enforced as a trio — a
filtered unique index generated from `RunStates.Active`, a pre-check, and a unique-violation race
arm — and, decisive for this design, **the publish step is retired (DEC-062)**: the Agent itself
pushes and opens pull requests with the credential in its environment, every runtime hard-codes
`OutputLink: null`, and `ICodeWorkspace.Publish`'s one production caller is the starter installer.
The named-branch checkout this feature needs already exists (`Prepare(coordinates, branch, …)`,
the install path). `AgentInstruction.Prompt` is free-form and `Action` is read by no runtime.

**A defect in #274 falls out of this map.** Its product-created marker joins open changes against
`Run.OutputLink` — a column nothing writes any more, so the marker can never fire in production.
Its functional test passed by seeding the column at the persistence layer: a test that provisioned
its own precondition (ADR-0002's shape). The fix rides here because this change already modifies
that requirement's surface: match on the head branch the ceremony names (`run/<id>` carries its
Run id) and, for change-targeted Runs, the recorded target. The delta spec rewrites the join
sentence; the retro owns the lesson.

## Goals / Non-Goals

**Goals:** launch a Run on any open change with typed text; the change updates in place; one
active Run per change; the instruction is a record; the #274 marker actually fires.

**Non-Goals:** approval phases for these Runs (the launch is the intent, UC-012's reasoning);
local-folder execution of change Runs (pod lane first; a local Run never pushes, so "the change
updates" is unsatisfiable there); auto-triggering from vendor events; prompt files as the
instruction source; in-product review of the result.

## Decisions

### D1 — Widen the Run, do not clone it

The Run gains an optional change target (number, URL, head branch) and its Story and Automation
become optional with it, under one stated invariant: **exactly one target** — a Story with an
Automation, or a change with an instruction. A second aggregate would duplicate dispatch,
transcript, cost and cancellation surfaces, and "the PR-run mirrors the Run's rules" is the
property two aggregates satisfy on day one and silently stop satisfying (#151's lesson, one level
up). Migration: `VendorStoryId` and `AutomationId` become nullable, three target columns and an
`Instruction` text column arrive.

### D2 — Concurrency mirrors BR-001's trio exactly

A filtered unique index on `(ProjectId, TargetChangeNumber)` over `RunStates.ActiveStateFilter()`
— the same generated filter, so the two rules cannot drift apart — plus the pre-check and the
`IsDuplicateActiveRun`-shaped race arm converging on the same refusal. Story Runs and change Runs
cannot contend by construction: each rule's index covers only rows where its identity column is
non-null.

### D3 — The vendor answers what a number means

The launch names a change number; the server resolves URL and head branch through
`IChangeReader.Open` at launch time and refuses a number not among the open changes. Trusting the
caller's URL/branch would let any Trigger-permission holder point an agent push at an arbitrary
branch under the product's credential (BR-008 and plain safety agree here). Cost: one vendor read
per launch — a human gesture, not a poll.

### D4 — The executor forks on the target, not on a new pipeline

`RunExecutor.Invoke` branches where the target decides: a change Run skips the Story read, skips
prompt-file resolution (the recorded instruction is the prompt body, framed with the change's
number/title/branch), prepares the workspace with the **named-branch** overload, and skips
`HandOn` (labels are a Story concept). Everything else — runtime selection, credentials, phases
collapse to one (no approval), transcript, usage, terminal states — is the same code it already
was. Runtime: the launch may name one of the registered runtimes; absent, the executor uses the
same default the Automation form defaults to. Timeout: BR-005's default, there being no
Automation to carry one.

### D5 — `GetRunChanges` resolves by what the Run knows

A change-targeted Run already holds its change number, so its changes view reads
`IChangeFileReader.ForChange(projectId, number)` — one added Contracts member beside `ForStory`,
same record shape — instead of resolving through a Story it does not have.

### D6 — Display identity: the change number where the Story id was

`ListRuns` carries the target (number + URL); the Run screen titles change Runs by `PR #n`, links
to the vendor instead of a Story, and shows the instruction; the board's latest-run-per-story
grouping filters change Runs out (they have no Story to group under).

### D7 — The marker matches branches, not a retired column

`GetInboxChanges` marks a change as the product's own when its head branch parses as `run/<guid>`
and that Run exists in the project, or when a change-targeted Run records that change as its
target. The `OpenChangeView.HeadBranch` field added by #274 exists for exactly this question.

## Risks / Trade-offs

- **Nullable Story/Automation ripples through every Runs read.** → The invariant lives in the two
  `Run.Create` shapes; each surface was enumerated from the map (inbox, pulse, list, changes,
  board) and the delta names the tolerance rule for grouping surfaces.
- **An agent instructed ad hoc pushes with the product's credential.** → Scope is unchanged from
  implement Runs (same credential, same push rights); what is new is only who typed the prompt,
  and Trigger permission already gates that human.
- **A stale head branch at prepare time.** → Stage-named checkout failure; the Run fails readable.
- **The unique index trio must not drift from BR-001's.** → Both filters are generated from
  `RunStates.Active`; the migration test pattern (`OutputLabelMigration_Should_Constraint`) covers
  the new migration applying cleanly.

## Migration Plan

One EF migration in the Runs module (nullable story/automation, target columns, instruction,
filtered unique index). Applied at startup as every module migration is; rollback is the
generated Down. No data backfill: existing rows keep their Story shape.

## Open Questions

None.
