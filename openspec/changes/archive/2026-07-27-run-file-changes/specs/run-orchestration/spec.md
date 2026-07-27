# run-orchestration

## ADDED Requirements

### Requirement: a Run's file changes are reviewable beside its Plan

The Run detail view SHALL show the files the Run's change touched — path, status, added and
removed counts, and the unified patch rendered with added and removed lines visually
distinguished using design-system tokens. The read SHALL be live through the Connector at the
Run's linked change (BR-008). A Run with no pull request, a change touching no files, and a
failed read SHALL be three distinct messages. A file whose patch is omitted SHALL state the
reason and link to the vendor.

#### Scenario: the reviewer sees what the Agent did

- **WHEN** a Member opens a Run whose pull request changed files
- **THEN** each file is listed with its status and counts, and its diff renders with added and
  removed lines distinguishable

#### Scenario: no pull request yet

- **WHEN** the Run has produced no pull request
- **THEN** the section says so — distinctly from a change that touched no files

#### Scenario: an unshowable file is explained, not hidden

- **WHEN** a changed file is binary or its patch is too large
- **THEN** the file appears with a stated reason and a link to the vendor, and the other files
  still render
