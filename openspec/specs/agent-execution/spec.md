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

Run execution SHALL select the `IAgentRuntime` implementation — and its credential secret
name, which MAY be absent — from the Automation's `Runtime` value through a selector seam.
A runtime whose credential name is absent SHALL execute with no resolved credential (free
providers); a runtime naming a credential keeps the resolve-by-name path (BR-010). Adding a
runtime SHALL be a composition change, never an executor edit.

#### Scenario: two runtimes, two paths

- **WHEN** two Automations differ only in runtime and their Runs execute
- **THEN** each Run is executed by its runtime's implementation

#### Scenario: a free-model runtime needs no vault entry

- **WHEN** an OpenCode-runtime Run executes with no credential secret configured
- **THEN** the Run proceeds — no vault lookup occurs and no failure is manufactured

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

### Requirement: every catalogue action executes

Run execution SHALL dispatch on the Automation's action, and all four of DEC-026's actions SHALL
execute: implement-to-pull-request (unchanged), refine-or-comment (the Agent's answer posted as
a Story comment), transition-state (the Agent's proposed state written through the seam), and
estimate (an `estimate:<n>` label replacing any prior one, plus the reasoning as a comment).
Only implement-to-pull-request SHALL prepare a workspace — the others touch no code. An
estimate whose answer carries no number, and a transition whose state the vendor rejects, SHALL
fail the Run with that reason rather than guessing.

#### Scenario: the Agent comments

- **WHEN** a refine-or-comment Run executes
- **THEN** the Agent's answer is a comment on the Story and the Run succeeds

#### Scenario: the Agent transitions

- **WHEN** a transition-state Run executes and the Agent names an acceptable state
- **THEN** the Story's state changes and the Run succeeds

#### Scenario: the Agent estimates

- **WHEN** an estimate Run executes
- **THEN** the Story carries exactly one `estimate:<n>` label and a comment with the reasoning

#### Scenario: an unusable answer fails honestly

- **WHEN** an estimate answer carries no number, or a transition names a state the vendor
  rejects
- **THEN** the Run fails with that reason and the Story is unchanged

#### Scenario: only the PR action clones

- **WHEN** any action other than implement-to-pull-request executes
- **THEN** no workspace is prepared and nothing is published

### Requirement: the grill action interrogates a Story to its project's readiness bar

A `GrillToReady` Run SHALL evaluate the Story's current body and its conversation so far against
a readiness document read live from the connected repository. When criteria are unmet, the pass
SHALL end by asking — the specific unmet criteria, posted through the conversational wait — and
questions SHALL NOT repeat what the conversation has already settled. When the bar is met, the
Run SHALL apply the configured ready label through the ordinary label write and post a verdict
comment naming the criteria, then succeed. A missing readiness document SHALL fail the Run naming
the configured path, before any comment or label is written.

#### Scenario: gaps become questions

- **WHEN** a grilled Story is missing criteria the rubric demands
- **THEN** the Run's comment names them specifically and the Run waits for input

#### Scenario: answers close the gaps

- **WHEN** the resumed pass finds the conversation satisfies the rubric
- **THEN** the ready label is applied, the verdict comment names the criteria, and the Run
  succeeds

#### Scenario: already ready

- **WHEN** a Story that meets the bar is grilled
- **THEN** the first pass marks it ready with no questions asked

#### Scenario: no rubric, no grill

- **WHEN** the configured rubric path does not exist in the repository
- **THEN** the Run fails naming that path, and the Story is untouched

#### Scenario: the chain is ordinary matching

- **WHEN** the ready label is another enabled Automation's trigger
- **THEN** that Automation triggers through reconciliation and matching, with no dedicated
  chaining code

### Requirement: the propose action turns a ready Story into a documentation PR

A `ProposeSpec` Run SHALL produce a pull request containing only documentation — the proposal
for the Story — through the same publishing pipeline as implementation, and record it as the
Run's output. It SHALL follow the repository's own declared conventions for such documents,
defaulting to a proposals directory. A Story with no body SHALL fail before any workspace is
prepared, stating there is nothing to propose from. A Story whose linked change already exists
SHALL fail naming that change rather than opening a second.

#### Scenario: a ready story becomes a proposal PR

- **WHEN** a propose Run executes against a Story with a body
- **THEN** a pull request with the proposal exists, linked as the Run's output

#### Scenario: nothing to propose from

- **WHEN** the Story has no body
- **THEN** the Run fails saying so, and no workspace was prepared

#### Scenario: one open change per Story

- **WHEN** the Story already has a linked change
- **THEN** the Run fails naming it, and no second pull request exists

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

