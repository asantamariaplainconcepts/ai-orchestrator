## MODIFIED Requirements

### Requirement: a board view drives the pipeline by moving Stories between trigger columns

The project's Operate surface SHALL offer a board view whose columns are the project's **lifecycle
stages**, in the order stored on the Project, plus a pile for Stories carrying none of them. Moving a
Story into a stage column SHALL apply that label through the existing licensed write, and moving it
out SHALL remove it; no other vendor mutation is permitted from the board. Every move available
by dragging SHALL also be available without dragging, at every viewport width. A move SHALL be
refused before any write when the Story has an active Run, naming the rule. A vendor refusal
SHALL return the Story to its column with the refusal readable and the mirror unchanged. Cards
SHALL show the state of their latest Run, including a link to a running Run's output.

The columns SHALL be the stored lifecycle read back, and SHALL NOT be re-derived from the Automations
by walking output labels (ADR-0022). A stage SHALL render as a column whether or not an Automation
claims the transition into it, so a stage is never omitted for having no claimant. An Automation whose
trigger label is not a stage SHALL contribute no column; a Story carrying that label SHALL be placed
by whichever stage label it also carries, or in the untouched pile.

Where a boundary between two adjacent stages is claimed by no Automation, the board SHALL label that
boundary as **waiting for a person**, and SHALL NOT draw it as a fault: no validation error, no
"incomplete configuration" marker, and no elapsed-time or overdue indication, because a human wait is
untimed (BR-006). A person applying the next stage's label is the same mechanism an Automation uses, so
nothing further is needed to make the flow continue.

A step that requires approval SHALL NOT be drawn as an unclaimed boundary. That is a different wait: a
Run in `AwaitingApproval` has already reached its step and is in flight there, so it SHALL remain in
that step's column with its state shown on the card, and the step's column SHALL carry its existing
gated marking. The same holds for a Run awaiting an answer.

The board SHALL also be where an Admin arranges the flow: assigning an Automation to a transition,
moving it to another, and placing one on a transition whose from-stage is not yet a stage. Those
changes SHALL go through the ordinary Automation update, so BR-003's refusal applies unchanged, and
SHALL be offered to **ACT-001 Admin** only — an ACT-002 Member SHALL be offered no such control, and a
direct API request SHALL be refused on the missing permission rather than by the absence of a button
(BR-009).

#### Scenario: the columns are the stored lifecycle

- **WHEN** a project's lifecycle is `s1, s2, s3` and the board opens
- **THEN** those are the columns, in that order, taken from the project rather than recomputed from
  the Automations

#### Scenario: an unclaimed stage still has a column

- **WHEN** no Automation claims the transition into `s3`
- **THEN** `s3` still renders as a column, and its incoming boundary reads as waiting for a person with
  no error and no timer

#### Scenario: the seeded defaults produce a working board

- **WHEN** a project's default Automations exist and the board opens
- **THEN** the stages their claimed transitions created are the columns, with no board configuration
  anywhere

#### Scenario: the two waits are drawn differently

- **WHEN** a step requires approval and a Story's Run is awaiting that approval
- **THEN** the Story is in that step's own column with its state on the card, the column carries its
  gated marking, and no unclaimed boundary is drawn for it

#### Scenario: a move runs the pipeline

- **WHEN** a Story is moved into a stage column whose transition an enabled Automation claims
- **THEN** the label reaches the vendor and, after reconciliation, a Run exists for that Automation

#### Scenario: a refused move tells the truth

- **WHEN** the vendor refuses the label write
- **THEN** the Story returns to its original column, the refusal is readable, and the mirror is
  unchanged

#### Scenario: labelling at the vendor moves the card

- **WHEN** a stage label is applied at the vendor directly
- **THEN** the Story appears in that column after the next reconciliation, with no board-specific
  code involved

#### Scenario: an active Run refuses the gesture

- **WHEN** a Story with an active Run is moved onto a stage column
- **THEN** the move is refused before any write, naming the one-active-Run rule

#### Scenario: no gesture is drag-only

- **WHEN** the board renders at any width
- **THEN** every move offered by dragging, and every arrangement change offered by dragging, is
  offered by an explicit control, and each goes through the same function as its drag

#### Scenario: a Member reads the board and arranges nothing

- **WHEN** a signed-in ACT-002 Member opens the board and then calls the API directly to change a
  claimed transition
- **THEN** the columns and cards render, no arrangement control was offered, and the request is
  refused on the missing permission
