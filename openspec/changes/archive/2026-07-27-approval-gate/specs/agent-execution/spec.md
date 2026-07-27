# agent-execution

## ADDED Requirements

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
