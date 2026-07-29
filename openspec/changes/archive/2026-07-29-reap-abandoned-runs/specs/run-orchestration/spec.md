# run-orchestration

## ADDED Requirements

### Requirement: every Run reaches a terminal state, even when its worker never reports

A Run in a non-terminal executing state SHALL end whether or not the process executing it survives.
The system SHALL periodically end any Run in `Planning` or `Executing` whose start, plus its
Automation's timeout, plus a grace period, is in the past, marking it `Failed` with a reason stating
that it exceeded its timeout without its worker reporting.

That reason SHALL be distinguishable from a timeout the executor enforced itself, because an agent
that was too slow and a worker that disappeared call for different responses.

Ending a Run this way SHALL NOT re-dispatch it and SHALL NOT create another (BR-004). It SHALL free
the Story (BR-001) and release the project's concurrency slot (BR-002), and the Run SHALL appear
wherever failures appear, so the occurrence is visible rather than silent.

The system SHALL NOT end a Run that is still within its deadline, and SHALL NOT overwrite a Run that
has reached a terminal state — a Run that finished between being observed and being written SHALL be
left exactly as it finished.

Overdue-ness SHALL be a property of the Run, not of the sweeping process: a Run that became overdue
while nothing was sweeping SHALL be ended on the next pass.

#### Scenario: a worker that vanished

- **WHEN** a Run has been executing for longer than its Automation's timeout plus the grace period,
  and no worker has reported
- **THEN** it is `Failed`, its reason says its worker never reported, its Story accepts a new Run,
  and the project's concurrency count no longer includes it

#### Scenario: a slow Run inside its deadline

- **WHEN** a Run has been executing for less than its timeout, producing no output at all
- **THEN** it is left untouched

#### Scenario: a Run that finishes as the sweep runs

- **WHEN** a Run reaches a terminal state between being observed as overdue and being written
- **THEN** its outcome stands and the sweep changes nothing

#### Scenario: nothing is retried

- **WHEN** a Run is ended for exceeding its deadline
- **THEN** no Run is dispatched or created in its place

#### Scenario: overdue while nobody was watching

- **WHEN** the sweeping process is restarted after Runs have become overdue
- **THEN** those Runs are ended on the next pass

#### Scenario: the failure is visible

- **WHEN** a Run is ended for exceeding its deadline
- **THEN** it appears in the waiting inbox's failure lane like any other failure
