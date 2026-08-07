# agent-execution Specification

## Purpose
TBD - created by archiving change agent-runtime-seam. Update Purpose after archive.
## Requirements
### Requirement: a dispatched Run is executed through the runtime seam

The worker SHALL claim a Run id from the queue, load the Run, its Story and its Automation
through the module surfaces, mark the Run `Executing`, and invoke `IAgentRuntime` with an
instruction built in-process — prompt, action, timeout, and credentials resolved **by name**
at execution time (BR-010, DEC-014, DEC-030). No secret value SHALL appear in the queue
message, the database, logs, or container configuration at rest. The runtime's result SHALL
end the Run: `Succeeded` or `Failed`, with timestamps (BR-014).

#### Scenario: the contract round-trips in the job

- **WHEN** the worker claims a dispatched Run and the runtime returns a result
- **THEN** the Run reaches a terminal state with its timestamps, and the recorded outcome
  came through the seam — no vendor type outside the implementation

#### Scenario: a missing Run is a no-op

- **WHEN** the claimed id matches no Run (deleted, or a foreign message)
- **THEN** the worker logs and continues — the message was already deleted (BR-004), nothing
  retries

### Requirement: usage is reported at run end, and absence is unknown, never failure

The runtime SHALL report tokens and cost at run end when its output carries them (BR-011,
DEC-038), persisted on the Run. A missing or unparseable usage block SHALL yield an unknown
usage on a Run that otherwise succeeds — degradation is to honesty, not to error.

#### Scenario: usage present

- **WHEN** the runtime's result carries usage and cost
- **THEN** the Run records them

#### Scenario: usage absent

- **WHEN** the result carries no readable usage
- **THEN** the Run's usage reads unknown and the Run's outcome is unaffected

### Requirement: a terminal Run frees its Story

`Succeeded` and `Failed` SHALL be terminal states excluded from BR-001's active-state index
filter: a Story whose Run has ended can match or be run again, and a `Failed` Run stays
terminal — re-running it is a human act (BR-004), never automatic.

#### Scenario: run again after the end

- **WHEN** a Story's only Run is terminal and a matching event or Run now arrives
- **THEN** a new Run is created — BR-001 constrains active Runs only

### Requirement: an ImplementToPullRequest Run produces a linked pull request

For a Run whose Automation action is `ImplementToPullRequest`, execution SHALL: prepare a
workspace by cloning the project repository with the PAT on a run-scoped branch; run the Agent
in it; and, when the Agent succeeded and produced changes, commit, push and open a pull
request whose body references the Story — recording the PR URL on the Run (BR-014) and
exposing it through the runs API and the Runs table's Output column. Clone, commit, push and
PR SHALL be deterministic code behind a workspace seam — never Agent instructions. The PAT
SHALL exist only in memory and in no persisted remote configuration, log, or Run field
(BR-010, DEC-030).

#### Scenario: label to pull request

- **WHEN** a single-phase ImplementToPullRequest Run executes and the Agent changes files
- **THEN** the Run ends Succeeded with the new PR's URL as its output link, and the PR's
  branch is scoped to the Run

#### Scenario: no changes is a failure with its reason

- **WHEN** the Agent succeeds but the workspace has no changes
- **THEN** the Run ends Failed stating the Agent produced no changes — no PR is opened

#### Scenario: each stage refuses distinctly

- **WHEN** the clone, the Agent, the push, or the PR call fails
- **THEN** the Run's failure reason names that stage — four failures, four reasons

#### Scenario: only the executable action executes

- **WHEN** a Run's Automation action is not ImplementToPullRequest
- **THEN** the Run ends Failed stating the action is not executable yet

### Requirement: the phase timeout ends the Run

A runtime execution exceeding the Automation's timeout SHALL be killed and the Run marked
`Failed` naming the limit (BR-005). Queued and approval waits do not count (BR-006) — the
timeout clock is the runtime invocation only.

#### Scenario: the agent overruns

- **WHEN** the runtime exceeds the Automation's timeout
- **THEN** the process is killed and the Run ends Failed naming the timeout

### Requirement: the Automation's runtime decides which agent executes

