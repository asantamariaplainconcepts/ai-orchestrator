## MODIFIED Requirements

### Requirement: the agent runtimes are observable where they run

A process that executes Runs SHALL probe each registered agent runtime on a stated cadence —
the runtime's CLI answers, and its configured credential resolves — and SHALL expose the result
beside the pod host's own readiness: state, last-checked time, the probe's cadence, and a
copyable remedy for each not-ready cause. A missing executable and an unresolvable credential
SHALL be distinguished, because their remedies differ. A Run that fails anyway SHALL carry the
same remedy in its failure reason (BR-004: nothing retries, so the failure carries everything):
a missing executable names the binary, that PATH resolution failed, and the install command; a
missing secret names the secret and the store to add it to — never a value (BR-010).

A runtime whose credential configuration is empty or whitespace SHALL be treated as having no
credential requirement, identically across runtimes: nothing is resolved, no credential
variable is exported to the agent process, and the CLI runs with the machine's own session —
the same session the pod default already mounts deliberately.

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
