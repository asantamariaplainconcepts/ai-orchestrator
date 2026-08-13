## ADDED Requirements

### Requirement: the code source may be set when the Project is created

Where a Project is created naming a folder on this machine, the resulting Connector SHALL carry the
`LocalFolder` code source with that folder's absolute path, without the Admin visiting the Connector
form. The path SHALL be subject to the same inspection the Connector form's own validation performs,
through the same `ILocalCodeWorkspace` seam, so the two cannot disagree about what a usable folder
is.

Everything the code source already governs SHALL hold unchanged for a Connector configured this way:
BR-016's checkout rules, the no-push and no-pull-request output, the reaping of abandoned checkouts,
and the habitat refusal where the Local locus is declared unavailable. This requirement changes
**when** the code source may be set, never **what** it does.

#### Scenario: a created Project already has its code source

- **WHEN** a Project is created naming a folder that is a git repository
- **THEN** its Connector carries the `LocalFolder` code source and that folder's path, and no
  Connector-form visit was required

#### Scenario: a Run on such a Connector behaves as any Local Run

- **WHEN** a Run is dispatched for a Project whose code source was set at creation
- **THEN** it works in its own checkout, leaves a branch, pushes nothing and opens no pull request,
  exactly as BR-016 already requires

#### Scenario: a declaring habitat still refuses

- **WHEN** a Project is created naming a folder in a habitat that declares the Local locus
  unavailable
- **THEN** the creation is refused carrying the declared reason verbatim, and no Project gains a
  local path
