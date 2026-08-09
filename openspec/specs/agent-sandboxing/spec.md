# agent-sandboxing Specification

## Purpose

Where the Agent's process runs, and what stands between it and the machine that started it. The
executor prepares a workspace and decides what to run; this capability decides **where** that
happens — as a child of the orchestrator's own process, in a microVM on the same machine, or in
one created remotely over an authenticated API. The launcher is a seam, so a habitat chooses its
substrate by configuration and no other part of the product learns which one it got.

## Requirements

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

**A third arrangement exists, and only in the dev loop: the owner's own session.** Where a
habitat declares session carriage, a runtime whose credential is held in **files** SHALL have
those files provided to the sandbox, the operator SHALL be able to turn it off with one setting,
and the transcript SHALL name it as the credential source — so a Run that acts and bills as
somebody's seat says so. The state SHALL be **copied** rather than mounted, so it lives exactly
as long as the sandbox and an agent cannot alter the machine's own session, and only the
credential files SHALL be carried rather than the CLI's whole configuration tree.

The set carried SHALL be fixed by observing a real CLI inside a sandbox — recorded, not assumed.

**A runtime whose credential cannot be carried SHALL say so rather than failing mute.** Where a
credential is held somewhere a copy cannot reach — an operating system keychain, a hardware
store — the readiness surface SHALL report that runtime as not ready in a session-carrying
habitat, naming the reason and the copyable remedy, so a developer learns it before a Run does.

Session carriage SHALL be declared by the habitat that wants it and SHALL default off everywhere
else. A carried session is readable by whatever runs in the sandbox, which is acceptable where a
developer runs their own repositories and is not acceptable where a habitat runs somebody else's;
the consequence SHALL be stated where the option lives.

#### Scenario: the agent authenticates while holding nothing

- **WHEN** a Run executes under a launcher that injects credentials out of band
- **THEN** no credential value exists inside the sandbox, the agent's authenticated calls
  succeed, and the transcript names the injection as the credential source

#### Scenario: the injecting launcher has no stored credential

- **WHEN** a launcher declaring out-of-band injection is configured but the credential was never
  stored
- **THEN** the Run refuses before the agent starts, naming the store and the command that
  fixes it — never an unauthenticated agent failing later for an unrelated-looking reason

#### Scenario: the dev loop's Run runs as its owner

- **WHEN** a sandboxed Run executes in a habitat declaring session carriage, on a machine signed
  into a file-credentialled runtime's CLI, with no credential secret stored for that runtime
- **THEN** the agent authenticates as that session, the Run reaches a terminal state, and the
  transcript names the owner's session as the credential source

#### Scenario: a session that cannot travel is explained, not silently missing

- **WHEN** a session-carrying habitat holds a runtime whose credential lives outside the
  filesystem
- **THEN** readiness reports that runtime not ready, naming why its session cannot be carried and
  the remedy that makes it work — never an agent meeting "not logged in" inside a sandbox

#### Scenario: the machine's own session is not disturbed

- **WHEN** a session-carried Run has finished
- **THEN** the machine's session state is unchanged, because the sandbox held a copy

#### Scenario: another habitat does not acquire it by forgetting

- **WHEN** a habitat that does not declare session carriage executes a sandboxed Run
- **THEN** no session state exists inside the sandbox, and credentials are injected or passed as
  before

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

### Requirement: the executor and the sandbox share a machine, and the spec says so

Where agents execute in sandboxes, the sandbox SHALL obtain the Run's workspace from the
executing process's own filesystem, and the executor and the sandbox host SHALL therefore run on
the same machine. A habitat that places them apart is not supported, and nothing in the product
attempts to bridge them.

This is stated because it is the fact that decides where a habitat can put things, and it was
until now only implied — by the executor preparing a local directory and the sandbox mounting
that path. A reader choosing between a VM, a Kubernetes node and a managed job cannot discover
it from the specs, and would design against it.

