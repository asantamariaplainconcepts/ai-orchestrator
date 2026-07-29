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

While a Run executes, newly recorded output SHALL reach every open viewer of that Run in under
one second, without the viewer polling. Delivery SHALL be best-effort: when it is unavailable the
page SHALL fall back to the existing periodic read, and no output SHALL be lost either way,
because the durable record is unchanged. The agent's execution SHALL NOT depend on delivery in
any way — a Run behaves identically whether or not anybody is watching, and whether or not the
delivery path works.

The product's recorded decision about the latency budget SHALL state the same figures the
implementation uses.

A watcher joining a Run already in progress SHALL receive every line committed before it joined and
every line committed while it was joining. The subscription SHALL be established before the initial
read, so that lines arriving during the handshake are delivered rather than waiting for a later
reconciliation pass. Because that ordering produces an overlap between the pushes and the read, each
delivery SHALL carry the position it starts at and each read SHALL carry the position the next line
will occupy, so a viewer can discard what it already has. A delivery whose lines the viewer already
holds SHALL change nothing it displays.

The delivery mechanism SHALL NOT retain state for Runs that have reached a terminal state: a
terminal Run produces no further output, and its bookkeeping SHALL be released when its final output
is delivered. Concurrent deliveries for the same Run SHALL NOT produce a duplicated frame.

#### Scenario: a line appears while the Run executes

- **WHEN** the runtime emits a line and a viewer has the Run open
- **THEN** the line is rendered in under one second, without the viewer having requested it

#### Scenario: two viewers see one Run

- **WHEN** two viewers have the same Run open
- **THEN** both receive every line, and the work the portal does per line does not grow with the
  number of viewers

#### Scenario: delivery is unavailable

- **WHEN** the live path cannot be established or is lost
- **THEN** the page falls back to the periodic read and the full output remains available

#### Scenario: the Run does not care

- **WHEN** the live path is broken or nobody is watching
- **THEN** the Run executes and records its output exactly as it otherwise would

#### Scenario: a window opened mid-Run misses nothing

- **WHEN** a Member opens a Run's page while it is executing
- **THEN** every line committed before and during the subscription is visible within the stated lag
  budget, without waiting for a reconciliation pass

#### Scenario: an overlapping delivery is discarded, not appended

- **WHEN** a delivery carries lines the viewer's initial read already returned
- **THEN** those lines are discarded and nothing is shown twice

#### Scenario: a terminal Run leaves nothing behind

- **WHEN** a Run reaches a terminal state and its last output is delivered
- **THEN** the delivery mechanism retains no bookkeeping for it

#### Scenario: two deliveries for one Run

- **WHEN** two notifications for the same Run are handled concurrently
- **THEN** a watcher receives no duplicated frame

#### Scenario: the decision and the code agree

- **WHEN** the recorded latency decision is compared with the implementation's flush interval
- **THEN** they state the same figure
