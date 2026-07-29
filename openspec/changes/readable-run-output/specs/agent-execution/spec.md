# agent-execution

## MODIFIED Requirements

### Requirement: a Run's output is observable while it executes

Agent output SHALL be persisted incrementally while a Run executes, and readable through the
Run's log endpoint together with whether the Run has finished. The observed lag from a line
being emitted to it being readable SHALL be at most five seconds. A Run that crashes mid-write
SHALL preserve every line persisted before the crash. A finished Run SHALL serve its full log
from the same read, marked complete. When the log cannot be read, the Run's state SHALL remain
visible and the failure SHALL name itself — never a blank page.

This SHALL hold for **every** runtime, including the default one. A runtime SHALL NOT be configured to
emit its output as a single document at exit, because a log that arrives only when the work is over is
not observable while the work happens, whatever the read endpoint does.

Where a runtime emits a stream of events rather than one document, its result — success, the reply, and
the usage — SHALL be read from the stream's terminal result event, not by parsing the whole stream as
one document.

A stream carrying **no** terminal result event SHALL fail the Run with the raw streams as evidence,
because the product cannot then say what the agent did, and the reply of a simple action becomes a
comment on somebody's Story — a Run reported as successful whose reply is raw stream text would publish
that. A missing **usage block inside** a result event SHALL remain unknown on an otherwise successful
Run: only the absent result event is a broken contract.

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

#### Scenario: the default runtime is not silent until it exits

- **WHEN** a Run whose runtime is the default one executes
- **THEN** lines are readable while it is still executing, not only after the process ends

#### Scenario: a streamed result is still a result

- **WHEN** a runtime's output is a stream of events and the Run succeeds
- **THEN** the reply and the usage come from the terminal result event, and the Run is recorded as
  succeeded

#### Scenario: no terminal event is a broken contract

- **WHEN** no terminal result event can be read from a stream, even though the process exited
  successfully
- **THEN** the Run fails with the raw streams as evidence, rather than being reported as succeeded with
  stream text as its reply

#### Scenario: a result event with no usage block

- **WHEN** a terminal result event carries a reply but no readable usage
- **THEN** the Run succeeds with that reply and its usage reads unknown
