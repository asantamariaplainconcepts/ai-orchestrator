# run-orchestration

## ADDED Requirements

### Requirement: a Member cancels a Run, and nothing it started is published

The system SHALL let a Member cancel a Run that is not already terminal, ending it `Cancelled`
immediately (BR-012, DEC-041) — terminal, so the Story is freed (BR-001) and the record shows
the cancellation without inventing a failure reason (BR-014). The worker SHALL observe the
cancellation at its boundaries: a Run cancelled before its runtime is invoked SHALL not invoke
it, and a Run cancelled during an invocation SHALL publish nothing and SHALL NOT have its
cancellation overwritten by the outcome. Cancelling a terminal Run SHALL be refused with its
state named. Cancellation SHALL NOT terminate an Agent already running — that limitation is
documented, not implied.

#### Scenario: a queued Run is discarded

- **WHEN** a `Queued` or `AwaitingApproval` Run is cancelled
- **THEN** it ends `Cancelled`, nothing is enqueued or executed, and the Story can run again

#### Scenario: a Run cancelled mid-flight publishes nothing

- **WHEN** a Run is cancelled while its agent invocation is in progress
- **THEN** no commit, push or pull request happens, and the Run remains `Cancelled` after the
  invocation returns

#### Scenario: a terminal Run cannot be cancelled

- **WHEN** cancellation targets a `Succeeded`, `Failed` or `Cancelled` Run
- **THEN** it is refused with that state named, and nothing changes
