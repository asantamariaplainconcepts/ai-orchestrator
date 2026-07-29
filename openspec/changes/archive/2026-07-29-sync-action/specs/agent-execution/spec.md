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
