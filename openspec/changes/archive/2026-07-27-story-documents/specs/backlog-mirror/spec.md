# backlog-mirror

## ADDED Requirements

### Requirement: a Story's attached documents are readable in the portal

The Story detail view SHALL list the markdown documents the Story's linked change adds or
modifies, and render the selected one through the same sanitising pipeline as the description.
Documents SHALL be read live at the change's head ref and SHALL NOT be mirrored (BR-008): a
branch that has moved on shows its current content. The view SHALL distinguish three absences —
no linked change, a change with no documents, and a document that could not be read.

#### Scenario: the specification is readable in the portal

- **WHEN** a Story's linked change adds markdown documents and the detail view is opened
- **THEN** the documents are listed by path and the selected one renders

#### Scenario: the branch moved on

- **WHEN** a document is read after its branch advanced
- **THEN** the content is the branch's current head, not an earlier copy

#### Scenario: three absences, three messages

- **WHEN** there is no linked change, or the change adds no documents, or a read fails
- **THEN** the view says which of the three it is

#### Scenario: document content is untrusted too

- **WHEN** a document contains a script or raw HTML
- **THEN** nothing executes — the same pipeline the description uses
