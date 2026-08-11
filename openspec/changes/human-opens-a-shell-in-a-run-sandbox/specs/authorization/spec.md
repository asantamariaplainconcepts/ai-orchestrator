## ADDED Requirements

### Requirement: attaching to a Run's sandbox is its own permission, held by both bundles

Opening a shell inside a Run's sandbox SHALL require a permission distinct from reading a Run —
`run.attach` — scoped to the project as every other permission is. Both the Admin and the Member
bundles SHALL hold it.

It is distinct from `run.read` because reading a Run observes what happened while attaching to one
executes arbitrary commands on the machine the Run is using. Granting it to Members is a decision
recorded with its cost: a Member's shell runs inside a sandbox carrying the machine owner's own
session, so a Member may act with the owner's credentials. The capability is granted anyway, and
every use of it is recorded.

Because the surface that offers a terminal dispatches nothing through the request pipeline, the
permission SHALL be enforced by that surface itself, against the same project role the pipeline
would have asked for.

#### Scenario: a Member attaches

- **WHEN** a caller whose role bundle holds `run.attach` requests a terminal on a Run in their project
- **THEN** the request is permitted

#### Scenario: a caller with only read access attaches

- **WHEN** a caller holds `run.read` but not `run.attach`
- **THEN** the request is refused, and the Run's logs and transcript remain readable

#### Scenario: the surface enforces it itself

- **WHEN** a terminal surface that dispatches no request receives a connection
- **THEN** it asks the same authorization questions the pipeline asks, in the same order
- **AND** a caller with no role on the project is refused
