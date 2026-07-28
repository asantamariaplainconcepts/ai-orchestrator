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

