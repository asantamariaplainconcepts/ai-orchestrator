# dev-orchestration

## ADDED Requirements

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
