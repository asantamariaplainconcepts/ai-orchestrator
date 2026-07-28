# automation-configuration

## ADDED Requirements

### Requirement: an Automation can hand work on by writing a label when it succeeds

An Automation SHALL carry an optional output label, applied to the Story through the licensed
label write when a Run of that Automation succeeds, and applied at no other time. An unset output
label SHALL mean the Automation ends silently. Saving an Automation whose output label equals its
own trigger label SHALL be refused, naming the reason.

#### Scenario: the chain continues past the grill

- **WHEN** an Automation with an output label has a Run that succeeds
- **THEN** the label reaches the vendor, and after reconciliation an Automation triggered by that
  label has a Run of its own

#### Scenario: silence is the default

- **WHEN** an Automation without an output label has a Run that succeeds
- **THEN** no label is written

#### Scenario: only success hands work on

- **WHEN** a Run of an Automation with an output label fails or is cancelled
- **THEN** no label is written

#### Scenario: an Automation may not trigger itself

- **WHEN** an Automation is saved with an output label equal to its trigger label
- **THEN** the save is refused with the reason

## MODIFIED Requirements

### Requirement: grill Automations carry their rubric path and ready label

An Automation whose action is the grill SHALL carry an optional rubric path, defaulting in code
to the framework's convention (`docs/process/definition-of-ready.md`). The label the grill applies
when the bar is met SHALL be the Automation's output label, defaulting in code to
`ready-for-proposal` for the grill action only. The portal SHALL offer the rubric path only for
the grill action, and the output label for every action.

#### Scenario: defaults apply when unset

- **WHEN** a grill Automation is created without either setting
- **THEN** execution uses the framework defaults

#### Scenario: the settings are the Admin's

- **WHEN** a rubric path or output label is set
- **THEN** execution uses exactly those values

#### Scenario: a grill configured before the field widened

- **WHEN** a grill Automation configured with a ready label is read after the change
- **THEN** that value is its output label and its behaviour is unchanged
