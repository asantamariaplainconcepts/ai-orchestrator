## ADDED Requirements

### Requirement: the spec-first tier arrives as one gated chain

The spec-first tier's wiring SHALL form a single linear chain: grill hands to propose, propose to
implement, implement to sync, each by an output label equal to the next step's trigger. The steps
that execute against a repository — propose, implement and sync — SHALL require approval, so every
automatic hand-off stops in the Inbox for a person to approve a plan before anything executes; the
chain's human waits are the gates, not breaks. `refine` and `status` SHALL carry no output labels
and no step SHALL hand to them: one is an occasional post-merge append and the other a query, and
wiring either into the chain would run it on every pass.

This SHALL be catalogue content (the manifest's `automation` blocks), never code, and it applies to
what setup creates from now on: an existing project's Automations are skipped by setup as always
and their labels SHALL NOT be modified by this wiring.

#### Scenario: the created chain is stored on the Automations

- **WHEN** a fresh project's consented setup completes with every step selected
- **THEN** grill carries `ai:propose`, propose carries `ai:implement`, implement carries `ai:sync`,
  and sync, refine and status carry no output labels

#### Scenario: the tab draws one chain with three gates

- **WHEN** the Admin opens the Automations tab after that setup
- **THEN** the workflow draws grill, propose, implement and sync as one chain with approval gates
  on propose, implement and sync, and refine and status appear as standalone

#### Scenario: excluding a mid-chain step marks what it fed

- **WHEN** the Admin unchecks propose in the setup plan while implement stays selected
- **THEN** implement is marked as losing its hand-off, and nothing blocks the build

#### Scenario: an existing project's labels survive setup

- **WHEN** setup runs on a project whose spec-first Automations already exist without output labels
- **THEN** those Automations are skipped and their output labels are unchanged
