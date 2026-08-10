## ADDED Requirements

### Requirement: how a human supplies input to a waiting Run depends on the habitat, and the spec says which

For a Run in `AwaitingInput`, the route a human uses to supply the answer SHALL be determined by the
habitat the Run executes in, and both routes SHALL be stated here rather than inferred from an ADR:

- **Self-host.** A human MAY attach to a live agent session for the exchange — either to the agent's
  own process, or to a shell beside it in the Run's sandbox — bounded as the requirement below
  demands. The message route remains available and is the only route once the machine is reclaimed.
- **Deployed.** A human SHALL supply the answer as **a message costing exactly one agent pass**,
  delivered through the Connector and picked up by the existing resume path. No session SHALL be held
  for a human.

Before this, only the message route was specified and the absence of a session was implied by
[ADR-0008](../../../../docs/adr/0008-a-live-conversation-costs-a-pass-per-message.md) — so a reader of
the spec could not learn that a session was disallowed, and every proposal reopened the question. The
rule now lives in the spec, and [ADR-0021](../../../../docs/adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
carries the reasoning.

#### Scenario: a reader asks whether a human may attach to a waiting Run

- **WHEN** a contributor reads `run-orchestration` to learn how a human answers a waiting Run
- **THEN** the spec answers for both habitats from its own text, and names the ADR that decided it
- **AND** the reader does not have to infer the deployed refusal from silence

#### Scenario: a deployed Run waits for an answer

- **WHEN** a Run executing in a deployment enters `AwaitingInput`
- **THEN** no session is held for the human, and the answer arrives as a message costing one pass
- **AND** the Run resumes through the existing resume path

#### Scenario: a self-host Run waits for an answer

- **WHEN** a Run executing in self-host enters `AwaitingInput` and a human attaches
- **THEN** the exchange happens in the attached session for as long as the machine is held
- **AND** once the machine is reclaimed for inactivity, the message route is how the exchange continues

### Requirement: a bound on a human's exchange times the machine, never the person

Where a habitat holds any resource open while a human thinks, the resource's bound SHALL be expressed
as **inactivity of the resource**, and SHALL NOT be expressed as a deadline on the human's reply. Time
a Run spends `AwaitingApproval` or `AwaitingInput` SHALL continue to count toward no deadline
(BR-006). A reclaimed resource SHALL NOT terminate the Run's wait: the human SHALL be able to return
and continue, at the cost of whatever the reclaim discarded.

This is the pillar the live-session question turned on, and the one part of ADR-0008's reasoning that
no later decision has weakened. DEC-061 already demonstrates a conforming bound — a conversation
container reclaimed after ten minutes of inactivity, with the conversation itself outliving it.

#### Scenario: a human takes a week to answer

- **WHEN** a Run is waiting on a human who does not answer for days
- **THEN** the Run remains in its untimed wait and reaches no terminal state on account of the delay
- **AND** any machine held for the exchange has been reclaimed by its own inactivity bound

#### Scenario: a bound is proposed as a reply deadline

- **WHEN** a shape proposes to bound the exchange by how long the human may take
- **THEN** it is refused as a violation of BR-006, whatever cost it would have saved

### Requirement: a Run whose agent was attached to declares that its record is different

Where a human attaches to the agent's own process, the agent no longer emits the structured output the
Run's Output is rendered from, and the Run's record SHALL say so rather than appearing to be a
transcript that failed to parse. A Run whose agent ran attached SHALL be distinguishable, on its own
detail, from one that ran headless.

Stated because the failure mode is silent: `transcript.ts` keeps every line as `raw` when a line is
not JSON, so an attached Run renders as a wall of unparsed text that looks exactly like a broken
transcript. It is not broken — it is a different kind of record, and only this habitat produces it
(DEC-065).

#### Scenario: an attached Run is read afterwards

- **WHEN** a Member opens a Run whose agent was attached to in self-host
- **THEN** its detail states that the record is a terminal stream rather than a structured transcript
- **AND** the reader is not left to conclude the transcript failed

#### Scenario: the same Automation runs in both habitats

- **WHEN** one Automation's Runs execute in self-host with an attached agent and in a deployment
- **THEN** each Run's record is readable on its own terms and says which kind it is
