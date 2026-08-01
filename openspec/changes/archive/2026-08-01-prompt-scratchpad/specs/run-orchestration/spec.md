# run-orchestration

## ADDED Requirements

### Requirement: an Admin tries a prompt before committing it

An Admin SHALL be able to supply prompt text in the portal and run it once against the project's
repository, reading the reply and what it cost, without committing a file, configuring an Automation,
or applying a trigger label.

Each attempt SHALL be an independent trial: an edited draft SHALL reach an agent that has not seen
the previous draft or its reply, so the result of trying predicts the result of running rather than
continuing a discussion about the earlier attempt.

The text supplied SHALL NOT be stored anywhere the Run path reads. No Automation SHALL be written or
changed by an attempt, and a Run executing afterwards SHALL resolve its prompt from the repository
exactly as it would have otherwise. The surface SHALL say plainly that the text is not saved, and
SHALL name where it belongs when it is right — the project's prompts directory.

Trying a prompt SHALL require the same permission as holding a conversation, scoped to the project.
A caller with no role on the project SHALL be refused, disclosing nothing about it, and a Member
SHALL be allowed: an attempt spends exactly what a conversation message spends, and gating it more
tightly would be an inconsistency with no argument behind it.

An attempt SHALL NOT be a Run. It SHALL create no Run, occupy no cap slot and lock no Story, and an
Automation whose trigger is applied to a Story with an attempt in flight SHALL match and run as
usual.

#### Scenario: a prompt is tried without being committed

- **WHEN** an Admin supplies prompt text for a project with a Connector and runs it
- **THEN** exactly one agent pass runs against that project's repository and the reply is readable

#### Scenario: an edited draft is tried afresh

- **WHEN** an Admin edits the text and runs it again
- **THEN** the agent is given the edited text without the previous attempt or its reply

#### Scenario: trying changes no configuration

- **WHEN** an attempt has run
- **THEN** no Automation has been written or changed, and a Run afterwards resolves its prompt from
  the repository

#### Scenario: an attempt occupies nothing

- **WHEN** a trigger label is applied to a Story that has an attempt in flight
- **THEN** the Automation matches and its Run starts as usual

#### Scenario: trying is refused to a caller with no role

- **WHEN** a caller with no role on a project tries a prompt against it
- **THEN** it is refused for permission, disclosing nothing about the project

### Requirement: a trial is faithful to a Run, and says where it is not

The text an Admin supplies SHALL reach the agent framed as a Run frames the repository's prompt: the
prompt text, then the named Story's description in the same form a Run uses.

Where a trial cannot reproduce a Run, the surface SHALL name the difference rather than leave it to
be discovered from a divergent result. The differences SHALL be: an approval-gated Automation runs
its prompt in a planning phase whose framing a trial does not reproduce, and a per-Automation timeout
belongs to an Automation a trial does not have.

#### Scenario: a trial naming a Story is framed as a Run would frame it

- **WHEN** an attempt names a Story
- **THEN** the agent receives the prompt text followed by that Story's description in the same form a
  Run supplies

#### Scenario: naming no Story is ordinary

- **WHEN** an attempt names no Story
- **THEN** the pass runs against the project alone and is not treated as incomplete

## MODIFIED Requirements

### Requirement: a message costs exactly one agent pass, and the spend is visible

Sending a message SHALL run exactly one agent pass. The reply SHALL be readable in the portal, and
the pass's usage and cost SHALL be recorded against the conversation.

A pass whose usage the runtime did not report SHALL read **unknown**, never zero (BR-011). A
conversation whose total includes an unmeasured pass SHALL NOT present that total as exact. This
holds for an attempt at a prompt as for any other message: money that was spent SHALL be recorded on
the row that spent it, and SHALL NOT vanish because the text was a draft.

The agent SHALL be given the project's repository, cloned with the project's credential, so answers
are grounded in the code and not only in the mirror. Where a conversation names a Story, the agent
SHALL additionally be given that Story's context read from the mirror, as any other read is (BR-008).
A conversation naming no Story SHALL cause no vendor write on any Story.

**A Story SHALL be described to an agent in one way, whichever path supplies it.** The description
SHALL carry the Story's number, title, state, labels and description, and the description SHALL be
bounded, so that a prompt tried in a conversation and then run by an Automation is given the same
input rather than two framings that differ in what a prompt may branch on.

A message SHALL be bounded, and the bound SHALL admit the prompts this product exists to author: it
SHALL be large enough that a real prompt file is accepted rather than refused as a validation error
on somebody's paste.

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

#### Scenario: both paths describe a Story identically

- **WHEN** the same Story is supplied to an agent by a Run and by a conversation
- **THEN** the description is the same in both, carrying its number, state and labels, and bounded in
  both

#### Scenario: a real prompt is not refused for its length

- **WHEN** a message the length of a real prompt file is sent
- **THEN** it is accepted, and a message beyond the bound is still refused
