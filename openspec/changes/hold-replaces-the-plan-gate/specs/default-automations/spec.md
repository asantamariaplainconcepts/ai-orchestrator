## MODIFIED Requirements

### Requirement: the starter catalogue carries default Automation wiring as content

The catalogue's manifest entries MAY carry an `automation` block — trigger label, output labels
(including the hold, where a step stops for a person) — and the manifest-enumeration test SHALL
refuse a wiring that duplicates a trigger within the catalogue. The wiring is content beside the
prompts it belongs to: the product hardcodes no methodology.

The block SHALL NOT carry `requiresApproval`: there is no approval flag to carry. A manifest that
names one SHALL be refused by the same enumeration test, so a stale catalogue cannot quietly ship a
field nothing reads.

The duplicate-trigger refusal SHALL hold with no exception for any tier. A trigger SHALL have exactly
one prompt in the catalogue, so which file a step wires is never a function of which tier a caller
consented to.

#### Scenario: the wiring is enumerable and consistent

- **WHEN** the manifest is enumerated
- **THEN** every `automation` block names a trigger unique within the catalogue, and none names an
  approval flag

### Requirement: the spec-first tier arrives as one gated chain

The spec-first tier's wiring SHALL form a single linear chain: grill hands to propose, propose to
implement, implement to sync, each by an output label equal to the next step's trigger. The steps
that execute against a repository — propose, implement and sync — SHALL **apply the hold** among
their output labels, so every automatic hand-off stops on the Story for a person to review what was
produced before the next step starts; the chain's human waits are the gates, not breaks. `refine`
and `status` SHALL carry no output labels and no step SHALL hand to them: one is an occasional
post-merge append and the other a query, and wiring either into the chain would run it on every
pass.

Where the gate used to stop a Run *before* it acted, the hold stops the *next* step after this one
has acted — the review is of the pull request the step produced, not of a plan it proposed (BR-007
as rewritten, DEC-039 and DEC-040 superseded).

This SHALL be catalogue content (the manifest's `automation` blocks), never code, and it applies to
what setup creates from now on: an existing project's Automations are skipped by setup as always
and their labels SHALL NOT be modified by this wiring.

#### Scenario: the created chain is stored on the Automations

- **WHEN** a fresh project's consented setup completes with every step selected
- **THEN** grill carries `ai:propose`, propose carries `ai:implement` **and the hold**, implement
  carries `ai:sync` **and the hold**, sync carries the hold, and refine and status carry no output
  labels

#### Scenario: the tab draws one chain with three holds

- **WHEN** the Admin opens the Automations tab after that setup
- **THEN** the workflow draws grill, propose, implement and sync as one chain with a hold after
  propose, implement and sync, and refine and status appear as standalone

#### Scenario: a held Story stops the chain until a person clears it

- **WHEN** the propose step succeeds on a Story and applies `ai:implement` alongside the hold
- **THEN** no Run of the implement step is created until the hold is removed, even though the
  Story now carries the implement step's trigger label
