# backlog-mirror

## MODIFIED Requirements

### Requirement: a board view drives the pipeline by moving Stories between trigger columns

The project's Operate surface SHALL offer a board view whose columns derive from the project's
enabled Automation trigger labels, plus a pile for Stories carrying none of them. Moving a Story
into a trigger column SHALL apply that label through the existing licensed write, and moving it
out SHALL remove it; no other vendor mutation is permitted from the board. Every move available
by dragging SHALL also be available without dragging, at every viewport width. A move SHALL be
refused before any write when the Story has an active Run, naming the rule. A vendor refusal
SHALL return the Story to its column with the refusal readable and the mirror unchanged. Cards
SHALL show the state of their latest Run, including a link to a running Run's output.

The columns SHALL be ordered by the workflow: a step that hands work to another SHALL appear before
it. Automations that are not part of the workflow SHALL appear after the ordered ones, because a Story
can carry their labels and must be somewhere — the board orders the flow, it does not decide what
exists (DEC-053).

Where a step hands work to nobody, the board SHALL show a column after it holding the Stories that
step has finished, because those Stories are waiting for a person to decide whether the work
continues. That column SHALL be drawn as a place with its own heading, count and empty state, and it
SHALL show how long each Story has waited (BR-006). Placing it SHALL clear the preceding step's output
label through the ordinary Automation update, which is the same meaning and the same write the
workflow canvas uses, so the two surfaces cannot disagree. Removing its cause SHALL remove the column
and return its Stories to the columns their labels match.

A step that requires approval SHALL NOT produce such a column. That is a different wait: a Run in
`AwaitingApproval` has already reached its step and is in flight there, so it SHALL remain in that
step's column with its state shown on the card, and the step's column SHALL carry its existing gated
marking. The same holds for a Run awaiting an answer.

#### Scenario: the seeded defaults produce a working board

- **WHEN** a project's default Automations exist and the board opens
- **THEN** their trigger labels are the columns, with no board configuration anywhere

#### Scenario: columns follow the flow

- **WHEN** a project's Automations form a chain
- **THEN** the columns appear in the chain's order, with the Automations outside it after them

#### Scenario: a step that hands work to nobody

- **WHEN** a step's Automation has no output label and Stories have finished at that step
- **THEN** a column after it holds those Stories, with its own heading, count and how long each has
  waited

#### Scenario: the two waits are drawn differently

- **WHEN** a step requires approval and a Story's Run is awaiting that approval
- **THEN** the Story is in that step's own column with its state on the card, the column carries its
  gated marking, and no separate column is created before it

#### Scenario: closing the chain removes the column

- **WHEN** the preceding step is given an output label again
- **THEN** the column disappears and its Stories appear in the columns their labels match

#### Scenario: placing the column is the ordinary update

- **WHEN** a person is placed between two steps from the board
- **THEN** the preceding step's output label is cleared through the ordinary Automation update, and a
  refusal is shown with its reason and changes nothing

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
