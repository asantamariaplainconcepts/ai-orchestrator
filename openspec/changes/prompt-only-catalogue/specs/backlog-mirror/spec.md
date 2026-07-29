# backlog-mirror

## MODIFIED Requirements

### Requirement: a board view drives the pipeline by moving Stories between trigger columns

A board view SHALL present a column per configured trigger label, and moving a Story between columns
SHALL apply the destination's label at the vendor, which is what runs the pipeline (BR-003). A move SHALL
be refused, with the reason shown, when the Story has an active Run.

Column **order** SHALL no longer be derived from a chain between Automations: hand-off labels are retired
(#162), so no chain exists to order by. Columns SHALL be presented in the order the project's Automations
were configured. Nothing SHALL draw a pipeline the product cannot execute.

Every gesture SHALL remain available without dragging, because a drag is sugar and the explicit control is
the semantics.

#### Scenario: the seeded defaults produce a working board

- **WHEN** a project has Automations configured with trigger labels
- **THEN** the board presents one column per trigger label and each is a valid destination

#### Scenario: columns follow the flow

- **WHEN** the board renders its columns
- **THEN** they follow the order the Automations were configured in, since no hand-off chain exists to
  derive an order from

#### Scenario: a step that hands work to nobody

- **WHEN** an Automation completes
- **THEN** no label is written on its behalf, so no column receives the Story automatically — a prompt
  that wants to hand work on writes the label itself

#### Scenario: the two waits are drawn differently

- **WHEN** an Automation requires approval
- **THEN** the board says so, and the retired human-review block no longer appears

#### Scenario: closing the chain removes the column

- **WHEN** an Automation is deleted or disabled
- **THEN** its column stops being offered, which is the only way a column now leaves the board

#### Scenario: placing the column is the ordinary update

- **WHEN** an Admin changes which trigger label an Automation carries
- **THEN** the board's columns follow, through the same update any edit uses

#### Scenario: a move runs the pipeline

- **WHEN** a Member moves a Story to another column
- **THEN** the destination's trigger label is applied at the vendor and matching does the rest

#### Scenario: a refused move tells the truth

- **WHEN** a move cannot be applied
- **THEN** the card returns and the reason the vendor or the product gave is shown

#### Scenario: labelling at the vendor moves the card

- **WHEN** a trigger label changes at the vendor
- **THEN** the next mirror refresh moves the card to the matching column

#### Scenario: an active Run refuses the gesture

- **WHEN** a Story with an active Run is moved
- **THEN** the move is refused with its reason and nothing is written

#### Scenario: no gesture is drag-only

- **WHEN** a board gesture exists
- **THEN** it is reachable without dragging
