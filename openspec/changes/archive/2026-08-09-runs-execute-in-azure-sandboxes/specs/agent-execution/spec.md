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

### Requirement: the agent runtimes are observable where they run

Two phrases in this requirement described the pod substrate as a live thing — readiness sat
"beside the pod host's own", and the switched-off credential state was explained by "the session
the pod default already mounts". Both are retired here, and a requirement that explains itself by
a substrate the reader cannot find is worse than one that says less.

A process that executes Runs SHALL probe each registered agent runtime on a stated cadence —
the runtime's CLI answers, and its configured credential resolves — and SHALL expose the result
beside the readiness of the machine those runtimes describe: state, last-checked time, the probe's cadence, and a
copyable remedy for each not-ready cause. A missing executable and an unresolvable credential
SHALL be distinguished, because their remedies differ. A Run that fails anyway SHALL carry the
same remedy in its failure reason (BR-004: nothing retries, so the failure carries everything):
a missing executable names the binary, that PATH resolution failed, and the install command; a
missing secret names the secret and the store to add it to — never a value (BR-010).

A runtime whose credential configuration is empty or whitespace SHALL be treated as having no
credential requirement, identically across runtimes: nothing is resolved, no credential
variable is exported to the agent process, and the CLI runs with the machine's own session.

**Where the agent executes in a sandbox, readiness SHALL describe the machine the CLI actually
runs on, never this process's own binaries.** The probe SHALL report the sandbox host's own
preconditions — the sandbox service reachable, and whatever else it requires before a sandbox
can be created — each with its own remedy, and SHALL report a runtime's CLI readiness from
where that CLI will run. A probe that cannot reach the sandbox host SHALL say so rather than
answering from this process, because "ready here" is not an answer about a Run that will execute
elsewhere.

#### Scenario: a missing CLI is visible before any Run

- **WHEN** a registered runtime's executable is not on the executing process's PATH
- **THEN** the environment surface shows that runtime not ready, naming the binary and a
  copyable install command at the repository's pinned version, with the last-checked time

#### Scenario: a Run that fails anyway names the remedy

- **WHEN** a Run dispatches to a runtime whose executable cannot start
- **THEN** its failure reason names the binary, that PATH resolution failed, and the install
  remedy — never a raw process error alone

#### Scenario: an unresolvable credential is its own state

- **WHEN** a runtime's configured credential name resolves to no secret
- **THEN** the environment surface and any Run failure name the secret and the store to add it
  to, and no value ever appears

#### Scenario: switched off means the machine's own session

- **WHEN** a runtime's credential configuration is set to empty or whitespace
- **THEN** no secret is resolved, no credential variable reaches the agent process, and a Run
  executes with the machine's own session

#### Scenario: the sandbox host's own preconditions are visible

- **WHEN** Runs execute in sandboxes and the sandbox host is unreachable or unprepared
- **THEN** the environment surface names that precondition and its remedy with the last-checked
  time, distinguished from a missing runtime CLI

#### Scenario: readiness does not answer for the wrong machine

- **WHEN** Runs execute in sandboxes and this process happens to have a runtime's CLI installed
- **THEN** readiness reports the CLI from where Runs will actually run, never reporting ready on
  the strength of this process's own PATH
