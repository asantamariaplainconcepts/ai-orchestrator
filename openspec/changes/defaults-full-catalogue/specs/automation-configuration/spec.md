# automation-configuration

## MODIFIED Requirements

### Requirement: a project can be given the framework's default Automations in one action

An Admin SHALL be able to apply a default set of Automations to a project in a single action. The
set SHALL be defined in code and SHALL cover **every** action in the catalogue, each with a
trigger label, a runtime, and an approval setting. Where two defaults compose — one produces a
label another triggers on — the set SHALL wire them, so the workflow they describe works without
further configuration. Applying it SHALL be safe to repeat: the action SHALL create only what is
absent and SHALL report what already existed, separately from what it created, which SHALL also
make a later, larger set applicable to a project seeded from an earlier one. A trigger that
overlaps an existing Automation SHALL be skipped rather than failing the whole operation, and the
remaining defaults SHALL still be created.

#### Scenario: an unconfigured project

- **WHEN** an Admin applies the defaults to a project with no Automations
- **THEN** one Automation exists per catalogue action, and the response reports them as created

#### Scenario: applied a second time

- **WHEN** an Admin applies the defaults again
- **THEN** nothing is duplicated, the response reports every default as already present, and the
  operation succeeds

#### Scenario: a project seeded before the set grew

- **WHEN** the defaults are applied to a project holding an earlier, smaller set
- **THEN** only the additions are created and the rest are reported as already handled

#### Scenario: the seeded workflow chains

- **WHEN** a seeded Automation applies a label another seeded Automation triggers on
- **THEN** that Automation triggers through ordinary matching, with no configuration by the Admin

#### Scenario: one trigger is already taken

- **WHEN** a default's trigger label is already used by an Automation with a different action
- **THEN** that one is reported as skipped, the others are created, and the existing Automation
  is left exactly as it was
