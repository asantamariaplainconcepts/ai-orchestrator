# automation-configuration

## ADDED Requirements

### Requirement: an Admin places the human review by dragging it where the person belongs

The portal SHALL let an Admin drag a human-review block from the catalogue and drop it into a gap
between two steps of the workflow, which SHALL set the following step's approval requirement.
Removing the block SHALL clear it. Dragging a placed block to a different gap SHALL move the
requirement: set on the step it now precedes, cleared on the step it no longer precedes.

The block SHALL NOT be a persisted entity. It is the approval requirement drawn where it takes
effect, so nothing about its position SHALL be stored.

A move SHALL apply the new requirement before clearing the old one, so that an interrupted move
leaves an extra approval rather than none — an unexpected gate stops a Run and can be cleared, while
a missing gate lets one proceed unattended.

Every change SHALL go through the ordinary Automation update, so its refusals apply unchanged; a
refused drop SHALL return the workflow to what is stored and SHALL show the reason given. While a
drag is in progress the gaps that can accept the block SHALL be marked, and a gap that would be
refused SHALL NOT appear as a target.

Dragging SHALL remain sugar: the explicit approval control SHALL stay available at every viewport
width, and below the width at which the flow reads left to right, dragging SHALL NOT be offered at
all.

#### Scenario: placing a person between two steps

- **WHEN** an Admin drops the human block into the gap before a step that runs unattended
- **THEN** that step requires approval, and the block is drawn in that gap

#### Scenario: removing the person

- **WHEN** an Admin removes a placed human block
- **THEN** the following step no longer requires approval

#### Scenario: moving the person along the flow

- **WHEN** an Admin drags a placed block from one gap to another
- **THEN** the step it now precedes requires approval and the step it left does not

#### Scenario: an interrupted move fails safe

- **WHEN** a move applies the new requirement and the clearing of the old one does not complete
- **THEN** both steps require approval, and neither Run proceeds unattended

#### Scenario: a refused drop changes nothing

- **WHEN** a drop would be refused by the ordinary Automation update
- **THEN** the workflow returns to what is stored and the reason is shown

#### Scenario: an impossible gap is not a target

- **WHEN** a drag is in progress
- **THEN** the gaps that can accept the block are marked and one that would be refused is not
  offered

#### Scenario: the same change without dragging

- **WHEN** an Admin uses the explicit approval control instead
- **THEN** the requirement changes identically, at every viewport width

#### Scenario: no dragging where the flow is vertical

- **WHEN** the workflow renders below the width at which it reads left to right
- **THEN** dragging is not offered and every change remains reachable
