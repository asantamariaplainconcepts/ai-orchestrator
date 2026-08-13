# local-code-source Specification

## Purpose
A project's code may come from a folder on the orchestrator's host (self-host flavour, DEC-049, #210): configuration and posture gating, path validation, and the local workspace whose output is a branch, never a push or a PR (BR-016).
## Requirements
### Requirement: the code-source surface exists only in the self-host posture

The code-source configuration and validation endpoints SHALL be composed only when the
deployment runs in the self-host posture (the same discriminator that composes the LocalOwner
identity). A deployment without that posture MUST answer 404 for the whole surface — the option
is absent, never merely disabled — because a path input naming a cloud container's own disk is a
trap, not a feature (DEC-049).

#### Scenario: cloud deployment hides the surface

- **WHEN** a deployment composed without the self-host posture receives any code-source request
  (configure with `localFolder`, or validate-path)
- **THEN** it answers 404 and no Connector gains a local path

#### Scenario: self-host deployment offers it to Admins only

- **WHEN** a caller without Admin standing on the project calls validate-path in a self-host
  deployment
- **THEN** the request is refused by the ordinary permission gate (BR-009)

### Requirement: a local path is validated against the host before it is trusted

The product SHALL answer, for a given absolute path: whether it is a directory, whether it is a
git repository, its current branch, and whether its working tree is clean. The endpoint MUST
answer about exactly the one path it was given — it never lists directory contents.

#### Scenario: a valid repository

- **WHEN** validate-path is called with a path that is a clean git checkout on branch `main`
- **THEN** the response carries `isDirectory=true, isGitRepository=true, branch="main",
  isClean=true`

#### Scenario: the specific failure is named

- **WHEN** validate-path is called with a directory that is not a git repository
- **THEN** the response carries `isDirectory=true, isGitRepository=false` and no branch, so the
  UI can name the failing check rather than a generic error

### Requirement: Local Runs use the host's own credentials, and the log says so

A Local Run SHALL skip vendor-credential resolution and use whatever credentials the host's own
tooling already holds. The Run's log MUST state this in one line, so a reader never wonders which
identity touched their folder. Nothing about a local run's credentials is stored (DEC-052).

#### Scenario: no secret is resolved for a local run

- **WHEN** a Local Run executes
- **THEN** the secret-resolution seam is not invoked for the workspace, and the log carries the
  single line stating host credentials were used

### Requirement: a habitat that cannot reach the folder refuses by name

Where the habitat declares the Local locus unavailable, naming a `LocalFolder` code source SHALL be
refused at save, and a Run SHALL NOT resolve to the Local locus — both refusals carrying the declared
reason verbatim, never a path error from inside a container. No checkout SHALL be attempted.

The refusal SHALL exist at the API, not only in the portal: a Connector stored before the
declaration, or a request made around the portal, meets the same sentence.

#### Scenario: saving a LocalFolder Connector in a declaring habitat

- **WHEN** an Admin submits a Connector with the `LocalFolder` code source where the habitat declares
  the locus unavailable
- **THEN** the save is refused with the declared reason, and nothing is stored

#### Scenario: a pre-existing LocalFolder Connector cannot produce a Local Run

- **WHEN** a Run would resolve to the Local locus in a declaring habitat
- **THEN** the Run is refused with the declared reason — it does not fail later on a container path

#### Scenario: no checkout is attempted where the locus is unavailable

- **WHEN** a Run would resolve to the Local locus in a habitat declaring it unavailable
- **THEN** no worktree is created and no `git` command runs against any configured path

#### Scenario: the portal never offers what the habitat withheld

- **WHEN** the code-source section renders in a declaring habitat
- **THEN** the local-folder option is not offered and the reason is shown in its place

### Requirement: a Local Run works in its own checkout and leaves a branch, never a push

A Run with locus Local SHALL execute in **its own checkout of** the Connector's configured folder:
create a `git` worktree of that repository on branch `ai/{vendorStoryId}-{slug}`, hand the worktree's
path to the Agent runtime, and commit what changed. It MUST NOT push and MUST NOT open a pull
request — the output is the local branch, recorded on the Run; `OutputLink` stays null.

The configured folder MUST NOT be written to, checked out, or otherwise entered: its `HEAD`, its
current branch and its uncommitted changes SHALL be exactly as they were when the Run ends, whatever
state the Run ends in. Consequently the working tree is **not** required to be clean — neither
before dispatch nor at execution — and there is no previous checkout to restore on a failure path.

When the Run ends the worktree SHALL be removed and the branch SHALL remain in the configured
folder's repository, where its owner reaches it with ordinary `git`.

Runs with locus Local SHALL NOT be serialised against one another; the project concurrency cap
(BR-002) is the only bound on how many execute at once, as it is for sandboxed Runs.

Where the checkout cannot be created — the configured path is not a git repository, or `git` refuses
— the Run SHALL be refused before anything is written, naming the folder and the reason. Nothing
retries (BR-004).

#### Scenario: a dirty folder no longer refuses the Run

- **WHEN** a Local Run is dispatched for a Story whose Connector folder has uncommitted changes
- **THEN** the Run is accepted and executes, and no clean-tree refusal is raised at dispatch or at
  execution

#### Scenario: the owner's folder is untouched

- **WHEN** a Local Run ends, in any terminal state
- **THEN** the configured folder is on the same branch, at the same `HEAD`, with the same
  uncommitted changes it carried before the Run started

#### Scenario: a checkout yields a committed branch and then goes away

- **WHEN** a Local Run for Story 51 executes and the Agent edits files
- **THEN** a branch `ai/51-…` exists in the configured folder's repository containing those commits,
  nothing was pushed, the Run records the branch name and the checkout it worked in, and the checkout
  no longer exists

#### Scenario: two Local Runs execute at once

- **WHEN** two Local Runs for different Stories on the same configured folder are dispatched within
  the project cap
- **THEN** both execute concurrently, each in its own checkout, and neither observes the other's file
  changes

#### Scenario: an unusable folder is refused by name

- **WHEN** a Local Run is dispatched for a configured folder that is not a git repository, or where
  the worktree cannot be created
- **THEN** the Run is refused before any write, and the refusal names the folder and the specific
  reason rather than reporting a generic failure

### Requirement: checkouts abandoned by a dead process are reaped

At startup the product SHALL remove the Local-Run checkouts it created that no live Run owns, and
prune the repository's record of them. Branches those checkouts produced SHALL NOT be removed — the
branch is the Run's output and outlives the checkout by design.

The sweep exists because the measured failure mode is already on record for the sandbox substrate:
31 abandoned sandboxes and 125 GB, because a process died before its `finally` ran. A checkout leaks
the same way and for the same reason.

#### Scenario: a checkout left by a dead process is removed

- **WHEN** the product starts and finds a checkout it created for a Run that is not executing
- **THEN** the checkout is removed and the repository's worktree record is pruned

#### Scenario: reaping never destroys a Run's output

- **WHEN** an abandoned checkout carrying a committed `ai/{story}-{slug}` branch is reaped
- **THEN** the branch still exists in the configured folder's repository afterwards

#### Scenario: a live Run's checkout survives the sweep

- **WHEN** the sweep runs while a Local Run is executing
- **THEN** that Run's checkout is left alone and the Run continues

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

### Requirement: the code source may be set when the Project is created

Where a Project is created naming a folder on this machine, the resulting Connector SHALL carry the
`LocalFolder` code source with that folder's absolute path, without the Admin visiting the Connector
form. The path SHALL be subject to the same inspection the Connector form's own validation performs,
through the same `ILocalCodeWorkspace` seam, so the two cannot disagree about what a usable folder
is.

Everything the code source already governs SHALL hold unchanged for a Connector configured this way:
BR-016's checkout rules, the no-push and no-pull-request output, the reaping of abandoned checkouts,
and the habitat refusal where the Local locus is declared unavailable. This requirement changes
**when** the code source may be set, never **what** it does.

#### Scenario: a created Project already has its code source

- **WHEN** a Project is created naming a folder that is a git repository
- **THEN** its Connector carries the `LocalFolder` code source and that folder's path, and no
  Connector-form visit was required

#### Scenario: a Run on such a Connector behaves as any Local Run

- **WHEN** a Run is dispatched for a Project whose code source was set at creation
- **THEN** it works in its own checkout, leaves a branch, pushes nothing and opens no pull request,
  exactly as BR-016 already requires

#### Scenario: a declaring habitat still refuses

- **WHEN** a Project is created naming a folder in a habitat that declares the Local locus
  unavailable
- **THEN** the creation is refused carrying the declared reason verbatim, and no Project gains a
  local path

