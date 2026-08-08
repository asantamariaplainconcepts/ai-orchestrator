## MODIFIED Requirements

### Requirement: the executor selects the workspace per Run by locus

`RunExecutor` SHALL obtain the Run's workspace through the existing `ICodeWorkspace` seam,
selected by the Run's locus: `Sandbox` clones fresh into an isolated machine of its own; `Local`
uses the folder workspace. Locus is a workspace decision inside the executor, never a routing
decision (design D1) — dispatch is identical for both.

The value was named `Pod` until this change. The substrate it was named after is retired here,
the domain glossary has always said an Agent is "never a pod", and every substrate that replaced
it is literally a sandbox. Because the value is persisted as a string, the rename is a data
migration and not only a rename: every existing row is rewritten, or the next read of a
historical Run throws.

#### Scenario: a Sandbox run clones fresh

- **WHEN** a Run with locus `Sandbox` executes
- **THEN** the workspace is a fresh shallow clone and every existing agent-execution requirement
  holds without modification

#### Scenario: a Run stored before the rename still loads

- **WHEN** a Run row written with locus `Pod` is read after the upgrade
- **THEN** it reads as `Sandbox` — the migration rewrites the rows, so no historical Run becomes
  unreadable

#### Scenario: audit fields extend for Local (BR-014)

- **WHEN** a Local Run reaches a terminal state
- **THEN** the Run row carries its locus, working folder and branch name alongside every
  existing audit field
