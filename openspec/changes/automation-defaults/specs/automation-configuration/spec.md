# automation-configuration

## ADDED Requirements

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

#### Scenario: the project is at its Automation cap

- **WHEN** applying the defaults would exceed the project's cap
- **THEN** the cap is enforced and the response names the defaults that were not created

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
