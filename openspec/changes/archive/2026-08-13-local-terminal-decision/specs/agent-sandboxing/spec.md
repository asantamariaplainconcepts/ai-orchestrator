## MODIFIED Requirements

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

## ADDED Requirements

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
