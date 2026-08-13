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

### Requirement: a human may hold a sandbox open in self-host and nowhere else, and whatever holds one names its reaper

A Run's sandbox MAY outlive the exit of the agent process it was created for **only in the self-host
habitat, and only while a human is attached to it** (DEC-065). In a deployment a sandbox SHALL NOT be
held for a human: it is disposed with its Run as it is today.

Where a sandbox is held, it SHALL be reclaimed on **inactivity of the sandbox**, and that reclaim
SHALL NOT depend on the orchestrating process surviving. The startup sweep that claims the `aio-*`
namespace SHALL remain the backstop, and SHALL NOT treat a deliberately held sandbox as abandoned
while its human is still attached.

The existing requirement *"a sandbox is per Run and does not outlive it"* pairs every creation with a
disposal in a `finally`, which is correct and insufficient: a process killed mid-Run never runs its
`finally`. That was measured on the developer's machine — **31 running sandboxes and 125 GB of disk**,
25 of them probe sandboxes created every thirty seconds — and answered by claiming the namespace and
sweeping it at startup. Holding a sandbox on purpose inherits exactly that failure mode, which is why
the reaper is named here rather than assumed.

#### Scenario: an attached human goes away

- **WHEN** a human attached to a Run's sandbox in self-host stops interacting
- **THEN** the sandbox is reclaimed by its inactivity bound, without waiting on the orchestrating
  process to run any disposal
- **AND** the Run's own record is unaffected by the reclaim

#### Scenario: a held sandbox survives the process that created it

- **WHEN** the orchestrating process is killed while a sandbox is held for a human
- **THEN** the sandbox is reclaimed by the startup sweep that claims the `aio-*` namespace
- **AND** no sandbox remains reachable only by a reference that died with the process

#### Scenario: a deployed Run's agent finishes

- **WHEN** an agent process exits in a deployment, whether or not anyone is watching
- **THEN** its sandbox is disposed with the Run, and nothing holds it for a human

### Requirement: a second writer in a Run's workspace is accounted for

Where a human works inside a sandbox whose agent is still running — which self-host now permits — the
specification SHALL state what that means for the workspace they share: the agent's working tree MAY
be mutated by someone other than the agent, and the Run's record SHALL NOT attribute a human's changes
to the agent.

Stated because it is the consequence most easily discovered late: a human running `git checkout` in a
sandbox while an agent edits the same tree is not a transport problem, and no pty mechanic prevents
it.

#### Scenario: a human edits the workspace mid-Run

- **WHEN** a human changes files in a self-host sandbox whose agent Run is still executing
- **THEN** the change is permitted, and the Run's record does not present those changes as the
  agent's own work

#### Scenario: attribution is asked of a Run that was attached to

- **WHEN** a Member asks what a Run changed, and a human had a shell in its sandbox
- **THEN** the answer distinguishes what the agent did from what the Run's workspace ended up holding

### Requirement: a Run's sandbox is addressable by Run id for exactly as long as it exists

The name of the sandbox created for a Run SHALL be discoverable by that Run's id while the sandbox
lives, and SHALL cease to be discoverable when it is disposed. The record SHALL be written beside
creation and removed in the same `finally` that disposes the sandbox, and SHALL NOT be persisted.

Until now the name was a local variable and reached nothing outside the method that made it, which is
why no surface could address a sandbox. Persisting it instead would reproduce a fault this codebase
already reasoned about for previews: a stored row outlives the thing it describes and lies after a
restart. The sandbox's name has exactly that property — it is true while the sandbox exists and not
one moment longer.

**Discovery by Run id and discovery of the machine are different questions, and SHALL stay so.** The
per-Run ledger answers "which sandbox is this Run using", holds only this process's executing Runs, and
is never stored. Enumerating the machine answers "what sandboxes are on this machine", and reads the
machine itself. A sandbox left behind by a previous process is therefore absent from the first and
present in the second — not because anything was persisted, but because the sandbox is really there.

#### Scenario: a surface asks for a running Run's sandbox

- **WHEN** a surface asks for the sandbox of a Run that is executing
- **THEN** it receives the name of the sandbox that Run is using

#### Scenario: the Run finishes

- **WHEN** a Run's sandbox is disposed
- **THEN** the name is no longer discoverable by that Run's id
- **AND** no surface can address the disposed sandbox, by its Run's id or by its name, because it is
  gone from the machine

#### Scenario: the process restarts

- **WHEN** the orchestrating process restarts
- **THEN** no sandbox name from a previous process is discoverable by any Run's id, because none was
  stored

#### Scenario: a sandbox outlives the Run that owned it

