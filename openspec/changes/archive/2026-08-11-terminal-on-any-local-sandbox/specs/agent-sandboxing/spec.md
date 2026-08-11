## ADDED Requirements

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

## MODIFIED Requirements

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
