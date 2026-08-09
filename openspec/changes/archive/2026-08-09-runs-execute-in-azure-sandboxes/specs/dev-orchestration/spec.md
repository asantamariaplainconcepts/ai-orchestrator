## MODIFIED Requirements

### Requirement: run mode takes a habitat parameter, defaulting to the dev loop

The AppHost SHALL read a `habitat` parameter (`Parameters:habitat`, overridable through user
secrets and appsettings) in run mode, defaulting to `local`. `local` SHALL apply the dev loop's
declarations exactly as before; `server` SHALL apply the same declaration set the generated
compose carries, so a developer can rehearse the operator's shape under `aspire run`. An unknown
value SHALL refuse at startup naming the valid ones.

The parameter SHALL NOT change publish output: publishing always emits the server declarations,
and the artifact carries no habitat parameter.

**What the server shape declares changed with the substrate.** It named a pod image and granted
the docker socket; it now names the **sbx sandbox launcher**, and the socket grant is gone from
the product entirely. The Server image carries no agent CLI on purpose, so retiring the pod
without naming a launcher would have left the self-host habitat with no way to execute an agent
at all.

#### Scenario: nothing configured is the dev loop

- **WHEN** `aspire run` starts with no habitat configured
- **THEN** every declaration matches the dev loop, unchanged

#### Scenario: the server shape is rehearsable locally

- **WHEN** `Parameters:habitat` is `server` under `aspire run`
- **THEN** the Server receives the same declarations the generated compose carries — the sandbox
  launcher, the Local-locus reason, no seeder — and no docker socket

#### Scenario: an unknown habitat refuses by name

- **WHEN** `Parameters:habitat` is neither `local` nor `server`
- **THEN** startup refuses naming both valid values

### Requirement: the whole system runs from a clone on a Docker-only machine

The repository SHALL carry a generated docker-compose description of the full system — portal,
migrations, database — runnable on a machine with only Docker and git, contacting no cloud
service beyond public image registries. The images SHALL be published to a public registry by CI
on merge to the default branch, tagged by commit SHA, and produced by the toolchain's own
container publish — no Dockerfile backs a self-host image. The compose SHALL be generated from
the same composition development runs, and CI SHALL fail when the committed artifact drifts from
it. The quickstart SHALL require no AI credential and SHALL build no image locally: the default
runtime is the free model, and the only secret anywhere is the operator's own vendor PAT,
provided as configuration.

**Two services left the description with their substrates.** The dispatch worker retired with
the queue — dispatch is the Postgres outbox now, consumed inside the portal — and the queue
emulator went with it. What used to be four containers and an emulator is a portal, a migrator
and a database.

#### Scenario: clone and run

- **WHEN** the quickstart is followed on a Docker-only machine
- **THEN** all images are pulled from the public registry by tag — nothing builds locally — and
  the portal serves

#### Scenario: zero cloud

- **WHEN** the compose environment runs
- **THEN** no Azure endpoint is contacted — the database is a container, dispatch is a table in
  it, secrets are configuration values; the only remote reads are public image pulls

#### Scenario: the artifact cannot drift silently

- **WHEN** the AppHost's composition changes without regenerating the compose
- **THEN** CI fails naming the drift

#### Scenario: no Dockerfile owns a self-host image

- **WHEN** the repository is searched for Dockerfiles
- **THEN** none backs any image the generated compose runs — every self-host image is declared
  through the toolchain's publish path, and the facts the old Dockerfiles carried (base image,
  non-root, the SPA in `wwwroot`) are expressed in project files, the AppHost, or code. Socket
  access is no longer among them: it existed so a container could start sibling containers, which
  is the property this change removed. An image that bakes OS packages the SDK cannot express
  (the conversation session's agent CLIs) keeps its Dockerfile, with the reason written in it
