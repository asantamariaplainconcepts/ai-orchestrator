# agent-execution

## ADDED Requirements

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
