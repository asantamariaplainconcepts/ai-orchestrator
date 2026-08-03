# automation-configuration

## ADDED Requirements

### Requirement: the setup card says what it will create before it is pressed

Where a pipeline has been discovered, the portal SHALL show what pressing the build control would
create, **before** it is pressed. The plan SHALL list one row per step, naming the trigger, the
prompt file that step would wire, whether that file already exists in the repository or a starter
would be installed for it, and whether the step waits for a person.

The plan SHALL be computed from the discovery the card has already performed. It SHALL NOT require a
second endpoint, and SHALL NOT cost an additional vendor read per row.

A step that would be wired but for which no starter can be installed SHALL be distinguishable from
one that would have a starter written, because those differ in whether anything is written to the
repository.

**No separate consent SHALL be required for installing the starters the plan names.** The rows state
which files would be written; a control asking whether to write them restates the preview, and a
confirmation of a confirmation trains a reader past both.

The statement that starters arrive as a draft pull request SHALL sit with the control that creates
them, because that is where the decision is taken.

A plan longer than a few rows SHALL collapse, and SHALL be expandable — a plan that fills the screen
stops being read, which defeats showing it.

#### Scenario: the plan precedes the press

- **WHEN** a pipeline has been discovered
- **THEN** one row per step is shown, naming the trigger, the file it wires, whether that file exists
  and whether the step waits for a person

#### Scenario: reading the plan changes nothing

- **WHEN** the plan is computed
- **THEN** no Automation is created and nothing is written to the repository

#### Scenario: the preview replaces the consent

- **WHEN** the plan is visible
- **THEN** no separate control asks whether to install the starters it names
