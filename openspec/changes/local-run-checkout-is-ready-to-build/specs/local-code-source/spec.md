## ADDED Requirements

### Requirement: a configured setup command makes the checkout buildable before the Agent starts

Where the Connector carries a setup command, a Local Run SHALL execute it to completion **in the
Run's own checkout** — after the checkout exists and its path is recorded on the Run, and **before**
the Agent runtime is invoked. A fresh checkout has no installed dependencies and no build outputs, so
without this an Agent asked to make the tests pass (UC-016) meets a tree where they cannot run.

The command SHALL be executed as a single command line by the host's shell, in the **same
environment the Agent process receives**, so setup and the Agent resolve the same toolchain — a
dependency that installs for one and is missing for the other is the failure this rule forecloses.
The exit status SHALL be the shell's own: a command line whose last command succeeds reports success
whatever an earlier one did, and the product SHALL NOT reinterpret the line to decide otherwise.

The command SHALL be read only from the Connector. No file in the checkout SHALL be read or executed
as setup — on this lane the repository is what the Agent is editing, so a repository-declared setup
file is a different capability with a per-version trust ceremony (UC-031) and not this one.

#### Scenario: the checkout is prepared before the Agent runs

- **WHEN** a Local Run executes for a project whose Connector configures a setup command
- **THEN** the command runs to completion in that Run's checkout, and the Agent runtime is invoked
  only after it has finished

#### Scenario: nothing in the checkout can become the command

- **WHEN** a Local Run's checkout contains a file declaring setup steps
- **THEN** that file is neither read nor executed, and only the Connector's configured command runs

### Requirement: a setup that fails ends the Run by name, before any Agent spend

A setup command exiting non-zero SHALL end the Run `Failed` **before the runtime is invoked**, with a
reason that names the setup, the command as configured, and the tail of its output — so a reader can
tell a repository that does not build from an Agent that did not succeed (BR-004). The tail, because
the whole output is already in the Run's log (BR-014) and the reason carries evidence rather than a
transcript.

Nothing retries. The Run's checkout SHALL be removed on this path exactly as on any other failure, so
a failed setup leaks no checkout.

Where no setup command is configured, no process SHALL be started, no line SHALL be written about it,
and the Agent SHALL be invoked immediately — **absence is not an error**.

#### Scenario: a non-zero exit is a named refusal

- **WHEN** a Local Run's setup command exits non-zero
- **THEN** the Run ends `Failed`, the runtime was never invoked, and the reason names the setup, the
  command and the tail of its output

#### Scenario: a failed setup is distinguishable from a failed Agent

- **WHEN** a reader opens a Run that failed in setup and a Run that failed in the Agent
- **THEN** the two reasons differ in what they name, and neither could be mistaken for the other

#### Scenario: a failed setup removes its checkout

- **WHEN** a Local Run ends `Failed` because its setup command did
- **THEN** the Run's checkout no longer exists, as for any other failed Local Run

#### Scenario: no command configured runs nothing

- **WHEN** a Local Run executes for a project whose Connector configures no setup command
- **THEN** no setup process is started, the Run is not refused, and the Agent is invoked immediately

### Requirement: setup spends the phase's budget, not one of its own

A Local Run's setup SHALL be bounded by the Automation's phase timeout (BR-005) together with the
Agent, never by a second limit of its own: the clock starts before setup and the runtime is invoked
with what remains. A setup command still running when that budget expires SHALL be killed with its
process tree and the Run SHALL end `Failed` naming **the limit that fired** — a Run that ran out of
time did not fail its build, and its reason must not claim it did.

Where the budget is exhausted before the runtime can be invoked, the runtime SHALL NOT be invoked at
all and the Run SHALL end naming the same limit.

#### Scenario: an overrunning setup names the limit

- **WHEN** a Local Run's setup command is still running when the Automation's timeout expires
- **THEN** it is killed and the Run ends `Failed` naming the limit, not naming a setup failure

#### Scenario: the Agent gets what setup did not spend

- **WHEN** a Local Run's setup completes having used part of the Automation's timeout
- **THEN** the runtime is invoked bounded by the remainder, and the Run cannot outlive the one budget

### Requirement: the setup's output is in the Run's log, ahead of the Agent's

Both of the setup command's output streams SHALL reach the Run's log as they arrive, preceded by a
line naming the command that is written **before** the process starts — so a setup that hangs is
legible while it hangs, which is the phase where UC-027's watching matters most.

Setup output SHALL precede the Agent's output in that same log. A Member watching a Run SHALL see one
stream in the order the work happened, never a Run that appears idle while its dependencies install.

#### Scenario: a Member watching sees setup before the Agent

- **WHEN** a Member watches the log of a Local Run whose setup ran
- **THEN** the setup's output appears in the same log, before the Agent's, in the order it was
  produced

#### Scenario: the command is named before it runs

- **WHEN** a Local Run's setup command starts
- **THEN** a line naming the command is already readable in the Run's log, before any of the
  command's own output arrives

#### Scenario: a hanging setup is observable while it hangs

- **WHEN** a setup command has been running for longer than the log's readable lag
- **THEN** the lines it has produced so far are readable, rather than arriving only when it ends
