# run-orchestration

## ADDED Requirements

### Requirement: an approval-gated Run pauses on its Plan and a human decides

A Run whose Automation has `requiresApproval = true` SHALL produce a Plan, store it on the Run
and pause at `AwaitingApproval` without publishing anything (BR-007, DEC-040). Approving SHALL
stamp the approval, return the Run to `Queued` and re-enqueue it for execution; rejecting SHALL
end the Run `Cancelled` — terminal, freeing the Story (BR-001). A Run awaiting approval SHALL
be subject to no timeout (BR-006) and SHALL NOT count toward the project cap (BR-002), while
still holding its Story against a second Run (BR-001). The Plan and the decision SHALL be part
of the Run's record (BR-014). No code path SHALL any longer refuse the two-phase lane as
unimplemented.

#### Scenario: the Agent proposes and the Run waits

- **WHEN** an approval-gated Run executes
- **THEN** its Plan is stored, its state is `AwaitingApproval`, and no branch or pull request
  was created

#### Scenario: approval resumes into execution

- **WHEN** the Plan is approved
- **THEN** the Run is re-enqueued, executes the implement path, and ends `Succeeded` with a
  pull request — as the single-phase lane does

#### Scenario: rejection ends it

- **WHEN** the Plan is rejected
- **THEN** the Run ends `Cancelled`, nothing is enqueued, and the Story can run again

#### Scenario: waiting is free and untimed

- **WHEN** a Run sits in `AwaitingApproval`
- **THEN** no timeout applies to it and the project's concurrency cap is unaffected, yet a new
  match on the same Story still creates no second Run

### Requirement: a Run's detail is readable, with its Plan

The portal SHALL offer a Run detail view reachable from the Runs table showing state,
timestamps, usage, output link and — when present — the Plan rendered as sanitised markdown,
with controls to approve or reject while the Run awaits approval.

#### Scenario: the reviewer reads the Plan where the decision is made

- **WHEN** a Member opens a Run awaiting approval
- **THEN** the Plan renders and both decisions are available; hostile markdown in the Plan is
  inert
