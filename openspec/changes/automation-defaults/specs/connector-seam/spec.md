# connector-seam

## ADDED Requirements

### Requirement: a label can be ensured in the repository, not only on a Story

The seam SHALL expose a way to ensure a label exists in a connector's repository, independent of
any Story. It SHALL succeed when the label already exists, so callers need not distinguish
"created" from "was already there". Where a vendor has no repository-level concept of a label,
the implementation SHALL succeed without acting rather than simulate one by labelling an
arbitrary work item, and SHALL say so where a reader will find it.

#### Scenario: the label is absent

- **WHEN** a label is ensured in a repository that does not have it
- **THEN** it exists afterwards and no Story has been modified

#### Scenario: the label is already there

- **WHEN** a label that already exists is ensured
- **THEN** the call succeeds and nothing changes

#### Scenario: a vendor with no repository-level labels

- **WHEN** a label is ensured against a vendor whose tags only exist once applied to a work item
- **THEN** the call succeeds without creating or modifying anything
