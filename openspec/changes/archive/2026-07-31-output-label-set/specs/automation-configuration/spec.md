# automation-configuration

## MODIFIED Requirements

### Requirement: an Automation can hand work on by writing a label when it succeeds

An Automation SHALL carry a **set** of output labels, every one of them applied to the Story through
the licensed label write when a Run of that Automation succeeds, and applied at no other time. An
empty set SHALL mean the Automation ends silently. Saving an Automation whose set contains its own
trigger label SHALL be refused, naming the reason — the refusal SHALL apply to every member, not only
to a single value.

Labels SHALL be compared the way the vendor compares them, so a set SHALL NOT hold the same label
twice in two spellings.

Every label SHALL be attempted, and a label the vendor could not ensure SHALL be reported to the human
rather than silently skipped; the Run SHALL fail naming every label that did not land. A Run that
failed at hand-off MAY already have handed on through the labels that did land, which is what a
partially applied set means and SHALL be visible on the Story.

#### Scenario: the chain continues past the grill

- **WHEN** an Automation with an output label has a Run that succeeds
- **THEN** the label reaches the vendor, and after reconciliation an Automation triggered by that
  label has a Run of its own

#### Scenario: silence is the default

- **WHEN** an Automation without an output label has a Run that succeeds
- **THEN** no label is written

#### Scenario: only success hands work on

- **WHEN** a Run of an Automation with an output label fails or is cancelled
- **THEN** no label is written

#### Scenario: an Automation may not trigger itself

- **WHEN** an Automation is saved with an output label equal to its trigger label
- **THEN** the save is refused with the reason

#### Scenario: several labels leave together

- **WHEN** an Automation naming several output labels has a Run that succeeds
- **THEN** every one of them reaches the vendor through the same write path

#### Scenario: one label the vendor refuses does not hide the others

- **WHEN** one label of several cannot be ensured
- **THEN** the remaining labels are still attempted, and the Run fails naming every label that did
  not land

#### Scenario: the same label twice is one label

- **WHEN** a set is saved containing the same label in two spellings the vendor treats as one
- **THEN** it is stored once

#### Scenario: what was configured before still works

- **WHEN** an Automation configured with a single output label runs after this change
- **THEN** it behaves exactly as it did, as a set of one

### Requirement: an Admin shapes the pipeline on a canvas

The portal SHALL present a project's Automations as two named things: a **catalogue** of every
Automation the project has, and a **workflow** — the path they form. An Automation SHALL belong to
the workflow exactly when it has an edge: it hands work to another, or another hands work to it.
Every other Automation SHALL appear in the catalogue only, and its absence from the workflow SHALL
NOT be an error or an omission — it is a trigger that acts on its own when somebody applies its
label. Membership SHALL be derived from the edges and SHALL NOT be stored.

The catalogue SHALL show each Automation's trigger label, action, runtime and whether it is
enabled, and SHALL offer every action already available: create, edit, disable, re-enable, delete.

The workflow SHALL render each Automation as a node, with **one edge per output label that equals
another Automation's trigger label** — several edges leaving one node when several match. The graph
and its layout SHALL be derived from the Automations themselves, with nothing about the picture
stored. At a wide viewport the chain SHALL be a single row read left to right, scrolling horizontally
**within its own container** when it exceeds it, and SHALL NOT wrap onto a second line; the page
itself SHALL NOT scroll sideways. At a narrow viewport it SHALL read top to bottom instead. The
workflow SHALL state how many steps it has and how many times it stops for a person, both derived.

Where several edges leave one node, the workflow SHALL state that they do not run at once: BR-001
allows one active Run per Story, so a second simultaneous match is ignored rather than queued. A
picture that draws branches without saying this SHALL be treated as claiming otherwise.

The workflow SHALL let an Admin connect one Automation to another, which **adds** the downstream
trigger label to the upstream set, and disconnect them, which **removes that label and leaves the
rest**. It SHALL show where a human is required — a broken chain between two Automations, and an
Automation that requires approval — and let the Admin add or remove that requirement in both
positions. Every change available by dragging SHALL be available from an explicit control at every
viewport width. All changes SHALL go through the ordinary Automation update, so its refusals apply
unchanged.

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

#### Scenario: two edges leave one node

- **WHEN** an Automation names two output labels that each match another enabled Automation's
  trigger
- **THEN** two edges leave that node, and the workflow says that branches serialize rather than run
  at once

#### Scenario: disconnecting one branch keeps the other

- **WHEN** one of two connections leaving an Automation is removed
- **THEN** only that label leaves the set, and the other edge still renders

### Requirement: an Admin configures what a trigger label makes an Agent do

An Admin SHALL configure, per Project, which trigger label starts which action, on which runtime,
with which timeout and whether the plan requires approval. Every field SHALL be bounded, and the
refusal for an unbounded or unparseable value SHALL name the field.

The output labels input SHALL be a picker that also accepts a freely typed value. It SHALL suggest
the trigger labels of the project's **other enabled** Automations, because wiring the next step of a
sequential workflow is what this field is most often for, and SHALL NOT suggest the Automation's own
trigger. Accepting free text SHALL remain possible, because a label may be a mark that triggers
nothing, or a trigger that does not exist yet.

A suggestion SHALL be a convenience only: every refusal SHALL also be enforced where the Automation
is saved, so a caller that does not use the portal is refused identically.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, action, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** an action from the catalogue has no executing Agent yet
- **THEN** it remains selectable and the interface says it cannot run yet — a configurable
  action that silently never executes is a trap

#### Scenario: the form offers what is wirable

- **WHEN** an Admin opens the output labels input on a project with other enabled Automations
- **THEN** their trigger labels are offered, and this Automation's own trigger is not

#### Scenario: a disabled Automation is not offered

- **WHEN** another Automation in the project is disabled
- **THEN** its trigger is not among the suggestions

#### Scenario: a label nobody listens to is still allowed

- **WHEN** an Admin types a label that matches no trigger
- **THEN** it is accepted and applied on success like any other
