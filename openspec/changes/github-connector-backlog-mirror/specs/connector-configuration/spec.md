# connector-configuration

## ADDED Requirements

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
