## ADDED Requirements

### Requirement: a sandbox's lifetime relative to a human is stated, and whatever holds one names its reaper

The specification SHALL state whether a Run's sandbox may be held open beyond the exit of the agent
process it was created for — specifically, whether a human attached to that sandbox extends its life.
Where the answer is yes, the requirement SHALL name what reclaims the sandbox when the human is no
longer there, and that reclaim SHALL NOT depend on the orchestrating process surviving.

The existing requirement *"a sandbox is per Run and does not outlive it"* pairs every creation with a
disposal in a `finally`, which is correct and insufficient: a process killed mid-Run never runs its
`finally`. That was measured on this developer's machine — **31 running sandboxes and 125 GB of disk,
25 of them probe sandboxes created every thirty seconds** — and answered by claiming the `aio-*`
namespace and sweeping it at startup. Any shape that deliberately holds a sandbox open while a human
is away inherits exactly that failure mode, so it inherits the obligation to name its reaper.

#### Scenario: an attached human goes away

- **WHEN** a human is attached to a Run's sandbox and stops interacting
- **THEN** the sandbox is reclaimed by the named mechanism, without waiting on the orchestrating
  process to run any disposal
- **AND** the Run's own record is unaffected by the reclaim

#### Scenario: a held sandbox survives the process that created it

- **WHEN** the orchestrating process is killed while a sandbox is held for a human
- **THEN** the sandbox is reclaimed by the startup sweep that claims the `aio-*` namespace
- **AND** no sandbox remains reachable only by a reference that died with the process

### Requirement: a second writer in a Run's workspace is accounted for

Where a human is permitted to work inside a sandbox whose agent is still running, the specification
SHALL state what that means for the workspace they share — at minimum, that the agent's working tree
may be mutated by someone other than the agent, and whether the Run's outcome remains attributable to
the agent alone.

Stated because it is the consequence most easily discovered late: a human running `git checkout` in a
sandbox while an agent edits the same tree is not a transport problem and no pty mechanic prevents it.

#### Scenario: a human edits the workspace mid-Run

- **WHEN** a human changes files in a sandbox whose agent Run is still executing
- **THEN** the specification's stated rule covers whether this is permitted
- **AND** if permitted, the Run's record does not attribute the human's changes to the agent
