## ADDED Requirements

### Requirement: a Run's sandbox is addressable by Run id for exactly as long as it exists

The name of the sandbox created for a Run SHALL be discoverable by that Run's id while the sandbox
lives, and SHALL cease to be discoverable when it is disposed. The record SHALL be written beside
creation and removed in the same `finally` that disposes the sandbox, and SHALL NOT be persisted.

Until now the name was a local variable and reached nothing outside the method that made it, which is
why no surface could address a sandbox. Persisting it instead would reproduce a fault this codebase
already reasoned about for previews: a stored row outlives the thing it describes and lies after a
restart. The sandbox's name has exactly that property — it is true while the sandbox exists and not
one moment longer.

#### Scenario: a surface asks for a running Run's sandbox

- **WHEN** a surface asks for the sandbox of a Run that is executing
- **THEN** it receives the name of the sandbox that Run is using

#### Scenario: the Run finishes

- **WHEN** a Run's sandbox is disposed
- **THEN** the name is no longer discoverable by that Run's id
- **AND** no surface can address the disposed sandbox

#### Scenario: the process restarts

- **WHEN** the orchestrating process restarts
- **THEN** no sandbox name from a previous process is discoverable, because none was stored

### Requirement: a human attached to a sandbox does not extend its life

In the self-host habitat a human MAY work inside a Run's sandbox while that Run executes. Their
presence SHALL NOT extend the sandbox's lifetime: the sandbox SHALL be disposed with its Run exactly
as it is today, and an attached human's session SHALL end with it.

This keeps the existing lifetime rule intact rather than adding a competing one. A shape that held a
sandbox open for a human would need the inactivity bound DEC-065 authorises, and would inherit the
leak that abandoned sandboxes already demonstrated — 31 of them and 125 GB, because a process died
before its `finally` ran. Neither is needed while the terminal lives inside the Run's own window.

#### Scenario: a Run finishes while a human is attached

- **WHEN** a Run's agent exits while a human is working in its sandbox
- **THEN** the sandbox is disposed on the Run's schedule and the human's session ends with it
- **AND** the human is told the sandbox is gone, rather than seeing a silently dead terminal

#### Scenario: nothing is held for a human who leaves

- **WHEN** an attached human stops interacting
- **THEN** no sandbox is held on their account, and no inactivity timer governs one
