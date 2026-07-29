# connector-configuration

## ADDED Requirements

### Requirement: an Admin configures a Connector by supplying the token itself

An Admin SHALL be able to configure a Connector by supplying the access token directly, without
having created a secret beforehand. The product SHALL derive the secret's name from the project,
SHALL store the value in the habitat's secret store, and SHALL NOT ask the Admin to choose a name.
Supplying a token and naming an existing secret SHALL both remain available; exactly one SHALL be
supplied, and neither or both SHALL be refused naming which.

Storing SHALL require a caller holding the Admin role. A habitat whose store cannot accept a
value SHALL refuse this path with a reason naming what to do instead, and the naming path SHALL
continue to work there.

The Connector SHALL be persisted only after the stored value has verified against the live
vendor, so a Connector that exists is still one that works (UC-004). Supplying a new token for a
Connector that already has one SHALL replace the stored value, and subsequent Runs SHALL use the
new one without a restart.

#### Scenario: connecting without a pre-existing secret

- **WHEN** an Admin supplies coordinates and a token for a project with no Connector
- **THEN** the Connector is configured, the token is in the habitat's secret store under a name
  the product chose, and no part of the token appears in the response

#### Scenario: rotation

- **WHEN** an Admin supplies a new token for a project that already has a Connector
- **THEN** the stored value is replaced under the same name, and the next Run uses the new value

#### Scenario: the operator brings their own secret

- **WHEN** an Admin names an existing secret instead of supplying a token
- **THEN** the Connector is configured exactly as it was before this capability existed

#### Scenario: neither or both

- **WHEN** a request carries no token and no secret name, or carries both
- **THEN** it is refused with a message naming what is missing or what conflicts

#### Scenario: a habitat that cannot store

- **WHEN** an Admin supplies a token in a habitat whose secret store cannot accept values
- **THEN** the request is refused with a reason naming what to do instead, and naming an existing
  secret still configures a Connector there

#### Scenario: a caller who is not an Admin

- **WHEN** a caller without the Admin role supplies a token
- **THEN** the request is refused and nothing is stored

#### Scenario: the token does not work

- **WHEN** the supplied token fails verification against the vendor
- **THEN** no Connector is configured, and the failure names the vendor's reason

## MODIFIED Requirements

### Requirement: only a secret name is persisted

The stored Connector SHALL contain the **name** of the secret holding its access token, and the
time that secret was last set. The token value SHALL NOT be written to the Connector's row, to
logs, to telemetry, or to any API response (BR-010, as revised by DEC-052).

Where the habitat's secret store keeps values in the product's own database rather than in a
managed vault, those values SHALL be encrypted at rest with a key held outside that database, so
possession of the database alone does not yield a usable credential. No API, page or log SHALL
expose a stored value by any route — the store SHALL offer no operation that reads one back.

#### Scenario: inspecting storage

- **WHEN** the Connector row is read directly from the database
- **THEN** it contains a secret name and no token value

#### Scenario: reading a Connector back through the API

- **WHEN** a client fetches a Project's Connector
- **THEN** the response carries the coordinates, the secret name and when it was last set, never
  the token

#### Scenario: a stolen database yields no credential

- **WHEN** a habitat stores values locally and its database is read without its encryption key
- **THEN** the stored values are ciphertext and no token can be recovered from it

### Requirement: secrets resolve through one seam

Token values SHALL be obtained through a single resolver abstraction. Application code SHALL NOT
read secrets from configuration directly, so the storage mechanism can change without touching
call sites.

Storing a value SHALL be a separate abstraction from resolving one, so that the ability to write
a credential is visible in the dependencies of the few places that hold it. The storing
abstraction SHALL expose no operation that returns a stored value.

#### Scenario: swapping the store

- **WHEN** the resolver implementation changes from the development store to a managed secret store
- **THEN** no calling code changes

#### Scenario: the named secret is missing

- **WHEN** a Connector names a secret the resolver cannot find
- **THEN** the operation fails with a message naming the missing secret, and never falls back to
  an empty or default credential

#### Scenario: reading and writing are different dependencies

- **WHEN** a component that only consumes credentials is inspected
- **THEN** it depends on the resolving abstraction alone and cannot store a value
