## ADDED Requirements

### Requirement: a Member opens a shell in an executing self-host Run's sandbox

A caller holding `run.attach` on a Run's project SHALL be able to open an interactive shell inside
that Run's sandbox while the Run executes, in the self-host habitat. The shell SHALL be a terminal
and not a log: keystrokes SHALL reach the process, control characters SHALL arrive as signals, and
the sandbox's terminal geometry SHALL follow the viewer's window.

The agent SHALL continue to run headless and unaffected. The shell is a second process beside it,
sharing the sandbox and its workspace but not the agent's standard input — which is what leaves the
agent's structured output, and therefore the Run's transcript, exactly as it was.

#### Scenario: a caller attaches to a stuck Run

- **WHEN** a caller holding `run.attach` opens the terminal of an executing self-host Run
- **THEN** a shell inside that Run's sandbox is reachable, and its workspace is the Run's own
- **AND** `Ctrl-C` interrupts a running command rather than printing a character

#### Scenario: the viewer's window sets the geometry

- **WHEN** a terminal is opened
- **THEN** the sandbox's pty is sized to the viewer's window, so full-screen programs draw correctly
- **AND** a later change to the window size does not reflow the open terminal, which the surface says
  rather than leaving a reader waiting for a redraw

#### Scenario: the agent keeps working while a human is attached

- **WHEN** a human is attached to a Run whose agent is mid-phase
- **THEN** the agent's phase is unaffected and its timeout keeps its meaning (BR-005)
- **AND** the Run's transcript contains none of the terminal's bytes

### Requirement: a terminal refuses in three distinguishable ways

A terminal SHALL NOT be offered by default, and each refusal SHALL name its own cause. A caller
without `run.attach` SHALL be refused on permission, decided server-side. A habitat that hosts no
terminal SHALL answer that no terminal is hosted there. A Run that is not executing SHALL offer
nothing at all.

These are three different sentences and SHALL NOT be collapsed into one: a habitat's limitation
rendered as a permission failure teaches a Member to ask for access that would not help, and a
finished Run rendered as a disabled control promises something no Run can keep.

#### Scenario: a caller lacks the permission

- **WHEN** a caller without `run.attach` requests a Run's terminal
- **THEN** the request is refused by the surface itself, not by the absence of a control
- **AND** the refusal names permission as the cause

#### Scenario: the habitat hosts no terminal

- **WHEN** a Run executes where the launcher is not a local sandbox host
- **THEN** the answer is that no terminal is hosted in this habitat
- **AND** that answer is distinguishable from a refusal on permission

#### Scenario: the Run has finished

- **WHEN** a Run has reached a terminal state
- **THEN** no terminal is offered, and no affordance suggests one could be

### Requirement: an attach is recorded against the Run

Every attach SHALL be recorded against the Run: who attached, and when. A Run's sandbox carries the
machine owner's own session, so a human working inside it may act with the owner's credentials — and
a capability that leaves no trace of who used it cannot be reasoned about afterwards.

The record SHALL be the attach event itself. The terminal's own bytes SHALL NOT be recorded, so the
Run's transcript stays the agent's record rather than becoming a screen capture.

#### Scenario: a Member attaches to a Run

- **WHEN** a caller attaches to a Run's sandbox
- **THEN** the Run's record shows that an attach happened, by whom, and at what time

#### Scenario: the record is read afterwards

- **WHEN** a Run that was attached to is read after it finishes
- **THEN** the attach is visible in its record
- **AND** the transcript contains no terminal output
