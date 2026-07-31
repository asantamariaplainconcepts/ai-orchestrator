# agent-execution

## ADDED Requirements

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

## REMOVED Requirements

### Requirement: every catalogue action executes

**Reason:** the catalogue is gone (#162). Its actions were the orchestrator performing work on the
agent's behalf — opening the pull request, writing the comment, transitioning the state, parsing the
estimate — which is exactly what this change removes. What each of them did is now what a prompt does.

**Migration:** Automations naming a removed action are deleted by the migration; nothing is in
production. The use cases they served (UC-016, UC-018, UC-024, UC-025) stay realisable through
prompts, and the requirement that replaces this one says what the orchestrator does instead: clone,
run, and stay out of the way.

### Requirement: the grill action interrogates a Story to its project's readiness bar

**Reason:** removed with the catalogue (#162). A grill is a prompt — it is one in this repository
already — and keeping a code path that reads a rubric, judges readiness and asks questions would be
the clearest possible example of the orchestrator doing an agent's job.

**Migration:** the readiness bar moves into a prompt file in the project's repository. **Note the
dormancy this leaves:** the grill's question path was the only producer of the `AwaitingInput` Run
state, so after this nothing reaches it and nothing enters the inbox's waiting-for-input category.
The state and its machinery are kept deliberately — changing Run states is out of scope for #162 —
and a prompt can ask a question by commenting, but cannot pause its own Run and resume.

### Requirement: the propose action turns a ready Story into a documentation PR

**Reason:** removed with the catalogue (#162), for the same reason as the grill: the ceremony —
compose the document, branch, commit, open the pull request — is work a prompt does with the
credential and the workspace it already holds.

**Migration:** the behaviour moves into a prompt file. Past Runs that used it render unchanged.
