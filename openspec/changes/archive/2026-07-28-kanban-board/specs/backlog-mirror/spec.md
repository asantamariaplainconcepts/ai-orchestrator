# backlog-mirror

## ADDED Requirements

### Requirement: a board view drives the pipeline by moving Stories between trigger columns

The project's Operate surface SHALL offer a board view whose columns derive from the project's
enabled Automation trigger labels, plus a pile for Stories carrying none of them. Moving a Story
into a trigger column SHALL apply that label through the existing licensed write, and moving it
out SHALL remove it; no other vendor mutation is permitted from the board. Every move available
by dragging SHALL also be available without dragging, at every viewport width. A move SHALL be
refused before any write when the Story has an active Run, naming the rule. A vendor refusal
SHALL return the Story to its column with the refusal readable and the mirror unchanged. Cards
SHALL show the state of their latest Run, including a link to a running Run's output.

#### Scenario: the seeded defaults produce a working board

- **WHEN** a project's default Automations exist and the board opens
- **THEN** their trigger labels are the columns, with no board configuration anywhere

#### Scenario: a move runs the pipeline

- **WHEN** a Story is moved into a trigger column whose Automation is enabled
- **THEN** the label reaches the vendor and, after reconciliation, a Run exists for that
  Automation

#### Scenario: a refused move tells the truth

- **WHEN** the vendor refuses the label write
- **THEN** the Story returns to its original column, the refusal is readable, and the mirror is
  unchanged

#### Scenario: labelling at the vendor moves the card

- **WHEN** a trigger label is applied at the vendor directly
- **THEN** the Story appears in that column after the next reconciliation, with no board-specific
  code involved

#### Scenario: an active Run refuses the gesture

- **WHEN** a Story with an active Run is moved onto a trigger column
- **THEN** the move is refused before any write, naming the one-active-Run rule

#### Scenario: no gesture is drag-only

- **WHEN** the board renders at any width
- **THEN** every move offered by dragging is offered by an explicit control on the card
