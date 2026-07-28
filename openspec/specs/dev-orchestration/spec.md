# dev-orchestration Specification

## Purpose
TBD - created by archiving change project-scaffolding. Update Purpose after archive.
## Requirements
### Requirement: one-command dev loop

`src/root/AiOrchestrator.AppHost` (Aspire) SHALL compose the full inner loop — the Server,
PostgreSQL, Azurite (DEC-013), the frontend dev server, the migration step, **and the dispatch
worker** — such that one command runs everything locally. The worker SHALL receive every
resource it composes (database, queue, configuration) and SHALL start automatically, restarting
when it exits so a queued Run is picked up without operator action. The composition SHALL be
documented as exercising the queue contract, matching, execution and pull-request publication —
and as **not** exercising KEDA scaling or Key Vault, whose only proof is Azure.

#### Scenario: cold start

- **WHEN** a fresh clone runs the dev loop after a build
- **THEN** the website is reachable through the Server origin with live API, database, and queue
  emulator

#### Scenario: the worker runs

- **WHEN** the dev loop starts
- **THEN** the dispatch worker starts, reaches its database and queue, and is restarted when it
  exits, so a Run enqueued from the portal is executed without touching the dashboard

#### Scenario: the loop closes locally

- **WHEN** a developer triggers a Run from the portal against the seeded project, using the
  free-model runtime
- **THEN** the Run is executed and reaches a terminal state, observable in the portal, with no
  cloud resources and no AI credential

### Requirement: OpenTelemetry from day 0

`AiOrchestrator.ServiceDefaults` SHALL wire OTel logs, metrics, and traces for every
service, with exporters selected by environment (OTLP locally, Azure Monitor in cloud
per DEC-023). `/api/health` and `/api/alive` SHALL be excluded from traces.

#### Scenario: traces flow locally

- **WHEN** the exemplar endpoint handles a request in the dev loop
- **THEN** its trace is visible in the Aspire dashboard, and health probes produce none

### Requirement: the local composition seeds a demo project

The run composition SHALL seed a demo project, its Connector and an Automation using the free
model, once and idempotently, so the loop is usable on first boot. The seeder SHALL be reachable
only from the local run composition — a deployed host SHALL have no way to invoke it — and SHALL
name the repository it points at from configuration rather than inventing one.

#### Scenario: first boot is usable

- **WHEN** the dev loop starts against an empty database
- **THEN** a project with a Connector and an OpenCode Automation exists

#### Scenario: seeding twice changes nothing

- **WHEN** the dev loop restarts against a database that already has the demo project
- **THEN** nothing is duplicated

#### Scenario: the deployed host cannot seed

- **WHEN** the Server runs without the local composition's flag
- **THEN** the seeder does not run

### Requirement: the whole system runs from a clone on a Docker-only machine

The repository SHALL carry a generated docker-compose description of the full system — portal,
migrations, worker, database, queue emulator — buildable and runnable on a machine with only
Docker and git, contacting no cloud service. The compose SHALL be generated from the same
composition development runs, and CI SHALL fail when the committed artifact drifts from it. The
quickstart SHALL require no AI credential: the default runtime is the free model, and the only
secret anywhere is the operator's own vendor PAT, provided as configuration.

#### Scenario: clone and run

- **WHEN** the quickstart is followed on a Docker-only machine
- **THEN** all images build locally from the repository's Dockerfiles and the portal serves

#### Scenario: zero cloud

- **WHEN** the compose environment runs
- **THEN** no Azure endpoint is contacted — the queue is Azurite, the database a container,
  secrets configuration values

#### Scenario: the artifact cannot drift silently

- **WHEN** the AppHost's composition changes without regenerating the compose
- **THEN** CI fails naming the drift

