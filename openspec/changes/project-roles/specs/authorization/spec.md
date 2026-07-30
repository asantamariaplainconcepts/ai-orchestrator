# authorization

## ADDED Requirements

### Requirement: every operation names the permission it requires, and the pipeline enforces it

Each command and query SHALL declare the permission it requires, and enforcement SHALL happen in the
shared handler pipeline rather than inside handlers, so that omitting a check is not a way to pass.
An operation that declares nothing SHALL require the Admin bundle: a use case added without thought
SHALL be refused rather than open.

Permission SHALL be a function of the caller **and** the project. There SHALL be no way to ask what a
caller may do without naming a project, because BR-009's bundles are project-scoped and a single
global answer could only be invented.

The bundles SHALL be the two DEC-034 locked and no others: Admin holds everything; Member observes and
triggers — labels, Run now, approve, cancel — and configures nothing.

A refusal SHALL name that permission was the reason, and SHALL NOT reveal whether the project exists to
a caller with no role on it.

#### Scenario: a Member cannot configure

- **WHEN** a caller holding Member on a project configures its Connector or its Automations
- **THEN** the operation is refused for permission, and nothing is written

#### Scenario: a Member can operate

- **WHEN** a caller holding Member applies a trigger label, dispatches a Run, approves or cancels one
- **THEN** it proceeds, because observing and triggering is what the bundle grants

#### Scenario: an undeclared operation is closed, not open

- **WHEN** an operation declares no permission
- **THEN** it requires Admin

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

#### Scenario: a single-occupant habitat needs no roles

- **WHEN** the habitat is a machine one person owns, or has no identity provider configured
- **THEN** that caller may do everything, with no role stored anywhere
