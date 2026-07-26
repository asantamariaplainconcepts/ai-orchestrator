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

