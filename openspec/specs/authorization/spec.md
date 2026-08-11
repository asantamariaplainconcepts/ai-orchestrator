# authorization Specification

## Purpose
TBD - created by archiving change project-roles. Update Purpose after archive.
## Requirements
### Requirement: every operation names the permission it requires, and the pipeline enforces it

Each command and query SHALL declare the permission it requires, and enforcement SHALL happen in the
shared handler pipeline rather than inside handlers, so that omitting a check is not a way to pass.
An operation that declares nothing SHALL require the Admin bundle: a use case added without thought
SHALL be refused rather than open.

Permission SHALL be a function of the caller **and** the project. There SHALL be no way to ask what a
caller may do without naming a project, because BR-009's bundles are project-scoped and a single
global answer could only be invented.

A declaration SHALL name a **permission**, and a role SHALL be a **bundle** of permissions — the two
SHALL NOT be the same thing. An operation SHALL NOT name a role: adding a bundle must be a change to
the mapping, not a sweep over every declaration in the product.

The mapping SHALL be contributed by each module beside the operations that declare the permissions, so
that adding one touches no central list. Admin SHALL hold every permission by rule rather than by
enumeration, so a permission added later cannot be omitted from the bundle defined as holding all of
them.

The bundles SHALL be the two DEC-034 locked and no others: Admin holds everything; Member observes and
triggers — labels, Run now, approve, cancel — and configures nothing.

A refusal SHALL name that permission was the reason, and SHALL NOT reveal whether the project exists to
a caller with no role on it.

A surface that dispatches no command or query — a real-time subscription, for instance — SHALL check
the same thing for itself. The pipeline cannot see such a surface, so being authenticated SHALL NOT be
mistaken there for being permitted.

#### Scenario: a Member cannot configure

- **WHEN** a caller holding Member on a project configures its Connector or its Automations
- **THEN** the operation is refused for permission, and nothing is written

#### Scenario: a Member can operate

- **WHEN** a caller holding Member applies a trigger label, dispatches a Run, approves or cancels one
- **THEN** it proceeds, because observing and triggering is what the bundle grants

#### Scenario: an undeclared operation is closed, not open

- **WHEN** an operation declares no permission
- **THEN** it is refused, whatever bundle the caller holds

#### Scenario: a bundle is a set of permissions, not a name in a handler

- **WHEN** a bundle is given a permission it did not hold
- **THEN** every operation requiring that permission becomes available to it, with no operation
  changed

#### Scenario: a role on one project is not a role on another

- **WHEN** a caller holds Admin on one project and nothing on a second
- **THEN** their Admin operations succeed on the first and are refused on the second

#### Scenario: no role reveals nothing

- **WHEN** a caller with no role on a project addresses it
- **THEN** the refusal does not disclose whether that project exists

### Requirement: an Admin assigns project roles, and the first one comes from configuration

An Admin of a project SHALL be able to grant either bundle to a person who has signed in at least
once, and to change or remove it, from that project's configuration surface. A role SHALL be keyed by
the provider's stable identity id rather than by an address, so that a role cannot follow a reassigned
mailbox.

Bootstrap administrators SHALL come from configuration and SHALL hold Admin on every project. They
SHALL NOT be claimed by whoever signs in first: granting administration by race is not an
authorization model.

When no bootstrap administrator is configured and no role exists, **nobody** SHALL hold Admin, and the
deployment SHALL say so plainly rather than locking silently or granting silently.

#### Scenario: granting a role

- **WHEN** an Admin grants Member to somebody who has signed in
- **THEN** that person holds Member on that project and nothing on any other

#### Scenario: the bootstrap administrator needs no grant

- **WHEN** a configured bootstrap administrator signs in to a project with no roles at all
- **THEN** they hold Admin there without anybody having granted it

#### Scenario: nobody administers, and it is said out loud

- **WHEN** a deployment has no bootstrap administrator configured and no roles stored
- **THEN** no caller holds Admin, and the deployment announces that state

#### Scenario: a role cannot be granted to a stranger

- **WHEN** an Admin tries to grant a role to somebody who has never signed in
- **THEN** it is refused, because there is no identity yet to attach a role to

#### Scenario: removing a role removes the power

- **WHEN** an Admin removes somebody's role
- **THEN** that person's configuring operations on that project are refused from the next request

#### Scenario: the last administrator cannot be removed or demoted

- **WHEN** removing or demoting a role would leave a project with nobody holding Admin — no stored
  administrator and none configured
- **THEN** it is refused, because nothing inside the product could undo it

### Requirement: a caller sees only the projects they have a role on

An operation that reaches across projects SHALL narrow its answer to the projects the caller holds a
role on. A list that named every project would disclose exactly what the refusals are worded to
withhold, and a permission model whose reads ignore it is a decorative one.

Creating a project SHALL be available to any signed-in caller, and its creator SHALL hold Admin on
it. This is not administration claimed by race — it is authority over the one thing that caller
brought into existence, and without it only the configured bootstrap administrators could ever
begin.

Where a habitat has a single caller — a machine its owner owns, or a deployment with no identity
provider — that caller SHALL hold every permission, and no role SHALL need storing. Which habitat
this is SHALL be decided by configuration, never inferred from the caller: "nobody has signed in
yet" and "this person is the only occupant" are different states that must not be confused.

#### Scenario: cross-project reads are scoped

- **WHEN** a caller who holds a role on one project lists projects, connectors or the inbox
- **THEN** only the project they hold a role on appears

#### Scenario: creating a project makes you its Admin

- **WHEN** a signed-in caller with no roles anywhere creates a project
- **THEN** they hold Admin on it, and still nothing on any other

#### Scenario: the live log stream is scoped per Run

- **WHEN** a caller with no role on a project subscribes to one of its Runs' live log
- **THEN** the subscription is refused, and refused identically for a Run that does not exist

#### Scenario: a single-occupant habitat needs no roles

- **WHEN** the habitat is a machine one person owns, or has no identity provider configured
- **THEN** that caller may do everything, with no role stored anywhere

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

