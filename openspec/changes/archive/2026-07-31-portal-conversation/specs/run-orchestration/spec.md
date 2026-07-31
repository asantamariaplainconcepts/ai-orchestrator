# run-orchestration

## ADDED Requirements

### Requirement: a Member holds a conversation with an agent without occupying a Run

A Member SHALL be able to hold a conversation with an agent from the portal. A conversation SHALL
belong to a project and MAY name one of that project's Stories as its subject; naming none SHALL be
an ordinary case and not a degraded one.

A conversation SHALL NOT be a Run. It SHALL occupy no cap slot, hold no lock on any Story, and block
nothing: an Automation whose trigger is applied to a Story with an open conversation SHALL match and
run exactly as it would otherwise. This is what keeps BR-001 and BR-014 untouched — waiting blocks a
Story because a Run occupies it, and a conversation that occupied one would stop every Automation on
that Story for as long as somebody kept talking.

Holding a conversation SHALL require a permission the Member bundle holds, scoped to the project, so
a caller with no role on a project cannot open one about it.

#### Scenario: a conversation exists without a Run

- **WHEN** a Member starts a conversation on a project
- **THEN** it exists with no Run created, optionally naming a Story and otherwise naming none

#### Scenario: an open conversation blocks nothing

- **WHEN** a trigger label is applied to a Story that has an open conversation
- **THEN** the Automation matches and its Run starts as usual

#### Scenario: a conversation belongs to a project a caller may see

- **WHEN** a caller with no role on a project tries to start or read a conversation about it
- **THEN** it is refused for permission, disclosing nothing about the project

### Requirement: a message costs exactly one agent pass, and the spend is visible

Sending a message SHALL run exactly one agent pass. The reply SHALL be readable in the portal, and
the pass's usage and cost SHALL be recorded against the conversation.

A pass whose usage the runtime did not report SHALL read **unknown**, never zero (BR-011). A
conversation whose total includes an unmeasured pass SHALL NOT present that total as exact.

The agent SHALL be given the project's repository, cloned with the project's credential, so answers
are grounded in the code and not only in the mirror. Where a conversation names a Story, the agent
SHALL additionally be given that Story's context read from the mirror, as any other read is (BR-008).
A conversation naming no Story SHALL cause no vendor write on any Story.

A conversation SHALL leave no comment or other trace on the Story at the vendor.

#### Scenario: one message, one pass

- **WHEN** a Member sends a message
- **THEN** exactly one agent pass runs and its reply is readable

#### Scenario: what a pass cost is recorded against the conversation

- **WHEN** a pass reports its usage
- **THEN** the conversation's spend includes it and is readable

#### Scenario: unmeasured is not free

- **WHEN** a pass reports no usage
- **THEN** that message reads unknown, and the conversation's total does not claim to be exact

#### Scenario: a conversation about nothing writes nothing

- **WHEN** a conversation naming no Story exchanges messages
- **THEN** no vendor write happens on any Story

#### Scenario: a conversation about a Story still writes nothing

- **WHEN** a conversation naming a Story exchanges messages
- **THEN** the Story at the vendor is unchanged, and the agent was given that Story's context

### Requirement: a failed message leaves the conversation open

A pass that fails SHALL be shown as a failure on the message that caused it, and the conversation
SHALL stay open. A failed message SHALL NOT be a failed conversation, and SHALL NOT prevent the next
message.

#### Scenario: a failure is a message, not an ending

- **WHEN** an agent pass fails
- **THEN** the conversation shows the failure, stays open, and accepts another message

### Requirement: a conversation's agent runs where the habitat provides one

The agent pass SHALL run behind a runtime seam, so the module that owns conversations does not know
where the agent executes.

Where the habitat provides an on-demand session host, a conversation SHALL get **its own** container
for as long as it is being used, carrying the cloned workspace and the project's credential, and the
host SHALL reclaim it after inactivity. One conversation SHALL map to one container, so isolation
coincides with the credential boundary (DEC-030) and no project's credential is visible to another.

Where the habitat provides none — a machine one person owns, or a deployment with no session host —
the pass SHALL run in process, and the conversation SHALL behave identically. Which habitat this is
SHALL be decided by configuration and never inferred (ADR-0010).

#### Scenario: a session belongs to one conversation

- **WHEN** two conversations on different projects are active at once
- **THEN** each runs in its own container with only its own project's credential

#### Scenario: no session host is not a broken conversation

- **WHEN** the habitat provides no session host
- **THEN** conversations work, with the pass running in process
