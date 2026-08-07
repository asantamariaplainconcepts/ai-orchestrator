## ADDED Requirements

### Requirement: a Project resolves its Automations' runtime by default

A Project SHALL carry runtime settings an Admin edits from Settings: a **default runtime**, and an
optional **credential secret name per runtime**. Names SHALL be stored and values never (BR-010),
and the credential names SHALL be readable only where the rest of the project's configuration is
(BR-009) — never on a surface a Member reads.

An Automation's runtime SHALL be optional: unset means the Project default, resolved **at
execution time**, so changing the default changes future Runs without touching any Automation. An
Automation naming a runtime explicitly SHALL win over the default. Existing Automations keep the
explicit runtime they already carry — the migration SHALL change no behaviour.

The form SHALL offer "Project default" as the runtime's first option and SHALL say what it
currently resolves to, so choosing it is informed rather than blind. An update that sends no
runtime SHALL mean the default — the same absent-versus-set discipline the setup action's
selections already use.

#### Scenario: the default applies at execution time

- **WHEN** an Automation with no explicit runtime fires after the Admin changes the Project
  default
- **THEN** the new Run resolves to the new default, and no Automation row changed

#### Scenario: an explicit runtime wins

- **WHEN** an Automation naming a runtime fires on a Project whose default differs
- **THEN** the Run executes on the Automation's runtime

#### Scenario: existing Automations survive the migration unchanged

- **WHEN** the schema change lands on a project with Automations
- **THEN** every existing Automation keeps its explicit runtime and its Runs behave as before

#### Scenario: credential names are stored, values never

- **WHEN** an Admin sets a credential name for a runtime and the settings are read back
- **THEN** the name is present, no secret value appears anywhere, and a non-Admin read does not
  include the names
