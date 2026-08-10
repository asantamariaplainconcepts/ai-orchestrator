## ADDED Requirements

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