A change that makes the workspace reach the sandbox some other way — a clone the sandbox
performs itself, a shared volume, a transport — SHALL modify this requirement rather than
leaving it standing beside a contradicting implementation.

#### Scenario: a habitat separates them

- **WHEN** a deployment places the executor on one machine and the sandbox host on another
- **THEN** it is unsupported: the sandbox has no way to obtain the workspace, and no component
  attempts to transfer it

#### Scenario: the constraint is discoverable

- **WHEN** somebody chooses a habitat for agent execution
- **THEN** the locality contract is readable in this capability's spec rather than inferable
  only from the executor's implementation

### Requirement: a sandbox may be created where the executing process is not

A sandbox launcher MAY create its sandboxes on a machine other than the one executing Runs, over an
authenticated API rather than a local socket. Where it does, the Run's workspace SHALL be sent to
the sandbox rather than mounted from the executing process's filesystem, and the executor and the
sandbox SHALL NOT be required to share a machine.

Such a launcher SHALL NOT require any host-level grant on the executing machine — no socket, no
privileged mount — because a grant that is root-equivalent on the host is the cost this substrate
exists to remove.

Where a launcher's command surface bounds how long a single execution may take, that bound SHALL be
absorbed by the launcher and SHALL NOT reach the executor: a Run may last as long as BR-005 allows,
and its output SHALL still be observable while it executes (UC-027).

#### Scenario: a Run executes where the executor is not

- **WHEN** a Run is dispatched in a habitat whose launcher creates sandboxes remotely
- **THEN** it executes in a sandbox on another machine, receives its workspace, and reaches a
  terminal state — and no socket or privileged grant exists on the executing machine

#### Scenario: an execution longer than the launcher's own limit still completes

- **WHEN** an agent runs for longer than a single command against that launcher may take
- **THEN** the Run completes anyway, its output appears while it works, and nothing about the
  limit is visible to the executor

### Requirement: a remotely-created sandbox declares what its platform's defaults get wrong

Where a habitat's sandboxes are created on a platform whose defaults do not suit a Run, the habitat
SHALL declare the corrections rather than inherit them, and composition SHALL refuse a habitat that
leaves them undeclared.

Two are known and SHALL be declared: **automatic suspension SHALL be disabled**, because a platform
that measures idleness by requests from outside will suspend a sandbox whose agent is thinking; and
**egress SHALL be denied by default with an explicit allow list**, because a sandbox created without
a policy may have unrestricted outbound access whatever the platform's documentation says.

A denied request SHALL be refused and SHALL be recordable, so a habitat can show what its agents
tried to reach.

#### Scenario: a thinking agent is not suspended

- **WHEN** an agent runs for several minutes producing no output and receiving no calls
- **THEN** its sandbox is still running and the Run continues

#### Scenario: an undeclared habitat refuses to start

- **WHEN** a habitat names a remote sandbox launcher without declaring suspension and egress
- **THEN** composition refuses, naming what is missing — never a deployment whose agents run
  unrestricted because a default was assumed

#### Scenario: the deny side denies

- **WHEN** an agent reaches for a host outside the allow list
- **THEN** the request is refused and the refusal is recordable, while an allowed host succeeds

### Requirement: a project's credentials stay a project's, whatever the platform scopes them to

Where a substrate scopes credentials to a container broader than a Project, the habitat SHALL give
each Project its own such container, so that a Run bills and acts as its own Project's identity
(#244) rather than a shared one.

Credential values SHALL NOT enter the sandbox where the platform can attach them at its boundary
instead, and the Run's transcript SHALL name that source (BR-010).

#### Scenario: two projects, two identities

- **WHEN** Runs of two different Projects execute on this substrate
- **THEN** each authenticates with its own Project's credential, and neither can use the other's

#### Scenario: the value never lands inside

- **WHEN** an agent executes with a credential attached at the boundary
- **THEN** no credential value is readable inside the sandbox, and the transcript names the
  injection as the source
