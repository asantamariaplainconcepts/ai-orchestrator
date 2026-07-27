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

### Requirement: an Admin edits, disables and re-enables an Automation

An Admin SHALL be able to change an Automation's trigger, action, runtime, approval flag and
timeout, and to disable and re-enable it. An edit SHALL face the same BR-003 overlap validation
as a create, excluding the Automation being edited from the comparison. Enabling SHALL re-run
that validation; disabling SHALL NOT, because a disabled Automation cannot overlap anything.
Editing or disabling SHALL NOT affect Runs already active — they complete against the
Automation they were created with.

#### Scenario: an edit that would overlap is refused

- **WHEN** an edit would make the trigger intersect another enabled Automation's
- **THEN** it is refused with the create-time error and nothing changes

#### Scenario: an unchanged trigger does not overlap itself

- **WHEN** an edit leaves the trigger as it was
- **THEN** it succeeds — the Automation is not compared against itself

#### Scenario: re-enabling re-checks

- **WHEN** a disabled Automation whose trigger now collides with a newer one is enabled
- **THEN** it is refused and stays disabled

#### Scenario: disabling stops future matches only

- **WHEN** an Automation with an active Run is disabled
- **THEN** the Run is unaffected and no new match is made for that trigger

### Requirement: a project can be given the framework's default Automations in one action

An Admin SHALL be able to apply a default set of Automations to a project in a single action. The
set SHALL be defined in code and SHALL cover every action in the catalogue, each with a trigger
label, a runtime, and an approval setting. Applying it SHALL be safe to repeat: the action SHALL
create only what is absent and SHALL report what already existed, separately from what it
created. A trigger that overlaps an existing Automation SHALL be skipped rather than failing the
whole operation, and the remaining defaults SHALL still be created.

#### Scenario: an unconfigured project

- **WHEN** an Admin applies the defaults to a project with no Automations
- **THEN** one Automation exists per catalogue action, and the response reports them as created

#### Scenario: applied a second time

- **WHEN** an Admin applies the defaults again
- **THEN** nothing is duplicated, the response reports every default as already present, and the
  operation succeeds

#### Scenario: one trigger is already taken

- **WHEN** a default's trigger label is already used by an Automation with a different action
- **THEN** that one is reported as skipped, the others are created, and the existing Automation
  is left exactly as it was

### Requirement: the default trigger labels are ensured in the connected backlog

Applying the defaults SHALL ensure each trigger label exists in the project's connected
repository, so a Member can choose it in the vendor's own interface without any Story having been
labelled first. Ensuring labels SHALL NOT be a precondition for creating the Automations: a
vendor failure SHALL be reported naming the labels affected, while the Automations remain
created. A project with no Connector SHALL still receive its Automations, and the response SHALL
say that no labels could be ensured.

#### Scenario: labels become selectable at the vendor

- **WHEN** the defaults are applied to a project connected to a repository with none of them
- **THEN** each trigger label exists in that repository, without any Story having been modified

#### Scenario: the vendor refuses

- **WHEN** the vendor rejects a label
- **THEN** the Automations are still created, the failure names the label, and the Story mirror
  is unchanged

#### Scenario: no Connector

- **WHEN** the defaults are applied to a project with no Connector
- **THEN** the Automations are created and the response states that labels could not be ensured

