# run-orchestration

## ADDED Requirements

### Requirement: a worker does not begin a phase it cannot finish

A worker whose remaining execution budget is less than one full phase timeout SHALL stop claiming
work and exit, leaving unclaimed messages for a worker started with a full budget. It SHALL NOT begin
a phase it knows it cannot complete.

This prevents the failure the sweeper recovers from. Recovery cannot be complete — a container may be
evicted at any moment — but a worker knowingly starting work it cannot finish is a choice.

#### Scenario: a worker near the end of its budget

- **WHEN** a worker's remaining budget is less than one phase timeout and the queue is not empty
- **THEN** it claims nothing further and exits, and the messages remain for the next worker

#### Scenario: a worker with budget to spare

- **WHEN** a worker's remaining budget exceeds one phase timeout
- **THEN** it claims and executes as normal

## MODIFIED Requirements

### Requirement: a Run's output reaches watchers as it is recorded

Output SHALL reach a watching Member as it is recorded, within a stated lag budget, and the product's
recorded decision about that budget SHALL state the same figures the code uses.

A watcher joining a Run already in progress SHALL receive every line committed before it joined and
every line committed while it was joining. The subscription SHALL be established before the initial
read, so that lines arriving during the handshake are delivered as pushes rather than waiting for a
later reconciliation pass; an overlap between the pushes and the read is expected and SHALL be
resolved by sequence, because a redelivered push must be handled regardless.

The push mechanism SHALL NOT retain state for Runs that have reached a terminal state: a terminal
Run produces no further output, and its bookkeeping SHALL be released when its final output is
delivered. Concurrent deliveries for the same Run SHALL NOT produce a duplicated frame for a watcher.

#### Scenario: a window opened mid-Run misses nothing

- **WHEN** a Member opens a Run's page while it is executing
- **THEN** every line committed before and during the subscription is visible within the stated lag
  budget, without waiting for a reconciliation pass

#### Scenario: a terminal Run leaves nothing behind

- **WHEN** a Run reaches a terminal state and its last output is delivered
- **THEN** the push mechanism retains no bookkeeping for it

#### Scenario: two deliveries for one Run

- **WHEN** two notifications for the same Run are handled concurrently
- **THEN** a watcher receives no duplicated frame

#### Scenario: the decision and the code agree

- **WHEN** the recorded latency decision is compared with the implementation's flush interval
- **THEN** they state the same figure