Run execution SHALL select the `IAgentRuntime` implementation through a selector seam, resolving
the runtime name in a stated order: the human's per-Run choice recorded on the Run, then the
Automation's explicit runtime, then the Project default, then the deployment default. The
credential secret name resolves the same way one level down: the Project's name for that runtime,
then the deployment's, then none. A runtime whose resolved credential name is absent SHALL execute
with no resolved credential (free providers, DEC-044); a runtime naming a credential keeps the
resolve-by-name path (BR-010). Adding a runtime SHALL be a composition change, never an executor
edit.

The transcript SHALL name which source the credential came from — project, deployment, or none —
because a Run billed to the wrong key must be diagnosable from its own record.

**An absent credential SHALL NOT shadow a host identity.** A runtime process environment SHALL
only carry a credential variable when there is a non-empty value to carry: exporting an empty
`GITHUB_TOKEN` or API key overrides whatever auth the host's own tooling holds, which is exactly
the Local lane's working state (#210).

#### Scenario: two runtimes, two paths

- **WHEN** two Automations differ only in runtime and their Runs execute
- **THEN** each Run is executed by its runtime's implementation

#### Scenario: a free-model runtime needs no vault entry

- **WHEN** an OpenCode-runtime Run executes with no credential secret configured
- **THEN** the Run proceeds — no vault lookup occurs and no failure is manufactured

#### Scenario: the chain resolves in order

- **WHEN** a Run carries a per-Run choice, its Automation names a runtime, and the Project has a
  default
- **THEN** the per-Run choice wins; absent it, the Automation's; absent both, the Project's;
  absent all three, the deployment default

#### Scenario: the project credential outranks the deployment's

- **WHEN** a Project names a credential for the resolved runtime and the deployment also has one
- **THEN** the Run resolves the Project's name, and the transcript says the project supplied it

#### Scenario: an empty token does not reach the environment

- **WHEN** a Local Run executes with no vendor credential resolved
- **THEN** the runtime's process environment carries no empty credential variable, and the host's
  own auth remains in effect

### Requirement: opencode usage comes from the observed event stream

