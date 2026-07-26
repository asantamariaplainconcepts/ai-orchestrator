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

