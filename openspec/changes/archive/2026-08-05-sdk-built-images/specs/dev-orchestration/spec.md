# dev-orchestration — delta for sdk-built-images

## MODIFIED Requirements

### Requirement: the whole system runs from a clone on a Docker-only machine

The repository SHALL carry a generated docker-compose description of the full system — portal,
migrations, worker, database, queue emulator — runnable on a machine with only Docker and git,
contacting no cloud service beyond public image registries. The images SHALL be published to a
public registry by CI on merge to the default branch, tagged by commit SHA, and produced by the
toolchain's own container publish — no Dockerfile backs a self-host image. The compose SHALL be
generated from the same composition development runs, and CI SHALL fail when the committed
artifact drifts from it. The quickstart SHALL require no AI credential and SHALL build no image
locally: the default runtime is the free model, and the only secret anywhere is the operator's
own vendor PAT, provided as configuration.

#### Scenario: clone and run

- **WHEN** the quickstart is followed on a Docker-only machine
- **THEN** all images are pulled from the public registry by tag — nothing builds locally — and
  the portal serves

#### Scenario: zero cloud

- **WHEN** the compose environment runs
- **THEN** no Azure endpoint is contacted — the queue is Azurite, the database a container,
  secrets configuration values; the only remote reads are public image pulls

#### Scenario: the artifact cannot drift silently

- **WHEN** the AppHost's composition changes without regenerating the compose
- **THEN** CI fails naming the drift

#### Scenario: no Dockerfile owns a self-host image

- **WHEN** the repository is searched for Dockerfiles
- **THEN** none backs any image the generated compose runs — every self-host image is declared
  through the toolchain's publish path, and the facts the old Dockerfiles carried (base image,
  non-root, the SPA in `wwwroot`, socket access for pods) are expressed in project files, the
  AppHost, or code. An image that bakes OS packages the SDK cannot express (the conversation
  session's agent CLIs, Azure path) keeps its Dockerfile, with the reason written in it

### Requirement: the compose output is declared beside the resource it describes

Each fact in the generated compose SHALL be declared on the resource it describes — an image
reference on the project that publishes it, a healthcheck on the container it probes, a wait
condition on the dependent that waits — and there SHALL be no global compose-file patch block. A
compose fact that cannot be expressed per resource SHALL move to the operator's override layer
with its reason documented, never back to a global block.

The operator's contract SHALL NOT change beyond the published-images shift: the same `.env`
variables plus an overridable image tag, the same `docker compose up`, a server that answers on
`SERVER_PORT`.

#### Scenario: the generated compose boots

- **WHEN** the regenerated compose is brought up with fresh volumes and published images
- **THEN** postgres reaches healthy, migrations complete, and the server starts and answers —
  exercised for real, never inferred from the YAML

#### Scenario: no dead service names

- **WHEN** a service is removed from the composition
- **THEN** no patch site keeps naming it, because every declaration lives on a resource that
  either exists or is gone
