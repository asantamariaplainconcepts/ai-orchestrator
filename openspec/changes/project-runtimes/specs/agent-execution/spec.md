## MODIFIED Requirements

### Requirement: the Automation's runtime decides which agent executes

Run execution SHALL select the `IAgentRuntime` implementation through a selector seam, resolving
the runtime name in a stated order: the human's per-Run choice recorded on the Run, then the
Automation's explicit runtime, then the Project default, then the deployment default. The
credential secret name resolves the same way one level down: the Project's name for that runtime,
then the deployment's, then none. A runtime whose resolved credential name is absent SHALL execute
with no resolved credential (free providers, DEC-044); a runtime naming a credential keeps the
resolve-by-name path (BR-010). Adding a runtime SHALL be a composition change, never an executor
edit.

The transcript SHALL name which source the credential came from — project, deployment, or none —
because a Run billed to the wrong key must be diagnosable from its own record.

**An absent credential SHALL NOT shadow a host identity.** A runtime process environment SHALL
only carry a credential variable when there is a non-empty value to carry: exporting an empty
`GITHUB_TOKEN` or API key overrides whatever auth the host's own tooling holds, which is exactly
the Local lane's working state (#210).

#### Scenario: two runtimes, two paths

- **WHEN** two Automations differ only in runtime and their Runs execute
- **THEN** each Run is executed by its runtime's implementation

#### Scenario: a free-model runtime needs no vault entry

- **WHEN** an OpenCode-runtime Run executes with no credential secret configured
- **THEN** the Run proceeds — no vault lookup occurs and no failure is manufactured

#### Scenario: the chain resolves in order

- **WHEN** a Run carries a per-Run choice, its Automation names a runtime, and the Project has a
  default
- **THEN** the per-Run choice wins; absent it, the Automation's; absent both, the Project's;
  absent all three, the deployment default

#### Scenario: the project credential outranks the deployment's

- **WHEN** a Project names a credential for the resolved runtime and the deployment also has one
- **THEN** the Run resolves the Project's name, and the transcript says the project supplied it

#### Scenario: an empty token does not reach the environment

- **WHEN** a Local Run executes with no vendor credential resolved
- **THEN** the runtime's process environment carries no empty credential variable, and the host's
  own auth remains in effect
