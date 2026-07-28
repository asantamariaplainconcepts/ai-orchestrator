# agent-execution

## ADDED Requirements

### Requirement: a SyncChange Run closes the Story's change as the repository says to

A Run whose Automation's action is SyncChange SHALL close the Story's open change by following a
close-out procedure read from the connected repository, at a configurable path defaulting to the
framework's convention. The action SHALL NOT contain any procedure of its own. Before any
workspace is prepared, the Run SHALL refuse when the Story has no open change, and when the
procedure document cannot be read, naming the path it looked for. A failing Run SHALL leave the
pull request exactly as it found it, and SHALL record why it stopped.

#### Scenario: the change is closed as the repository describes

- **WHEN** a Story has an open change and its repository carries the procedure document
- **THEN** the agent follows that document, the Run succeeds, and it records what it closed

#### Scenario: nothing to close

- **WHEN** the Story has no open change
- **THEN** the Run fails with that reason, before any workspace is prepared

#### Scenario: no procedure to follow

- **WHEN** the procedure document is absent at the configured or default path
- **THEN** the Run fails naming the path it looked for, before any workspace is prepared

#### Scenario: the project's own procedure is used

- **WHEN** the Automation names a document path
- **THEN** exactly that document is read, and no other

#### Scenario: a failed close changes nothing

- **WHEN** a SyncChange Run fails
- **THEN** the pull request is as it was, and the Run states why

## MODIFIED Requirements

### Requirement: a project can be given the framework's default Automations in one action

An Admin SHALL be able to seed a project with the framework's default Automations in one action:
the conversational entry points and the pipeline they form, each with the trigger label the
framework documents. Applying defaults SHALL be a partial success — Automations that already
exist SHALL be reported as skipped rather than duplicated or refused — and SHALL report which
trigger labels it ensured in the connected backlog. The defaults SHALL include the close-out
step, gated on approval, because merging is the least reversible thing the pipeline does
(DEC-040).

#### Scenario: a new project gets a working pipeline

- **WHEN** an Admin applies the defaults to a project with no Automations
- **THEN** the framework's Automations exist, chained as the framework documents, and the
  close-out one requires approval

#### Scenario: applying twice changes nothing

- **WHEN** the defaults are applied to a project that already has some of them
- **THEN** the existing ones are reported as skipped and nothing is duplicated
