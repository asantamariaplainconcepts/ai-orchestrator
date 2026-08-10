## ADDED Requirements

### Requirement: how a human supplies input to a waiting Run is specified, not implied

For a Run in `AwaitingInput`, the specification SHALL state which route a human uses to supply the
answer, and SHALL cite the ADR that decided it. Exactly one of two shapes SHALL be named:

- **a message costing one agent pass** — the answer is delivered as a Story comment through the
  Connector, and the existing resume path starts a new pass; or
- **an attached live session** — a human is connected to a live agent process for the duration of the
  exchange, bounded as the requirement below demands.

Today the first shape is specified and the second is refused only by
[ADR-0008](../../../../docs/adr/0008-a-live-conversation-costs-a-pass-per-message.md). That
asymmetry is the defect: a reader of the spec cannot learn that an attached session is disallowed, so
the question stays open in every future proposal. Whichever shape the decision selects, the spec
SHALL carry the rule in its own text.

#### Scenario: a reader asks whether a human may attach to a waiting Run

- **WHEN** a contributor reads `run-orchestration` to learn how a human answers a waiting Run
- **THEN** the spec states which of the two shapes applies, and names the ADR that decided it
- **AND** the reader does not have to infer the absence of an attached session from silence

#### Scenario: a later change proposes an attached session

- **WHEN** a change proposes connecting a human to a live agent process
- **THEN** the spec's stated rule either licenses it or refuses it outright
- **AND** if it refuses, the proposal is blocked behind a superseding ADR rather than the ambiguity

### Requirement: a bound on a human's exchange times the machine, never the person

Where a shape holds any resource open while a human thinks, the resource's bound SHALL be expressed
as **inactivity of the resource**, and SHALL NOT be expressed as a deadline on the human's reply.
Time a Run spends `AwaitingApproval` or `AwaitingInput` SHALL continue to count toward no deadline
(BR-006). A reclaimed resource SHALL NOT terminate the Run's wait: the human SHALL be able to return
and continue, at the cost of whatever the reclaim discarded.

This is the pillar the live-session question turns on, and it is the one part of ADR-0008's reasoning
that no later decision has weakened. DEC-061 already demonstrates a conforming bound — a conversation
container reclaimed after ten minutes of inactivity, with the conversation itself outliving it.

#### Scenario: a human takes a week to answer

- **WHEN** a Run is waiting on a human who does not answer for days
- **THEN** the Run remains in its untimed wait and reaches no terminal state on account of the delay
- **AND** any machine held for the exchange has been reclaimed by its own inactivity bound

#### Scenario: a bound is proposed as a reply deadline

- **WHEN** a shape proposes to bound the exchange by how long the human may take
- **THEN** it is refused as a violation of BR-006, whatever cost it would have saved
