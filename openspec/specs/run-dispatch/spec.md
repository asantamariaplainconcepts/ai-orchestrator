# run-dispatch Specification

## Purpose
TBD - created by archiving change dispatch-substrate. Update Purpose after archive.
## Requirements
### Requirement: a dispatched Run reaches exactly one job execution

Dispatching SHALL place a message carrying only the Run's id on the dispatch queue. A consumer
SHALL delete the message as soon as it claims it, before performing any work, so a consumer that
dies is never re-dispatched — BR-004 forbids automatic retries, and queue redelivery is one.
Re-running a Run SHALL require a deliberate human action (BR-013 *Run now*).

#### Scenario: the consumer crashes mid-work

- **WHEN** a job claims a message and then terminates without completing
- **THEN** no second job is started for that Run, and the Run is left in a state a human can
  re-trigger

#### Scenario: the message carries no business data

- **WHEN** a dispatch message is inspected on the queue
- **THEN** it contains the Run id and nothing else — the job reads Run, Story and Automation
  from the database

### Requirement: queue length starts jobs

A KEDA-scaled Container Apps Job SHALL start when messages are present on the dispatch queue and
SHALL scale to zero when it is empty. The scaler SHALL NOT enforce the per-project concurrency
cap (BR-002); Runs beyond a project's cap are not enqueued, so the cap has exactly one home.

#### Scenario: a message arrives

- **WHEN** a message is enqueued and the job is not running
- **THEN** a job execution starts, and its completion is observable afterwards

#### Scenario: the queue is empty

- **WHEN** no messages remain
- **THEN** no job replicas run and nothing bills

### Requirement: Agent jobs run under their own identity

The dispatch job SHALL authenticate with a user-assigned identity distinct from the portal's,
granted only what it needs: pull from the registry, read its secrets by name, reach the database.
Secrets SHALL be resolved by name through `ISecretResolver` (BR-010); no credential SHALL appear
in the job's configuration.

#### Scenario: inspecting the job's configuration

- **WHEN** the deployed job's environment variables are read
- **THEN** they carry a vault URI and secret names only — no token, password or connection string

#### Scenario: identities are not shared

- **WHEN** the portal's identity and the job's identity are compared
- **THEN** they are different principals with different role assignments

### Requirement: the dispatch substrate follows the habitat, and ambiguity refuses

Dispatch SHALL have one contract and two substrates, chosen by **configuration presence** and never
by an environment name: a queue connection string SHALL compose the queue substrate, and its
absence SHALL compose an in-process substrate backed by the same durable outbox integration events
use.

A habitat configured for **both** substrates, or for **neither**, SHALL refuse at startup naming
which contract is ambiguous. Choosing one silently is the failure this rule exists to prevent, and
a habitat contract is asked rather than inferred (ADR-0010).

**A habitat with a queue SHALL NOT compose an in-process consumer**, whatever else it composes.
The portal already holds everything needed to execute a Run; only a consumer is missing, and where
a queue exists that separation is the credential boundary between the portal and the worker.

#### Scenario: no queue means one process

- **WHEN** a Run is dispatched in a habitat with no queue connection string
- **THEN** it is published to the durable outbox and consumed in the same process, with no queue
  and no second container involved

#### Scenario: a queue means the deployed path, unchanged

- **WHEN** a Run is dispatched in a habitat configured with a queue connection string
- **THEN** it takes the queue substrate exactly as before, and no in-process consumer exists

#### Scenario: ambiguity is refused, not resolved

- **WHEN** a habitat is configured for both substrates, or for neither
- **THEN** startup fails naming which contract is ambiguous

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