- **WHEN** a process is killed before its `finally` disposes a Run's sandbox, and a new process starts
- **THEN** the sandbox is discoverable by enumerating the machine, with no Run attributed to it
- **AND** it is not discoverable by the id of the Run that created it

### Requirement: a human attached to a sandbox does not extend its life

In the self-host habitat a human MAY work inside a Run's sandbox while that Run executes. Their
presence SHALL NOT extend the sandbox's lifetime: the sandbox SHALL be disposed with its Run exactly
as it is today, and an attached human's session SHALL end with it.

This keeps the existing lifetime rule intact rather than adding a competing one. A shape that held a
sandbox open for a human would need the inactivity bound DEC-065 authorises, and would inherit the
leak that abandoned sandboxes already demonstrated — 31 of them and 125 GB, because a process died
before its `finally` ran. Neither is needed while the terminal lives inside the Run's own window.

**This requirement binds a SANDBOX, not a terminal** (OPN-008, closed by ADR-0029 / DEC-070). Where a
terminal opens on the host rather than in a sandbox there is no sandbox to dispose, and the rule that
applies instead is the one below: the terminal ends with the Run, and the Run's checkout is reaped on
its own schedule. The distinction is written down because the requirement's wording presumed a sandbox
existed, and a reader implementing a host terminal would otherwise have to guess which half applied.

#### Scenario: a Run finishes while a human is attached

- **WHEN** a Run's agent exits while a human is working in its sandbox
- **THEN** the sandbox is disposed on the Run's schedule and the human's session ends with it
- **AND** the human is told the sandbox is gone, rather than seeing a silently dead terminal

#### Scenario: nothing is held for a human who leaves

- **WHEN** an attached human stops interacting
- **THEN** no sandbox is held on their account, and no inactivity timer governs one

### Requirement: this machine's own sandboxes are enumerable, bounded to the namespace the host claims

The sandboxes present on the executing machine SHALL be enumerable in the self-host habitat, and the
enumeration SHALL contain a sandbox if and only if its name carries a prefix this host claims as its
own. A sandbox created outside this product SHALL be absent from the enumeration, and SHALL NOT be
reachable by naming it.

The claimed prefixes SHALL be the same ones the startup sweep reaps — today `aio-probe-` and `aio-run-`,
as `SbxSandboxLifecycle.ReapAbandoned` applies them
(`src/shared/AiOrchestrator.Infrastructure/Agents/Sbx/SbxSandboxLifecycle.cs`). The predicate SHALL have
a single definition used by both the sweep and the enumeration, so what this host may enter and what it
may delete cannot drift apart. `aio-` is not itself the boundary: names such as `aio-carry-` and
`aio-workspace-` are host temp paths rather than sandboxes.

Each entry SHALL carry enough to tell the sandboxes apart — its name, its status, and the Run it belongs
to where that is known. Attribution SHALL come from the unpersisted per-Run ledger and SHALL therefore be
absent for a sandbox this process did not create, which is a true statement about the machine rather than
a gap.

#### Scenario: the machine's sandboxes are listed

- **WHEN** the sandboxes of a self-host machine are enumerated
- **THEN** every sandbox whose name carries a claimed prefix is present, with its name and status
- **AND** each names the Run it belongs to where that Run is one this process is executing

#### Scenario: a sandbox this product did not create

- **WHEN** the machine holds a sandbox created outside this product, such as one named for another tool
- **THEN** it is absent from the enumeration
- **AND** no request naming it can reach it

#### Scenario: a sandbox survives the process that made it

- **WHEN** a process is killed and leaves a sandbox carrying a claimed prefix behind
- **THEN** the enumeration of the next process contains it, with no Run attributed to it
- **AND** it remains subject to the startup sweep that claims the same prefixes

#### Scenario: the enumeration and the sweep agree

- **WHEN** the set of names the sweep would reap is compared with the set the enumeration exposes
- **THEN** they are the same set, because both apply one definition of what this host owns

### Requirement: a sandbox is addressable by its own name, and a caller's name is resolved before it is used

A sandbox SHALL be addressable by its name and not only by the id of the Run that owns it, so a sandbox
with no live Run can be reached. A name supplied by a caller SHALL NOT be passed to the sandbox runtime
as given: it SHALL first be resolved against a freshly-read enumeration, and refused when absent.

Resolution SHALL be performed at the moment of use rather than trusted from an earlier read. A caller's
list is a memory of what was true when it was produced, and a sandbox may have been reaped and its name
reused in between.

This replaces, rather than deletes, the bound #304 relied on. That slice resolved a sandbox only through
the per-Run ledger, which is why a caller-supplied name could not reach the runtime at all
(`src/shared/AiOrchestrator.Infrastructure/Agents/Sbx/SbxRunTerminalHost.cs`). The namespace predicate
SHALL be what makes name-addressing safe in its place, and the code SHALL NOT continue to assert the
older invariant it no longer holds.

