## MODIFIED Requirements

### Requirement: the phase timeout ends the Run

The Automation's timeout SHALL bound **the phase**, not the runtime invocation alone. The clock
starts when the phase's work starts, and every step the product performs inside the phase before the
Agent — preparing the workspace, running a configured setup command (`local-code-source`) — spends
that same budget. The runtime SHALL be invoked bounded by what remains.

A runtime execution exceeding what remains SHALL be killed and the Run marked `Failed` naming the
limit (BR-005). Where the budget is exhausted **before** the runtime can be invoked, the runtime
SHALL NOT be invoked at all and the Run SHALL end `Failed` naming the same limit — a Run that ran out
of time names the clock, whichever step was holding it.

Queued and human waits do not count (BR-006).

This is why setup gets no second timeout: a limit of its own would let one Run spend a full budget
preparing and another full budget working, exceeding the ceiling DEC-054 places on a phase.

#### Scenario: the agent overruns

- **WHEN** the runtime exceeds the timeout it was invoked with
- **THEN** the process is killed and the Run ends Failed naming the timeout

#### Scenario: preparation spends the same budget

- **WHEN** a Run performs work inside the phase before invoking the runtime
- **THEN** the runtime is invoked bounded by the timeout minus what that work consumed, never by the
  full timeout again

#### Scenario: the budget is gone before the agent starts

- **WHEN** the phase's budget is exhausted before the runtime is invoked
- **THEN** the runtime is not invoked and the Run ends Failed naming the limit
