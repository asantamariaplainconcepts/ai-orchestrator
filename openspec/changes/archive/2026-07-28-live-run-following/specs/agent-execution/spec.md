# agent-execution

## ADDED Requirements

### Requirement: a Run's output is observable while it executes

Agent output SHALL be persisted incrementally while a Run executes, and readable through the
Run's log endpoint together with whether the Run has finished. The observed lag from a line
being emitted to it being readable SHALL be at most five seconds. A Run that crashes mid-write
SHALL preserve every line persisted before the crash. A finished Run SHALL serve its full log
from the same read, marked complete. When the log cannot be read, the Run's state SHALL remain
visible and the failure SHALL name itself — never a blank page.

#### Scenario: the log grows during execution

- **WHEN** a Run executes and the runtime emits output
- **THEN** the log read returns the lines so far, within the stated lag

#### Scenario: a crash preserves the partial log

- **WHEN** the runtime dies mid-run
- **THEN** the lines persisted before the crash remain readable

#### Scenario: finished means complete

- **WHEN** a terminal Run's log is read
- **THEN** the full output is returned and the response says it is complete

#### Scenario: unreadable log, visible Run

- **WHEN** the log store cannot serve
- **THEN** the Run's state still renders and the log area names the failure
