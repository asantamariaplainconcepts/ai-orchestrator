# automation-configuration

## ADDED Requirements

### Requirement: an Admin places the human review by dragging it where the person belongs

The portal SHALL let an Admin drag a human-review block from the catalogue and drop it into a gap
between two steps of the workflow. Dropping it SHALL clear the **preceding** step's output label, so
the chain stops there and a person reviews what that step produced before the work continues.

The block SHALL NOT change any step's approval requirement. Reviewing what a step produced and
approving what a step is about to do are two different waits with two different run-time behaviours
(BR-007), and the workflow SHALL keep them distinguishable: the block is the first, and the control
on the step's own card remains the second.

The block SHALL NOT be a persisted entity, and its position SHALL NOT be stored — the absent output
label is the fact.

Removing the block SHALL restore the preceding step's output label where the step drawn after the
gap makes the destination unambiguous. Where nothing follows the gap, removal SHALL require naming a
destination, because a label names one and an absence does not.

Moving the block from one gap to another SHALL clear the new gap's preceding output label **before**
restoring the old one, so that an interrupted move leaves a review in both places and never in
neither.

Every change SHALL go through the ordinary Automation update, so its refusals apply unchanged; a
refused change SHALL return the workflow to what is stored and SHALL show the reason given. While a
drag is in progress the gaps that can accept the block SHALL be marked, and a gap that would be
refused SHALL NOT appear as a target.

Dragging SHALL remain sugar: the controls that break and restore a connection SHALL stay available at
every viewport width, and below the width at which the flow reads left to right, dragging SHALL NOT
be offered at all.

#### Scenario: placing a person after a step

- **WHEN** an Admin drops the human block into the gap after a step that hands work on
- **THEN** that step's output label is cleared, the block is drawn in that gap, and no step's
  approval requirement changes

#### Scenario: the two waits stay different things

- **WHEN** a step requires approval and the gap after it holds no block
- **THEN** the card shows the approval requirement and the gap shows a connected chain, and neither
  is drawn as the other

#### Scenario: removing the person where the destination is known

- **WHEN** an Admin removes a block from a gap that has a step drawn after it
- **THEN** the preceding step's output label becomes that step's trigger label and the chain closes

#### Scenario: removing the person with nothing after the gap

- **WHEN** an Admin removes a block from the end of a chain
- **THEN** they are asked which Automation the work should be handed to, because an absence names no
  destination

#### Scenario: moving the person along the flow

- **WHEN** an Admin drags a placed block from one gap to another
- **THEN** the step before the new gap stops handing work on and the step before the old gap resumes

#### Scenario: an interrupted move fails safe

- **WHEN** the new gap has been broken and the old one has not yet been reconnected
- **THEN** a person is asked in both places, and no work continues unreviewed

#### Scenario: a refused change changes nothing

- **WHEN** a change would be refused by the ordinary Automation update
- **THEN** the workflow returns to what is stored and the reason is shown

#### Scenario: an impossible gap is not a target

- **WHEN** a drag is in progress
- **THEN** the gaps that can accept the block are marked and one that would be refused is not
  offered

#### Scenario: the same change without dragging

- **WHEN** an Admin uses the explicit break or restore control instead
- **THEN** the output label changes identically, at every viewport width

#### Scenario: no dragging where the flow is vertical

- **WHEN** the workflow renders below the width at which it reads left to right
- **THEN** dragging is not offered and every change remains reachable
