## MODIFIED Requirements

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

**A sandbox that belongs to no project is authorized at the habitat's scope, and only in self-host.**
The machine's sandboxes are a property of the machine rather than of any project: a sandbox left behind
by a killed process resolves to no Run and therefore to no project, so there is no project role to ask
for. For that surface, holding `run.attach` on **at least one project** SHALL be sufficient; where a
sandbox does resolve to a Run, that Run's project role SHALL be asked for as well.

The widening SHALL be confined to the habitat that licenses the surface at all. A self-host habitat is
one owner and one machine, which is the same assumption that already permits the startup sweep to delete
any sandbox in the claimed namespace without asking whose Run it was; a caller holding `run.attach`
anywhere on such a machine is that owner. A deployment SHALL NOT reach this reading: the habitat's answer
that no terminal is hosted there SHALL be given before any permission is evaluated, so the habitat-scoped
check exists only where no deployment can arrive at it.

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

#### Scenario: a caller reaches a sandbox that belongs to no project

- **WHEN** a caller holding `run.attach` on some project opens a terminal on a self-host sandbox that
  resolves to no Run
- **THEN** the request is permitted, authorized at the habitat's scope

#### Scenario: a caller holds the permission nowhere

- **WHEN** a caller who holds `run.attach` on no project requests the machine's sandboxes or a terminal
- **THEN** the request is refused, naming permission as the cause

#### Scenario: the habitat-scoped reading does not reach a deployment

- **WHEN** anyone requests the machine's sandboxes in a deployed habitat
- **THEN** the answer is that no terminal is hosted there, given without evaluating any permission
- **AND** no habitat-scoped grant is available in a deployment by any path
