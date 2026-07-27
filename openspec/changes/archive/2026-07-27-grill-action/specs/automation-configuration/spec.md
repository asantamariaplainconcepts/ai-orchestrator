# automation-configuration

## ADDED Requirements

### Requirement: grill Automations carry their rubric path and ready label

An Automation whose action is the grill SHALL carry an optional rubric path and an optional
ready label, defaulting in code to the framework's conventions
(`docs/process/definition-of-ready.md`, `ready-for-proposal`). The portal SHALL offer these
fields only for the grill action.

#### Scenario: defaults apply when unset

- **WHEN** a grill Automation is created without either setting
- **THEN** execution uses the framework defaults

#### Scenario: the settings are the Admin's

- **WHEN** a rubric path or ready label is set
- **THEN** execution uses exactly those values
