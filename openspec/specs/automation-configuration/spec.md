# automation-configuration Specification

## Purpose
TBD - created by archiving change automation-configuration. Update Purpose after archive.
## Requirements
### Requirement: an Admin configures what a trigger label makes an Agent do

An Admin SHALL create an Automation on a Project consisting of a trigger label, an optional Story
state, an action from the locked catalogue (DEC-026), a runtime, a `requiresApproval` flag, and a
phase timeout defaulting to 30 minutes (BR-005). The trigger label SHALL be required and
non-empty; the state SHALL be optional and compared as the vendor's own opaque string.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, action, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** an action from the catalogue has no executing Agent yet
- **THEN** it remains selectable and the interface says it cannot run yet — a configurable
  action that silently never executes is a trap

### Requirement: overlapping triggers are rejected when saved

Saving an Automation whose trigger could match a Story that an existing **enabled** Automation in
the same Project could also match SHALL fail with a domain error naming the conflicting
Automation (BR-003, DEC-033). Two triggers overlap when they share a label and either share a
state or one places no state constraint; different labels never overlap; disabled Automations are
ignored.

#### Scenario: the same label and state twice

- **WHEN** an Admin saves a second Automation with a trigger already used by an enabled one
- **THEN** the save fails and the response names the Automation it collides with

#### Scenario: the same label with different states

- **WHEN** two Automations use one label but different Story states
- **THEN** both save — no Story carries two states at once, so neither can match both

#### Scenario: a broad trigger subsumes a narrow one

- **WHEN** an Automation triggers on a label with no state constraint, and another uses the same
  label with a state
- **THEN** the save fails: a Story in that state would match both, which is the ambiguity BR-003
  exists to prevent

### Requirement: an Automation never carries a credential

An Automation SHALL reference a runtime by name only. No token, key or connection string SHALL be
stored on it or returned by any endpoint that exposes it (BR-010).

#### Scenario: reading a project's Automations

- **WHEN** the Automations of a Project are fetched
- **THEN** the response contains configuration only — no credential in any field