#### Scenario: a listed sandbox is entered by name

- **WHEN** a caller names a sandbox that the current enumeration contains
- **THEN** the sandbox is reached

#### Scenario: an unlisted name is refused

- **WHEN** a caller names a sandbox outside the claimed namespace, or one that does not exist
- **THEN** the name is refused and never reaches the sandbox runtime

#### Scenario: the sandbox goes between listing and entering

- **WHEN** a sandbox is reaped after a caller read the list but before they name it
- **THEN** the resolution finds it absent and the caller is told the sandbox is gone

### Requirement: entering a stopped sandbox starts it, and the surface says so

Entering a sandbox that is not running SHALL be permitted, and the surface SHALL state that entering it
starts it. It SHALL NOT be presented as though entering were free.

This is a property of the runtime rather than a choice: `sbx exec` against a stopped sandbox starts that
sandbox and then runs the command, verified against the real CLI on 2026-08-11. Left unsaid, entering a
stopped sandbox would silently boot a virtual machine — the resource leak the startup sweep exists to
answer, arrived at by a different route.

A sandbox started this way SHALL NOT be held for the person who started it, and SHALL remain subject to
the sweep that claims the namespace. Nothing here extends any sandbox's life on a human's account.

#### Scenario: a stopped sandbox is entered

- **WHEN** a caller opens a terminal on a sandbox whose status is stopped
- **THEN** the sandbox is started and a shell inside it is reachable
- **AND** the caller was told beforehand that entering it would start it

#### Scenario: a started sandbox is not held

- **WHEN** a caller who started a sandbox by entering it disconnects
- **THEN** no timer holds the sandbox on their account, and the startup sweep remains its backstop

### Requirement: a terminal may open on the host, bounded to the Run's own checkout

In the **self-host** habitat, a caller holding `run.attach` MAY open a terminal on an executing Run
where the habitat hosts no sandbox. A **deployment SHALL refuse it**, unchanged by this requirement,
and SHALL refuse it as *not available in this habitat* rather than as *not permitted for you*.

Today a terminal is a property of the sbx launcher rather than of locality: `IRunTerminalHost` is
registered only in the sbx branch of composition, so the one habitat ADR-0021 permits attaching in is
the one habitat with no terminal. DEC-065 does not settle this on its own — it was decided about a
session *inside a Run's sandbox*, and the requirement above presumes one exists.

Where such a terminal opens, all of the following SHALL hold:

- its **working directory** is that Run's own checkout, never the operator's own folder;
- the child's **environment SHALL NOT be inherited** from the server process. The pseudo-terminal is
  started with `posix_spawn`, which takes the child's whole environment, and the sandbox path
  deliberately inherits and overlays because the launcher CLI needs it. Inside a sandbox that is
  harmless, since nothing crosses the boundary; on the host there is no boundary, so an inherited
  environment would hand a shell whatever the habitat resolved into the server's;
- the shell started is a **named** one, not the operator's login shell with their profile;
- the **audit record distinguishes** a host terminal from a sandbox terminal, so a reader can never
  assume a bound that was not there.

**The bound is a product boundary and not a kernel one, and the product SHALL NOT imply otherwise.**
A shell opened in the Run's checkout can still leave it. This requirement buys a sane default and an
honest description; isolation is what the sandbox launcher is for, and it remains available.

#### Scenario: the local habitat opens a terminal

- **WHEN** a caller holding `run.attach` opens a terminal on an executing Run in a habitat with no
  sandbox launcher configured
- **THEN** a shell opens in that Run's own checkout
- **AND** its environment is not the server process's

#### Scenario: a deployment still refuses

- **WHEN** anyone attempts to open a terminal on a Run in a deployment
- **THEN** it is refused as not available in this habitat
- **AND** that refusal is distinguishable from not being permitted

#### Scenario: the record says what was entered

- **WHEN** a terminal is opened on the host rather than in a sandbox
- **THEN** the audit record names which of the two it was

### Requirement: what a terminal opens on is named for what it is

The type describing a place a terminal may open SHALL NOT be named for a sandbox once it may also
describe a Run's checkout. `LocalSandbox`, `MachineSandboxAccess` and `ListMachineSandboxes` are
accurate only while every terminal lives in a sandbox, and cease to be with the requirement above.

This is stated as a requirement rather than left to implementation because a type called
`LocalSandbox` holding a checkout path is the kind of small inaccuracy a later reader takes literally —
the same class of defect as a test that reports green while reading the wrong checkout.

#### Scenario: a reader inspects the seam

- **WHEN** the terminal seam is read after a host terminal exists
- **THEN** no type, method or surface name claims a sandbox for something that is a checkout

