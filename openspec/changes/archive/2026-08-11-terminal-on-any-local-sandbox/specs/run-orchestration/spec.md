## ADDED Requirements

### Requirement: a Member opens a shell in any of this machine's sandboxes

A caller holding `run.attach` SHALL be able to see this machine's sandboxes and open an interactive
shell in any of them, in the self-host habitat, from a surface that is not keyed to a Run. The listing
SHALL show enough to tell them apart — each sandbox's name, its status, and the Run it belongs to where
it has one.

The shell SHALL be a terminal and not a log: keystrokes SHALL reach the process, control characters
SHALL arrive as signals, and the terminal's geometry SHALL be sized to the viewer's window.

This is the generalisation of the Run-keyed terminal, not a replacement for it. That surface answers
"let me into the sandbox of the Run I am looking at" and SHALL keep doing so unchanged; this one answers
"let me into any sandbox this product has on this machine", including one whose Run has ended or whose
process was killed. Both reach the same sandboxes by the same mechanism.

No Run is involved in this surface, so no Story is locked by it (BR-001) and no phase timeout governs it
(BR-005).

#### Scenario: a caller lists the machine's sandboxes

- **WHEN** a caller holding `run.attach` opens the sandboxes surface in a self-host habitat
- **THEN** this machine's sandboxes within the claimed namespace are listed
- **AND** each shows its name, its status, and the Run it belongs to where it has one

#### Scenario: a caller opens a terminal on a listed sandbox

- **WHEN** a caller opens a terminal on a sandbox in the listing
- **THEN** a shell inside that sandbox is reachable
- **AND** `Ctrl-C` interrupts a running command rather than printing a character

#### Scenario: a sandbox with no Run is reachable

- **WHEN** the listing contains a sandbox left behind by a process that was killed
- **THEN** a terminal can be opened on it, attributed to no Run
- **AND** nothing about the surface requires a Run to exist

#### Scenario: an executing Run's sandbox keeps its own terminal

- **WHEN** a Run is executing and a caller uses the Run-keyed terminal
- **THEN** it behaves exactly as it did before this surface existed

### Requirement: the sandboxes surface refuses in causes a reader can tell apart

Each refusal of the sandboxes surface or its terminal SHALL name its own cause, and the causes SHALL NOT
be collapsed into one. A caller without `run.attach` SHALL be refused on permission, decided
server-side. A habitat that hosts no terminal SHALL answer that none is hosted there, and that answer
SHALL be distinguishable from a refusal on permission. A name that the current enumeration does not
contain SHALL be refused as not being this machine's to enter, whether it is a sandbox outside the
claimed namespace or no sandbox at all.

A sandbox outside the namespace and a sandbox that does not exist SHALL be refused identically, so that
the refusal cannot be used to discover what else is on the machine.

The habitat's answer SHALL be reached before any permission question, so that a deployment never
evaluates a permission for a surface it does not host.

#### Scenario: a caller lacks the permission

- **WHEN** a caller without `run.attach` requests the sandboxes surface or a terminal on a sandbox
- **THEN** the request is refused by the surface itself, not by the absence of a control
- **AND** the refusal names permission as the cause

#### Scenario: the habitat hosts no terminal

- **WHEN** anyone requests the surface in a deployed habitat
- **THEN** the answer is that no terminal is hosted here
- **AND** that answer is distinguishable from a refusal on permission, and is given without evaluating
  the caller's permissions

#### Scenario: a name outside the namespace

- **WHEN** a caller names a sandbox this product did not create
- **THEN** the request is refused as not this machine's to enter
- **AND** the refusal is identical to the one given for a sandbox that does not exist

#### Scenario: the sandbox is disposed while a terminal is open on it

- **WHEN** a sandbox is disposed while a caller holds a terminal on it
- **THEN** the shell ends and the caller is told the sandbox is gone
- **AND** the caller is not left looking at a dead terminal

## MODIFIED Requirements

### Requirement: an attach is recorded against the Run

Every attach SHALL be recorded: who attached, when, and which sandbox they entered. A sandbox carries the
machine owner's own session, so a human working inside it may act with the owner's credentials — and a
capability that leaves no trace of who used it cannot be reasoned about afterwards.

Where the sandbox belongs to a Run, the attach SHALL additionally appear in that Run's own record, so the
Run keeps a complete account of what happened to it and the two ways of opening a terminal tell the same
story.

Where the sandbox belongs to no Run, there is no Run record to write into, and the attach SHALL still be
recorded. The record SHALL therefore not depend on a Run existing: an attach on an abandoned sandbox is
exactly the case least able to be reconstructed afterwards, and so the one least safe to leave untraced.

The record SHALL be the attach event itself. The terminal's own bytes SHALL NOT be recorded, so a Run's
transcript stays the agent's record rather than becoming a screen capture.

#### Scenario: a Member attaches to a Run

- **WHEN** a caller attaches to a Run's sandbox
- **THEN** the Run's record shows that an attach happened, by whom, and at what time

#### Scenario: the record is read afterwards

- **WHEN** a Run that was attached to is read after it finishes
- **THEN** the attach is visible in its record
- **AND** the transcript contains no terminal output

#### Scenario: a Member attaches to a sandbox with no Run

- **WHEN** a caller opens a terminal on a sandbox that belongs to no Run
- **THEN** who opened it, when, and which sandbox are recorded
- **AND** the absence of a Run does not cause the attach to go unrecorded
