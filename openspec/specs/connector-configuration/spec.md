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

The stored Connector SHALL contain the **name** of the secret holding its access token, and the
time that secret was last set. The token value SHALL NOT be written to the Connector's row, to
logs, to telemetry, or to any API response (BR-010, as revised by DEC-052).

Where the habitat has no managed vault, stored values SHALL be protected at rest with the
framework's own data protection, held outside the application database, with the key material
held apart from the values, so possession of any one of the three does not yield a usable
credential. No API, page or log SHALL expose a stored value by any route — the store SHALL offer
no operation that reads one back.

#### Scenario: inspecting storage

- **WHEN** the Connector row is read directly from the database
- **THEN** it contains a secret name and no token value

#### Scenario: reading a Connector back through the API

- **WHEN** a client fetches a Project's Connector
- **THEN** the response carries the coordinates, the secret name and when it was last set, never
  the token

#### Scenario: a stolen store yields no credential

- **WHEN** a habitat stores values locally and those values are read without its key material
- **THEN** what is stored is not the token and no token can be recovered from it

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

### Requirement: a project can be retired without losing what its agents did

A Project SHALL be archivable and restorable, recording when it was archived. An archived Project
SHALL begin no new work: its Connector SHALL NOT be polled, a trigger label on its Stories SHALL
NOT create a Run, and a manual Run SHALL be refused with the reason. Work already under way SHALL
be unaffected — a Run executing when the Project is archived completes and records its outcome.
Everything already recorded SHALL remain readable at the addresses it always had. The projects
list SHALL exclude archived Projects by default while stating how many exist and offering a way
to see them. Restoring SHALL resume polling and matching with no configuration lost.

#### Scenario: archiving stops the polling

- **WHEN** an archived Project's Connector would next be polled
- **THEN** it is not polled, and nothing at the vendor changes

#### Scenario: archiving stops the matching

- **WHEN** a trigger label is applied to a Story of an archived Project
- **THEN** no Run is created

#### Scenario: archiving refuses a manual Run

- **WHEN** a Run is requested by hand on an archived Project
- **THEN** it is refused with the reason

#### Scenario: work under way is left alone

- **WHEN** a Project is archived while one of its Runs is executing
- **THEN** that Run completes and records its outcome exactly as it otherwise would

#### Scenario: the history stays readable

- **WHEN** an archived Project's Runs, their logs, or its pulse are requested
- **THEN** they are returned as they were before archiving

#### Scenario: the list says how many are hidden

- **WHEN** the projects list is read
- **THEN** archived Projects are excluded, their number is stated, and they can be shown

#### Scenario: restoring resumes the work

- **WHEN** an archived Project is restored
- **THEN** polling and matching resume, with its Connector and Automations unchanged

#### Scenario: archiving is confirmed deliberately

- **WHEN** an archive is requested without the Project's name as confirmation
- **THEN** it is refused and nothing changes

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

