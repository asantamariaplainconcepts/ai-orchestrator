# agent-execution

## ADDED Requirements

### Requirement: the propose action turns a ready Story into a documentation PR

A `ProposeSpec` Run SHALL produce a pull request containing only documentation — the proposal
for the Story — through the same publishing pipeline as implementation, and record it as the
Run's output. It SHALL follow the repository's own declared conventions for such documents,
defaulting to a proposals directory. A Story with no body SHALL fail before any workspace is
prepared, stating there is nothing to propose from. A Story whose linked change already exists
SHALL fail naming that change rather than opening a second.

#### Scenario: a ready story becomes a proposal PR

- **WHEN** a propose Run executes against a Story with a body
- **THEN** a pull request with the proposal exists, linked as the Run's output

#### Scenario: nothing to propose from

- **WHEN** the Story has no body
- **THEN** the Run fails saying so, and no workspace was prepared

#### Scenario: one open change per Story

- **WHEN** the Story already has a linked change
- **THEN** the Run fails naming it, and no second pull request exists