The opencode implementation SHALL invoke the pinned CLI headless with JSON event output,
aggregate usage from `step_finish` events (tokens and cost) and take the reply from `text`
events (OPN-004's closure). Unknown event types SHALL be skipped; a stream with no
`step_finish` SHALL yield unknown usage (BR-011); a non-zero exit or empty stream SHALL fail
the Run with the raw output as evidence.

#### Scenario: a free-model run reports its usage

- **WHEN** an opencode Run completes normally
- **THEN** the Run records the summed tokens and cost (zero cost for free models) and the
  reply text as its log

#### Scenario: shape drift degrades to honesty

- **WHEN** the event stream carries no readable step_finish
- **THEN** the Run's usage reads unknown and its outcome is decided by the exit code alone

### Requirement: the Agent's instruction carries the Story's requirement

The prompt built for a Run SHALL include the Story's mirrored description alongside its title,
state and labels — an Agent asked to implement a Story SHALL NOT be working from a headline
alone. The body SHALL be bounded at the prompt (not at rest) so an unusually long description
cannot turn into an unbounded cost or a timeout surprise.

#### Scenario: the requirement reaches the Agent

- **WHEN** a Run executes for a Story with a description
- **THEN** the instruction handed to the runtime contains that description

#### Scenario: a very long description is bounded

- **WHEN** the description exceeds the prompt's bound
- **THEN** the instruction carries a truncated body and says it was truncated

### Requirement: execution has two phases and each is bounded separately

For an approval-gated Run the runtime SHALL be invoked twice: once to produce a Plan (workspace
prepared, nothing published) and, after approval, once to implement — with the approved Plan in
its instruction. The Automation's timeout SHALL bound each invocation separately (BR-005),
never their sum and never the interval a human spent deciding (BR-006).

#### Scenario: the plan phase sees the code but publishes nothing

- **WHEN** the plan phase runs
- **THEN** the Agent worked in a prepared workspace and no commit, push or pull request happened

#### Scenario: the approved Plan reaches the implementer

- **WHEN** the execution phase runs after approval
- **THEN** the instruction handed to the runtime contains the approved Plan

### Requirement: a Run's output is observable while it executes

Agent output SHALL be persisted incrementally while a Run executes, and readable through the
Run's log endpoint together with whether the Run has finished. The observed lag from a line
being emitted to it being readable SHALL be at most five seconds. A Run that crashes mid-write
SHALL preserve every line persisted before the crash. A finished Run SHALL serve its full log
from the same read, marked complete. When the log cannot be read, the Run's state SHALL remain
visible and the failure SHALL name itself — never a blank page.

This SHALL hold for **every** runtime, including the default one. A runtime SHALL NOT be configured to
emit its output as a single document at exit, because a log that arrives only when the work is over is
not observable while the work happens, whatever the read endpoint does.

Where a runtime emits a stream of events rather than one document, its result — success, the reply, and
the usage — SHALL be read from the stream's terminal result event, not by parsing the whole stream as
one document.

A stream carrying **no** terminal result event SHALL fail the Run with the raw streams as evidence,
because the product cannot then say what the agent did, and the reply of a simple action becomes a
comment on somebody's Story — a Run reported as successful whose reply is raw stream text would publish
that. A missing **usage block inside** a result event SHALL remain unknown on an otherwise successful
Run: only the absent result event is a broken contract.

#### Scenario: the log grows during execution

- **WHEN** a Run executes and the runtime emits output
- **THEN** the log read returns the lines so far, within the stated lag

#### Scenario: a crash preserves the partial log

- **WHEN** the runtime dies mid-run
- **THEN** the lines persisted before the crash remain readable

#### Scenario: finished means complete

- **WHEN** a terminal Run's log is read
- **THEN** the full output is returned and the response says it is complete

#### Scenario: unreadable log, visible Run

- **WHEN** the log store cannot serve
- **THEN** the Run's state still renders and the log area names the failure

#### Scenario: the default runtime is not silent until it exits

- **WHEN** a Run whose runtime is the default one executes
- **THEN** lines are readable while it is still executing, not only after the process ends

#### Scenario: a streamed result is still a result

- **WHEN** a runtime's output is a stream of events and the Run succeeds
- **THEN** the reply and the usage come from the terminal result event, and the Run is recorded as
  succeeded

#### Scenario: no terminal event is a broken contract

- **WHEN** no terminal result event can be read from a stream, even though the process exited
  successfully
- **THEN** the Run fails with the raw streams as evidence, rather than being reported as succeeded with
  stream text as its reply

#### Scenario: a result event with no usage block

- **WHEN** a terminal result event carries a reply but no readable usage
- **THEN** the Run succeeds with that reply and its usage reads unknown

### Requirement: a SyncChange Run closes the Story's change as the repository says to

A Run whose Automation's action is SyncChange SHALL close the Story's open change by following a
close-out procedure read from the connected repository, at a configurable path defaulting to the
framework's convention. The action SHALL NOT contain any procedure of its own. Before any
workspace is prepared, the Run SHALL refuse when the Story has no open change, and when the
procedure document cannot be read, naming the path it looked for. A failing Run SHALL leave the
pull request exactly as it found it, and SHALL record why it stopped.

#### Scenario: the change is closed as the repository describes

- **WHEN** a Story has an open change and its repository carries the procedure document
- **THEN** the agent follows that document, the Run succeeds, and it records what it closed

#### Scenario: nothing to close

- **WHEN** the Story has no open change
- **THEN** the Run fails with that reason, before any workspace is prepared

#### Scenario: no procedure to follow

- **WHEN** the procedure document is absent at the configured or default path
- **THEN** the Run fails naming the path it looked for, before any workspace is prepared

#### Scenario: the project's own procedure is used

- **WHEN** the Automation names a document path
- **THEN** exactly that document is read, and no other

#### Scenario: a failed close changes nothing

- **WHEN** a SyncChange Run fails
- **THEN** the pull request is as it was, and the Run states why

### Requirement: an Automation may take its prompt from the repository

An Automation SHALL be able to name a markdown file in the connected repository as its action, and a
Run of that Automation SHALL use the file's content as the agent's instruction. The file SHALL be read
live at execution time and SHALL NOT be mirrored or cached, so the repository remains the only copy.

The Automation SHALL store the file's name and the project SHALL supply the directory it resolves
against, as `connector-configuration` requires. Subfolders SHALL be allowed within that directory.

Leading YAML frontmatter SHALL be stripped and ignored. That block is how another runner is told what
to do with the file, while this product's wiring is the Automation itself — its runtime, timeout,
approval gate and trigger. Ignoring it SHALL be deliberate: a declared model SHALL NOT choose what
this product spends, and a declared tool list SHALL NOT grant powers the Automation did not give.

The write surface SHALL be one comment on the Story and nothing else: no label, no state, no
workspace, and no pull request. A repository prompt SHALL NOT be able to widen its own surface by
asking to.

Both refusals SHALL precede the agent, each naming the **resolved** path — directory and name
together, so a misconfigured directory gives itself away: a file that cannot be read, and a file whose
body is empty once frontmatter is stripped. There SHALL be no fallback prompt and no substituted
catalogue action — an Automation configured to run the repository's prompt SHALL either run it or stop.

Usage, cost and streamed output SHALL behave as on any other Run.

#### Scenario: the repository's prompt is what the agent receives

- **WHEN** a Run executes an Automation naming a markdown file that exists
- **THEN** the file's body is the agent's instruction, alongside the Story's context

#### Scenario: frontmatter is not part of the prompt

- **WHEN** the named file begins with a YAML frontmatter block
- **THEN** that block does not reach the agent, and the body after it does

#### Scenario: the answer becomes a comment

- **WHEN** the agent answers successfully
- **THEN** the answer is posted as a comment on the Story, and no label, state or pull request is
  written

#### Scenario: the file is not there

- **WHEN** the name does not resolve to a readable file in the project's prompts directory
- **THEN** the Run fails naming the resolved path, before any agent runs

#### Scenario: the file says nothing

- **WHEN** the named file's body is empty once frontmatter is stripped
- **THEN** the Run fails naming the path, before any agent runs, rather than sending an empty prompt

#### Scenario: a prompt cannot grant itself powers

- **WHEN** the named file's frontmatter or body asks for tools, a model, or a write the Automation did
  not configure
- **THEN** nothing about the Run's surface changes: one comment, the Automation's runtime, the
  Automation's timeout

### Requirement: an Automation runs the repository's prompt, and the orchestrator writes nothing on its behalf

An Automation SHALL name exactly one action: run the repository's prompt. When a Run executes, the
workspace SHALL be cloned with the project's credential, the prompt SHALL resolve from the project's
prompts directory read live, and the agent SHALL run holding both the project credential and the AI
credential.

The orchestrator SHALL perform **no vendor or repository write of its own** afterwards. Whether a
pull request was opened, a comment written, a state transitioned or an estimate recorded is whatever
the prompt did — the orchestrator SHALL NOT do any of it on the agent's behalf, and SHALL NOT parse
the agent's output looking for something to publish.

Success and failure SHALL come from the agent's own result, the log SHALL stream as on any Run, and
usage SHALL stay honest — unknown remains unknown (BR-011).

The single exception is the workflow's own wiring: on success the orchestrator SHALL still apply the
Automation's output labels (#115/#116). That is machinery, true of every Automation whatever its
prompt says, rather than one action's ceremony.

#### Scenario: the prompt decides what happens

- **WHEN** a Run of an Automation executes
- **THEN** the agent runs against a cloned workspace with the project's credential, and no vendor
  write happens except what the agent itself performed

#### Scenario: nothing is published afterwards

- **WHEN** an agent finishes having produced file changes
- **THEN** the orchestrator opens no pull request and writes no comment — if the prompt did not
  publish, nothing was published

#### Scenario: the hand-off still happens

- **WHEN** a Run succeeds and its Automation names output labels
- **THEN** the orchestrator applies them, as it does for every Automation

#### Scenario: an unknown action is refused

- **WHEN** an Automation is saved naming any action other than the repository prompt
- **THEN** it is refused with the unknown-action refusal

### Requirement: the executor selects the workspace per Run by locus

`RunExecutor` SHALL obtain the Run's workspace through the existing `ICodeWorkspace` seam,
selected by the Run's locus: `Pod` keeps today's fresh-clone workspace unchanged; `Local` uses
the folder workspace. The queue message, dispatch worker and Aspire wiring are unchanged — locus
is a workspace decision inside the worker, never a routing decision (design D1).

#### Scenario: a Pod run is byte-for-byte today's behaviour

- **WHEN** a Run with locus `Pod` executes
- **THEN** the workspace is a fresh shallow clone and every existing agent-execution requirement
  holds without modification

#### Scenario: audit fields extend for Local (BR-014)

- **WHEN** a Local Run reaches a terminal state
- **THEN** the Run row carries its locus, working folder and branch name alongside every
  existing audit field, and none of the existing fields changed meaning

### Requirement: a change-targeted Run is prepared on the change's own head branch

For a Run that targets an open change, the Run ceremony SHALL prepare the workspace on the
change's **existing head branch** — the named-branch checkout the install path already performs —
instead of cutting a fresh `run/<id>` branch. Pushing the work SHALL remain what it already is for
every Run since the publish step was retired (DEC-062): the Agent's own act, performed with the
credential the instruction carries. No workspace publish step SHALL be reintroduced for this —
one retired ceremony must not come back wearing a new name.

The instruction the Agent receives SHALL carry the change's context — its number, title and head
branch — in place of the Story framing a story Run gets, and SHALL carry the Member's ad-hoc text
as the prompt body. An Agent that changes nothing and pushes nothing SHALL leave a succeeded Run
whose record says the change was left untouched — no changes is an answer to an instruction, not
a failure.

#### Scenario: the workspace is the change's branch

- **WHEN** a change-targeted Run is executed
- **THEN** the workspace is prepared on the change's head branch by name, and no `run/<id>`
  branch is created

#### Scenario: the checkout failure names its stage

- **WHEN** the change's head branch no longer exists at prepare time
- **THEN** the Run fails with the ceremony's stage-named checkout reason

#### Scenario: the framing is the change's, not a Story's

- **WHEN** the Agent's instruction is assembled for a change-targeted Run
- **THEN** it carries the change's number, title and head branch and the ad-hoc text, and no
  Story lookup is performed

### Requirement: the agent runtimes are observable where they run

A process that executes Runs SHALL probe each registered agent runtime on a stated cadence —
the runtime's CLI answers, and its configured credential resolves — and SHALL expose the result
beside the pod host's own readiness: state, last-checked time, the probe's cadence, and a
copyable remedy for each not-ready cause. A missing executable and an unresolvable credential
SHALL be distinguished, because their remedies differ. A Run that fails anyway SHALL carry the
same remedy in its failure reason (BR-004: nothing retries, so the failure carries everything):
a missing executable names the binary, that PATH resolution failed, and the install command; a
missing secret names the secret and the store to add it to — never a value (BR-010).

A runtime whose credential configuration is empty or whitespace SHALL be treated as having no
credential requirement, identically across runtimes: nothing is resolved, no credential
variable is exported to the agent process, and the CLI runs with the machine's own session —
the same session the pod default already mounts deliberately.

**Where the agent executes in a sandbox, readiness SHALL describe the machine the CLI actually
runs on, never this process's own binaries.** The probe SHALL report the sandbox host's own
preconditions — the sandbox service reachable, and whatever else it requires before a sandbox
can be created — each with its own remedy, and SHALL report a runtime's CLI readiness from
where that CLI will run. A probe that cannot reach the sandbox host SHALL say so rather than
answering from this process, because "ready here" is not an answer about a Run that will execute
elsewhere.

#### Scenario: a missing CLI is visible before any Run

- **WHEN** a registered runtime's executable is not on the executing process's PATH
- **THEN** the environment surface shows that runtime not ready, naming the binary and a
  copyable install command at the repository's pinned version, with the last-checked time

#### Scenario: a Run that fails anyway names the remedy

- **WHEN** a Run dispatches to a runtime whose executable cannot start
- **THEN** its failure reason names the binary, that PATH resolution failed, and the install
  remedy — never a raw process error alone

#### Scenario: an unresolvable credential is its own state

- **WHEN** a runtime's configured credential name resolves to no secret
- **THEN** the environment surface and any Run failure name the secret and the store to add it
  to, and no value ever appears

#### Scenario: switched off means the machine's own session

- **WHEN** a runtime's credential configuration is set to empty or whitespace
- **THEN** no secret is resolved, no credential variable reaches the agent process, and a Run
  executes with the machine's own session

#### Scenario: the sandbox host's own preconditions are visible

- **WHEN** Runs execute in sandboxes and the sandbox host is unreachable or unprepared
- **THEN** the environment surface names that precondition and its remedy with the last-checked
  time, distinguished from a missing runtime CLI

#### Scenario: readiness does not answer for the wrong machine

- **WHEN** Runs execute in sandboxes and this process happens to have a runtime's CLI installed
- **THEN** readiness reports the CLI from where Runs will actually run, never reporting ready on
  the strength of this process's own PATH

