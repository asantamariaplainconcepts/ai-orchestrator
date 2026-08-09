# run-dispatch Specification

## Purpose
TBD - created by archiving change dispatch-substrate. Update Purpose after archive.
## Requirements
### Requirement: the dispatch substrate follows the habitat, and ambiguity refuses

Dispatch SHALL have one contract and **one substrate**: the same durable Postgres outbox the
product's integration events already use. A Run accepted for dispatch SHALL survive the process
dying and SHALL be redelivered after restart, because the durability is the outbox and never a
transport.

The consumer SHALL be composed only by a host that should execute Runs, never acquired by
registering the producer.

A habitat still naming the retired queue connection string SHALL be refused at startup, naming the
substrate that replaced it — a key that quietly stopped meaning anything is how a deployment ends
up running something nobody chose.

#### Scenario: a dispatch survives the process dying

- **WHEN** a Run is accepted for dispatch and the process terminates before execution
- **THEN** it is redelivered after restart and reaches a terminal state

#### Scenario: the retired queue refuses by name

- **WHEN** a habitat starts with the queue connection string still configured
- **THEN** composition refuses, naming the outbox as what replaced it — never a silently ignored
  setting

### Requirement: an in-process dispatch survives the process dying

Where dispatch is in-process, its durability SHALL come from the outbox rather than from the
transport: a Run accepted for dispatch and lost to the process terminating SHALL be redelivered
after restart and SHALL reach a terminal state.

Redelivery SHALL change nothing for a Run that is no longer awaiting execution. The guard that
makes a duplicate delivery a no-op — a Run not in the state dispatch expects is logged and
dropped — SHALL hold on this substrate as it does on the queue, including for a Run a reaper has
already terminated. BR-004 governs Runs, which are never re-run automatically; redelivering the
substrate's own message is not re-running a Run.

#### Scenario: a killed process redelivers

- **WHEN** a Run is dispatched in-process and the process terminates before it completes
- **THEN** the Run is redelivered after restart and reaches a terminal state

#### Scenario: a redelivery after termination executes nothing

- **WHEN** a message is redelivered for a Run that has already reached a terminal state
- **THEN** nothing executes, and the Run's outcome is unchanged

#### Scenario: the lifecycle is indistinguishable

- **WHEN** a Run executes on the in-process substrate
- **THEN** the reaper, the resume checker and backlog polling all run, and the Run's states and
  timestamps are the same as they would be on the queue

### Requirement: the host's sessions enter the pod by deliberate default

Where the pod substrate is active, the host's agent-CLI configuration SHALL be provided to the
pod by default and the operator SHALL be able to turn it off; the transcript SHALL name the
credential source either way. The mechanism SHALL be fixed by observing a real CLI in a pod —
recorded, not assumed — and the consequence SHALL be stated where the option lives: pod Runs act
and bill as those sessions.

#### Scenario: the default carries the session and says so

- **WHEN** a pod Run executes with the default in place
- **THEN** the CLI in the pod can use the host's session, and the transcript names it as the
  credential source
