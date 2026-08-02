# automation-configuration

## ADDED Requirements

### Requirement: the workflow reads top-down at every width

The workflow SHALL render as a single vertical layout at every viewport width. There SHALL NOT be a
second layout, a second interaction model, or a breakpoint at which the chain changes direction.

Reordering SHALL be available at every width. A capability offered only above a breakpoint is a
capability the narrower reader does not have.

The chain SHALL NOT scroll horizontally within its own container at any supported width. A branch
SHALL indent under the step it leaves, in addition to naming that step.

A step SHALL present its trigger, whether a person gates it, and the actions available on it in one
header, and SHALL NOT be taller than the information it carries.

Where an Automation carries output labels that reach no other Automation, that SHALL be announced on
the step that owns the labels, because that is where it is corrected — not at the gap that follows.

Where a gap between steps is not being connected, no selection control SHALL render. Connecting
SHALL remain reachable from a named control, which is what a shipped capability requires; being
permanently on screen is not.

Where a step requires approval, it SHALL wear the same chip the board's column header uses, so the
two surfaces cannot disagree about what a human gate is called. That chip's explanatory hint SHALL
be the caller's, because the reason differs by surface.

#### Scenario: a pipeline is reorderable on a phone

- **WHEN** an Admin opens the workflow at a phone width
- **THEN** the control for placing a human step is visible and usable

#### Scenario: one direction, no sideways scroll

- **WHEN** the workflow renders at any supported width
- **THEN** the chain is a column, and it does not scroll horizontally inside its container

#### Scenario: a gap offers nothing until somebody connects

- **WHEN** an Admin looks at a gap nobody is connecting
- **THEN** no selection control is present, and a named control reveals one when used

#### Scenario: a gated step wears the board's chip

- **WHEN** a step requires approval
- **THEN** it shows the same chip the board's column header shows for a gated column
