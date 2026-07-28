# connector-configuration Specification

## Purpose
TBD - created by archiving change github-connector-backlog-mirror. Update Purpose after archive.
## Requirements
### Requirement: a project has at most one Connector

A Project SHALL have zero or one Connector, identifying the vendor and the repository coordinates
its Stories are read from. Configuring a Connector on a Project that already has one SHALL replace
it, not add a second.

#### Scenario: first configuration

- **WHEN** an Admin configures a GitHub Connector on a Project that has none
- **THEN** the Project has exactly one Connector, and its Stories can be polled

#### Scenario: reconfiguration

- **WHEN** an Admin configures a Connector on a Project that already has one
- **THEN** the Project still has exactly one Connector, carrying the new coordinates

### Requirement: the credential is verified before the Connector is stored

Saving a Connector SHALL perform a live call to the vendor using the supplied credential, and
SHALL store the Connector only if that call succeeds. A failure SHALL be reported as RFC 7807
ProblemDetails distinguishing **unreachable or unknown repository** from **credential rejected**,
because the two have different fixes.

#### Scenario: credential cannot read the repository

- **WHEN** an Admin saves a Connector whose token cannot read the named repository
- **THEN** the save fails, the problem names the credential as the cause, and no Connector is
  stored

#### Scenario: repository does not exist

- **WHEN** an Admin saves a Connector naming a repository that does not exist
- **THEN** the save fails, the problem names the coordinates as the cause, and no Connector is
  stored

#### Scenario: a stored Connector is a working Connector

- **WHEN** any Connector exists in the system
- **THEN** its credential was verified against the vendor at the moment it was stored

### Requirement: only a secret name is persisted

The stored Connector SHALL contain the **name** of the secret holding its access token. The token
value SHALL NOT be written to the database, to logs, or to any API response (BR-010).

#### Scenario: inspecting storage

- **WHEN** the Connector row is read directly from the database
- **THEN** it contains a secret name and no token value

#### Scenario: reading a Connector back through the API

- **WHEN** a client fetches a Project's Connector
- **THEN** the response carries the coordinates and the secret name, never the token

### Requirement: secrets resolve through one seam

Token values SHALL be obtained through a single resolver abstraction. Application code SHALL NOT
read secrets from configuration directly, so the storage mechanism can change without touching
call sites.

#### Scenario: swapping the store

- **WHEN** the resolver implementation changes from the development store to a managed secret store
- **THEN** no calling code changes

#### Scenario: the named secret is missing

- **WHEN** a Connector names a secret the resolver cannot find
- **THEN** the operation fails with a message naming the missing secret, and never falls back to
  an empty or default credential

### Requirement: secrets resolve per read, not at startup

Secret values SHALL be fetched when they are needed. The system SHALL NOT depend on the set of
secrets being known at process start, because Connectors — and therefore secret names — are
created while the application is running.

#### Scenario: a secret created after startup

- **WHEN** an Admin configures a Connector naming a secret that was created after the application
  started
- **THEN** it resolves without restarting the application

#### Scenario: a rotated secret

- **WHEN** a secret's value is rotated in the store
- **THEN** the next resolution uses the new value, with no restart and no cache to invalidate

### Requirement: the host owns secret-store wiring, not the modules

Registration of any secret-store client SHALL happen in the host composition root. A module SHALL
depend only on the resolver abstraction, so modules remain host-agnostic and can be composed by
any host.

#### Scenario: a module stays host-agnostic

- **WHEN** a module needs a credential
- **THEN** it depends on the resolver abstraction only, and references no cloud SDK or hosting
  integration

### Requirement: every Connector's health is visible from the projects list

The product SHALL expose each configured Connector's health — project, vendor, last successful
sync, last failure — in one read, and the projects list SHALL show each project in one of four
states: healthy, failing, never synced, or not configured. The failure sentence SHALL be
reachable without leaving the list, and a healthy Connector SHALL show how old its last sync is.
No new probing SHALL exist: the view renders what the poller already records (BR-008).

#### Scenario: four states, four projects

- **WHEN** projects exist with a healthy, a failing, a never-synced and no Connector
- **THEN** the list shows each distinctly

#### Scenario: the failure explains itself in place

- **WHEN** a Connector is failing
- **THEN** its stored failure sentence is readable from the list

#### Scenario: recovery needs no action

- **WHEN** a failing Connector's next poll succeeds
- **THEN** the list reflects healthy on its ordinary refresh

