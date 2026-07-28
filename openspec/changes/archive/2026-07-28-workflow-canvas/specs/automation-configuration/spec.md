# automation-configuration

## ADDED Requirements

### Requirement: an Admin shapes the pipeline on a canvas

The portal SHALL offer a canvas view of a project's Automations in which each Automation is a
node and an edge exists exactly where one Automation's output label equals another's trigger
label. The graph and its layout SHALL be derived from the Automations themselves, with nothing
about the picture stored. The canvas SHALL let an Admin connect one Automation to another, which
sets the upstream output label to the downstream trigger label, and disconnect them, which clears
it. It SHALL show where a human is required — a broken chain between two Automations, and an
Automation that requires approval — and let the Admin add or remove that requirement in both
positions. Every change available by dragging SHALL be available from an explicit control at
every viewport width. All changes SHALL go through the ordinary Automation update, so its
refusals apply unchanged.

#### Scenario: the seeded defaults draw the shipped pipeline

- **WHEN** a project with the default Automations opens the canvas
- **THEN** grill and propose are connected, the step after propose is shown as requiring a human,
  and no canvas configuration exists anywhere

#### Scenario: closing the chain makes the pipeline autonomous

- **WHEN** the human requirement is removed from the gap between two Automations
- **THEN** the upstream Automation's output label becomes the downstream trigger label, and a
  subsequent successful Run of the upstream Automation causes a Run of the downstream one with
  no human acting

#### Scenario: opening the chain restores the wait

- **WHEN** a human requirement is placed on a connection
- **THEN** the upstream output label is cleared and no Run follows automatically

#### Scenario: approval is the same field the form shows

- **WHEN** the human requirement is added to or removed from an Automation itself
- **THEN** that Automation's approval requirement changes, and the form reflects it

#### Scenario: a refused change is reported, not applied

- **WHEN** a connection would make an Automation trigger itself, or would collide with another
  Automation's trigger
- **THEN** the change is refused with its reason and the canvas returns to what is stored

#### Scenario: no gesture is drag-only

- **WHEN** the canvas renders at any width
- **THEN** every connection and human-requirement change offered by dragging is offered by an
  explicit control
