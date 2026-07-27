# Proposal: approval-gate

## Why

Issue #22 (UC-015 + UC-013). `requiresApproval` has been a field with no behaviour since #14,
and **five consecutive changes have shipped the same stated limitation** — "the two-phase lane
is not implemented yet". DEC-040 chose the richer shape deliberately: the Agent proposes, a
human decides, execution follows. This makes it real and retires the refusal.

## What Changes

- **Phase 1 — the Plan.** An approval-gated Run prepares a workspace, runs the Agent with a
  plan instruction, stores the Plan on the Run, and pauses at `AwaitingApproval`. Nothing is
  published: a phase-1 PR would be a lie, and a plan written without seeing the code is a guess,
  so it clones but publishes nothing.
- **The decision.** `POST .../runs/{runId}/approve` stamps `ApprovedAt`, returns the Run to
  `Queued` and re-enqueues it; `POST .../runs/{runId}/reject` ends it `Cancelled` (terminal,
  freeing the Story). No fifth state — the worker routes on the record it already has.
- **Phase 2 — execution with the approved Plan in the instruction.** Without that, approval is
  theatre: the human would be blessing a document the Agent never sees again.
- **A Run detail route**, which UC-013 assumes and #20 did not build: state, timestamps, usage,
  output link, the Plan as sanitised markdown (#37's pipeline), Approve and Reject.
- **The limitation goes.** `RunCreation.TwoPhaseRefused` and its error disappear; the tests
  that asserted the refusal now assert the pause.

## Impact

- Affected specs: `run-orchestration` (the gate and its rules), `agent-execution` (two phases,
  each with its own timeout).
- Touched: Runs module (Plan/ApprovedAt + migration, RunCreator's lane split, executor phases,
  two decision slices, ListRuns), frontend (detail route + catalog), Runs functional tests,
  ARCHITECTURE.md.
- Out of scope: approver identity (#13), editing a Plan, cancelling from the page (#23),
  re-planning a rejected Run (BR-004).
