# connector-configuration — delta

## ADDED Requirements

### Requirement: a Connector separates its code source from its backlog vendor

A Connector SHALL carry a code source: `Repository` (the default — the vendor's repository, as
today) or `LocalFolder` with an absolute path on the host. Stories always come from the backlog
vendor regardless of code source. Every Connector existing before this change SHALL behave as
`Repository` with no migration side effects.

#### Scenario: local folder saved in the self-host posture

- **WHEN** an Admin reconfigures a Connector with `codeSource=localFolder` and a path that
  validates as a git repository
- **THEN** the Connector persists the kind and path, and the backlog coordinates and credential
  semantics are untouched

#### Scenario: existing Connectors are unchanged

- **WHEN** the migration runs on a database with existing Connectors
- **THEN** every row reads back as `codeSource=repository` with a null path, and polling,
  labelling and dispatch behave exactly as before the change
