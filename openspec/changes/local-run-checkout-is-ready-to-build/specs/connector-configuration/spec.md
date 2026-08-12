## ADDED Requirements

### Requirement: a Connector carries the setup command its local checkout needs

A Connector SHALL carry an optional **setup command** — a single command line — applicable only
where its code source is `LocalFolder`. It SHALL be supplied through the same Admin-gated Connector
endpoint that carries the folder itself
(`src/modules/Backlog/AiOrchestrator.Modules.Backlog/Features/Backlog/UseCases/ConfigureConnector.cs`,
already declaring `BacklogPermissions.Configure`), stored exactly as written, and never read from,
merged with, or defaulted from any file in the code source.

An absent or blank command SHALL be stored as null rather than as an empty string, so one stored
value means one thing. Every Connector configured before this change SHALL read as having no setup
command, with no migration side effects and no change to how its Runs behave.

The command SHALL NOT be validated against the host at configuration time. Whether a tool resolves
is knowable only on the machine at the moment it runs — the same reason path validation answers
about a path rather than about a build.

#### Scenario: a local folder Connector stores its setup command

- **WHEN** an Admin saves a Connector with the local-folder code source and a setup command
- **THEN** the command is persisted with the Connector and returned on the read, and the backlog
  coordinates and credential semantics are untouched

#### Scenario: a blank command is stored as absent

- **WHEN** a Connector is saved with a blank or whitespace-only setup command
- **THEN** the stored value is null, not an empty string, and the Connector reads as having none

#### Scenario: existing Connectors read as having none

- **WHEN** the migration runs on a database with existing Connectors
- **THEN** every row reads back with no setup command, and dispatch, polling and labelling behave
  exactly as before the change

#### Scenario: the command is stored as written, not sourced from the repository

- **WHEN** a Connector's setup command is read for a Run
- **THEN** the value is the one the Admin saved, and no file in the code source was read to produce
  or amend it

### Requirement: the setup command is offered only where it applies, and cleared when it stops applying

The Connector form SHALL render the setup-command input only while the local-folder code source is
selected, beside the folder path and inside the same **Advanced** disclosure, with its explanation
beside the input rather than pooled at the end of the form.

Where the code source is `Repository` the input SHALL NOT be rendered and the request SHALL send the
field as null — hiding and clearing are the same act, exactly as for the code repository the local
source makes inapplicable. A hidden input whose stale value still travelled would leave a command
configured that nobody can see, and a later switch back to the local folder would execute it.

Unlike the folder path, the input SHALL remain optional: an empty value is a valid configuration, so
nothing about the disclosure's cannot-be-collapsed rule changes.

#### Scenario: the repository source does not offer it

- **WHEN** the code source is `Repository`
- **THEN** the setup-command input is absent from the form and the saved request sends the field as
  null

#### Scenario: switching away clears the stored command

- **WHEN** a Connector holding a setup command is saved with the code source switched back to
  `Repository`
- **THEN** the stored command is cleared, so switching back to the local folder later executes
  nothing until an Admin configures it again

#### Scenario: a stored command opens the disclosure

- **WHEN** the form opens for a Connector that stores a setup command
- **THEN** the Advanced disclosure is already open and the input carries the stored value

#### Scenario: the explanation sits with the input

- **WHEN** the setup-command input is rendered
- **THEN** its explanation is beside it, and all of its copy resolves through the typed i18n catalog
