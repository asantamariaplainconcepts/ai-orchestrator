# automation-configuration

## MODIFIED Requirements

### Requirement: an Admin shapes the pipeline on a canvas

The portal SHALL present a project's Automations as two named things: a **catalogue** of every
Automation the project has, and a **workflow** — the path they form. An Automation SHALL belong to
the workflow exactly when it has an edge: it hands work to another, or another hands work to it.
Every other Automation SHALL appear in the catalogue only, and its absence from the workflow SHALL
NOT be an error or an omission — it is a trigger that acts on its own when somebody applies its
label. Membership SHALL be derived from the edges and SHALL NOT be stored.

The catalogue SHALL show each Automation's trigger label, action, runtime and whether it is
enabled, and SHALL offer every action already available: create, edit, disable, re-enable, delete.

The workflow SHALL render each Automation as a node, with an edge exactly where one Automation's
output label equals another's trigger label. The graph and its layout SHALL be derived from the
Automations themselves, with nothing about the picture stored. At a wide viewport the chain SHALL
be a single row read left to right, scrolling horizontally **within its own container** when it
exceeds it, and SHALL NOT wrap onto a second line; the page itself SHALL NOT scroll sideways. At a
narrow viewport it SHALL read top to bottom instead. The workflow SHALL state how many steps it has
and how many times it stops for a person, both derived.

The workflow SHALL let an Admin connect one Automation to another, which sets the upstream output
label to the downstream trigger label, and disconnect them, which clears it. It SHALL show where a
human is required — a broken chain between two Automations, and an Automation that requires
approval — and let the Admin add or remove that requirement in both positions. Every change
available by dragging SHALL be available from an explicit control at every viewport width. All
changes SHALL go through the ordinary Automation update, so its refusals apply unchanged.

#### Scenario: the seeded defaults draw the shipped pipeline

- **WHEN** a project with the default Automations opens the canvas
- **THEN** grill and propose are connected, the step after propose is shown as requiring a human,
  and no canvas configuration exists anywhere

#### Scenario: an Automation outside the chain

- **WHEN** a project has an Automation that neither hands work on nor receives it
- **THEN** it appears in the catalogue and not in the workflow, and nothing reports that as a
  problem

#### Scenario: the chain does not wrap

- **WHEN** a chain longer than the viewport renders at a wide width
- **THEN** it stays one row and scrolls within its own container, and the page does not scroll
  sideways

#### Scenario: how big the flow is

- **WHEN** the workflow renders
- **THEN** it states its number of steps and how many times it stops for a person, and both match
  the Automations it drew

#### Scenario: a project with no chain at all

- **WHEN** every Automation in a project stands alone
- **THEN** the catalogue lists them and the workflow shows an empty state saying what would make a
  flow

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
