## ADDED Requirements

### Requirement: a habitat can execute the agent in a sandbox while the executor stays outside

Where a habitat names a sandbox launcher, the agent's CLI SHALL execute inside a per-Run
sandbox while the Run's executor — the module host, the Contracts reads, secret resolution and
every state write — remains in the calling process. Selection SHALL follow configuration
presence (ADR-0010): a launcher named selects the sandboxed process host; nothing named keeps
in-process execution unchanged in every habitat.

The runtimes' own behaviour SHALL be unaffected by where the process runs: the same command
line, the same streamed output (#96), the same timeout semantics (BR-005) and the same usage
parsing (BR-011) apply in both.

#### Scenario: the agent runs in a sandbox and the Run is unchanged

- **WHEN** a Run executes in a habitat naming a sandbox launcher
- **THEN** the agent's CLI runs inside a sandbox, its output streams to the Run as it arrives,
  and the Run reaches the same terminal state with the same log and usage it would have reached
  in-process

#### Scenario: nothing configured, nothing changes

- **WHEN** no sandbox launcher is named
- **THEN** the agent executes as a child process of the executing host, exactly as before

#### Scenario: the sandbox cannot be created

- **WHEN** a Run's sandbox cannot be created because the sandbox host is absent or unhealthy
- **THEN** the Run fails naming what is missing and the remedy for it — never a hang, and never
  a silent fallback to executing outside the sandbox

### Requirement: the orchestrator's own credentials and connections never enter the sandbox

A sandbox SHALL receive the Run's workspace and what the agent needs to do its work, and SHALL
NOT receive the database connection string, secret-store locations, or the orchestrator's module
configuration. The composition SHALL make this structural rather than advisory: the launcher is
constructed with its own options and has no access to the host's configuration.

#### Scenario: a sandbox holds no orchestrator credential

- **WHEN** a Run executes in a sandbox
- **THEN** no database connection string and no secret-store path exists inside it, and nothing
  in the launcher's construction could supply one

### Requirement: a credential is either injected out of band or passed, and never silently absent

A sandbox launcher SHALL declare whether it supplies the agent's credentials out of band — a
host-side mechanism authenticating the agent's requests without the value entering the sandbox.
Where it does, the runtime SHALL NOT export credential values into the sandbox, and the Run's
transcript SHALL name that source. Where it does not, credentials SHALL travel as values for the
process's lifetime exactly as they do in-process (BR-010: values never at rest).

A launcher that declares out-of-band injection SHALL verify the credential is present before the
agent starts, and SHALL refuse the Run naming the store and the command that fixes it. An agent
SHALL NOT be started with neither an injected nor a passed credential.

#### Scenario: the agent authenticates while holding nothing

- **WHEN** a Run executes under a launcher that injects credentials out of band
- **THEN** no credential value exists inside the sandbox, the agent's authenticated calls
  succeed, and the transcript names the injection as the credential source

#### Scenario: the injecting launcher has no stored credential

- **WHEN** a launcher declaring out-of-band injection is configured but the credential was never
  stored
- **THEN** the Run refuses before the agent starts, naming the store and the command that
  fixes it — never an unauthenticated agent failing later for an unrelated-looking reason

### Requirement: a sandbox is per Run and does not outlive it

A sandbox SHALL be created for exactly one Run and disposed when that Run's agent finishes,
however it finishes. Disposal SHALL survive cancellation, because an abandoned sandbox is the
leak. A sandbox SHALL NOT be reused across Runs.

#### Scenario: a cancelled Run leaves no sandbox

- **WHEN** a Run is cancelled while its agent executes in a sandbox
- **THEN** the sandbox is disposed anyway, and no sandbox from that Run remains

#### Scenario: state does not travel between Runs

- **WHEN** two Runs of different projects execute in sequence
- **THEN** the second runs in a sandbox that carries nothing from the first

### Requirement: the workspace the agent sees is proven, not assumed

A launcher SHALL make the Run's workspace available inside the sandbox and SHALL report the path
the agent's command will see. It SHALL verify that path is present inside the sandbox before the
agent starts, and refuse naming the mapping when it is not.

#### Scenario: an unmapped workspace refuses at the boundary

- **WHEN** the workspace is not visible inside the sandbox at the reported path
- **THEN** the Run refuses naming the workspace and the mapping — never an agent reporting a
  missing repository

### Requirement: two isolation substrates are refused rather than layered

A habitat naming both a pod image and a sandbox launcher SHALL be refused at composition,
naming both keys, because one silently winning would make the operator's other choice an
invisible no-op.

#### Scenario: the ambiguous habitat refuses to start

- **WHEN** a host is configured with both a pod image and a sandbox launcher
- **THEN** startup fails naming both configuration keys and what to remove
