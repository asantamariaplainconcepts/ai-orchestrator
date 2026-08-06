# automation-configuration

## ADDED Requirements

### Requirement: each row of the setup plan can be excluded before the press

Every row of the plan SHALL be selectable, and every row SHALL start selected. Both kinds SHALL be
selectable on the same terms: a row that would wire a file the repository already holds, and a row
that would install a starter. The difference between them is what happens when they are *kept*, not
whether the Admin may decline them.

Excluding a row SHALL be the whole gesture. It SHALL NOT require a reason, a second dialogue, or a
different control per kind — the plan is one checklist of what will happen, and a preview a reader
cannot change is a notice rather than a decision.

The confirm SHALL carry the selection, so that only the steps still selected are created. Where no
row is selected, the confirm control SHALL be unavailable: an action that provably does nothing is
better withheld than offered.

Exclusion SHALL affect only what this press creates. It SHALL NOT delete or disable an Automation
the project already has, and it SHALL NOT modify or remove a file the repository already holds.

#### Scenario: every row starts selected

- **WHEN** the plan is shown
- **THEN** every row is selectable and every row is selected

#### Scenario: both kinds of row can be excluded

- **WHEN** the plan holds a row for a file already in the repository and a row that would install a
  starter
- **THEN** each can be excluded, by the same control

#### Scenario: only the selected rows are created

- **WHEN** rows are excluded and the plan is confirmed
- **THEN** the action is invoked with the remaining selection, and the report names the excluded
  steps as excluded

#### Scenario: excluding everything withholds the press

- **WHEN** no row is selected
- **THEN** the confirm control is unavailable

#### Scenario: reading and choosing still write nothing

- **WHEN** rows are selected and deselected
- **THEN** no Automation is created and nothing is written to the repository until the plan is
  confirmed

### Requirement: a hand-off broken by exclusion is shown, and never blocks

Where an excluded step was handing work to a step that is still selected, the plan SHALL mark that
the hand-off no longer happens — a person hands on at that point instead. The mark SHALL appear as
the selection changes, without a further read of the repository.

The confirm SHALL NOT be blocked, disabled, or gated behind an extra confirmation by such a break. A
workflow with a human hand-off is a workflow this product already supports; the break is
information, not an error.

A step SHALL be understood to hand work to another exactly when one of its output labels is the
other's trigger, compared case-insensitively — the same identity BR-003 compares triggers with
(DEC-056). An output label naming no step in the plan SHALL NOT be treated as a hand-off, so
excluding a step that hands work to nobody SHALL mark nothing.

For the plan to answer this without a further read, each row SHALL carry the labels its step hands
on, from the discovery the card has already performed.

#### Scenario: excluding a step that feeds another marks the gap

- **WHEN** a step whose output label is another selected step's trigger is excluded
- **THEN** the plan marks that the receiving step is no longer handed work

#### Scenario: the mark never blocks the press

- **WHEN** a hand-off gap is marked
- **THEN** the confirm remains available and needs no additional confirmation

#### Scenario: excluding a step that hands work to nobody marks nothing

- **WHEN** a step with no output label naming another step in the plan is excluded
- **THEN** no hand-off gap is marked

#### Scenario: the gap is computed from what discovery already returned

- **WHEN** the selection changes
- **THEN** the mark updates without an additional read of the repository

### Requirement: an exclusion is a choice about this press, not a stored preference

The plan SHALL NOT remember what a previous press excluded. Opening the card again, or running the
setup again later, SHALL propose every step it would act on with every row selected.

A step someone declined once is not a step the project has decided against — and a stored exclusion
would silently hide it from the next person, who never made that choice.

#### Scenario: a later visit proposes the excluded step again

- **WHEN** the card is opened again after a press that excluded a step whose Automation was
  therefore never created
- **THEN** that step appears in the plan again, selected

## MODIFIED Requirements

### Requirement: the setup card says what it will create before it is pressed

Where a pipeline has been discovered, the portal SHALL show what pressing the build control would
create, **before** it is pressed. The plan SHALL list one row per step, naming the trigger, the
prompt file that step would wire, whether that file already exists in the repository or a starter
would be installed for it, and whether the step waits for a person.

The plan SHALL be computed from the discovery the card has already performed. It SHALL NOT require a
second endpoint, and SHALL NOT cost an additional vendor read per row.

A step that would be wired but for which no starter can be installed SHALL be distinguishable from
one that would have a starter written, because those differ in whether anything is written to the
repository.

A step that neither has a file in the chosen directory nor can have a starter installed SHALL NOT
appear in the plan at all: nothing would happen for it either way, and a row offering a choice that
changes nothing is noise in a list whose whole purpose is to say what the press will do.

**No separate consent SHALL be required for installing the starters the plan names.** The rows state
which files would be written; a control asking whether to write them restates the preview, and a
confirmation of a confirmation trains a reader past both.

The statement that starters arrive as a draft pull request SHALL sit with the control that creates
them, because that is where the decision is taken.

A plan longer than a few rows SHALL collapse, and SHALL be expandable — a plan that fills the screen
stops being read, which defeats showing it.

#### Scenario: the plan precedes the press

- **WHEN** a pipeline has been discovered
- **THEN** one row per step is shown, naming the trigger, the file it wires, whether that file exists
  and whether the step waits for a person

#### Scenario: reading the plan changes nothing

- **WHEN** the plan is computed
- **THEN** no Automation is created and nothing is written to the repository

#### Scenario: the preview replaces the consent

- **WHEN** the plan is visible
- **THEN** no separate control asks whether to install the starters it names

#### Scenario: a step nothing would happen for is not offered

- **WHEN** a step has no file in the chosen directory and no starter can be installed for it
- **THEN** it does not appear as a row in the plan
