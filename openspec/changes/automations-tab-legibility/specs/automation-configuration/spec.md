## MODIFIED Requirements

### Requirement: an Admin edits, disables and re-enables an Automation

An Admin SHALL be able to change an Automation's trigger, action, runtime, approval flag and
timeout, and to disable and re-enable it. An edit SHALL face the same BR-003 overlap validation
as a create, excluding the Automation being edited from the comparison. Enabling SHALL re-run
that validation; disabling SHALL NOT, because a disabled Automation cannot overlap anything.
Editing or disabling SHALL NOT affect Runs already active — they complete against the
Automation they were created with.

This SHALL be reachable from the portal, not only from the API. The editing surface SHALL be the same
form that creates an Automation, so that the input rules, the field vocabularies and the refusals cannot
diverge between the two.

That form SHALL arrive **over** the tab rather than within it: a modal panel, centred at pointer
widths and a bottom sheet at narrow ones. Opening it, dismissing it, and saving it SHALL NOT change
the page's scroll position — a form inserted into the page moves every other thing on it, and an
Admin who was reading the workflow SHALL still be looking at it afterwards. Dismissal SHALL be
offered by at least the keyboard, a close control, and the overlay, and all of them SHALL abandon the
edit identically.

The panel SHALL be reachable from both places an Automation is shown — a catalogue entry and a
workflow node — and both SHALL open the same panel, so the two surfaces cannot grow two editing
experiences.

Disabling, re-enabling and deleting SHALL be offered inside that panel, where they act on the
Automation the panel names, and SHALL NOT be offered as controls on a catalogue row. Deletion SHALL
require a second, distinct confirmation before it is sent: a single click adjacent to Edit is one
mis-aim away from a deletion nobody intended.

Because the update endpoint is a full replace, an edit SHALL send every field, seeded from the
Automation's stored values — a field the form omits SHALL NOT be silently reset to its default. The
timeout SHALL therefore be a visible field in both modes: a value resent on the Admin's behalf is one
they are entitled to see.

Changing the action to one that reads no document SHALL clear the document name, because a value no
visible control can reach is a value the Admin cannot manage.

An edit SHALL NOT change whether the Automation is enabled.

Every refusal the panel can provoke — an overlapping save, a refused enable, a refused delete — SHALL
be reported inside the panel, beside the control that provoked it, and SHALL NOT be reported only
where the reader is no longer looking.

#### Scenario: an edit that would overlap is refused

- **WHEN** an edit would make the trigger intersect another enabled Automation's
- **THEN** it is refused with the create-time error and nothing changes

#### Scenario: an unchanged trigger does not overlap itself

- **WHEN** an edit leaves the trigger as it was
- **THEN** it succeeds — the Automation is not compared against itself

#### Scenario: re-enabling re-checks

- **WHEN** a disabled Automation whose trigger now collides with a newer one is enabled
- **THEN** it is refused and stays disabled

#### Scenario: disabling stops future matches only

- **WHEN** an Automation with an active Run is disabled
- **THEN** the Run is unaffected and no new match is made for that trigger

#### Scenario: the form opens on what is stored

- **WHEN** an Admin opens edit on an Automation
- **THEN** every field shows that Automation's current value, including its timeout

#### Scenario: an untouched timeout survives the edit

- **WHEN** an Admin edits only the trigger label of an Automation whose timeout was set to a
  non-default value
- **THEN** the stored timeout is unchanged after the save

#### Scenario: an edit leaves the enabled flag alone

- **WHEN** a disabled Automation is edited and saved
- **THEN** it is still disabled, and an enabled one is still enabled

#### Scenario: a document name goes when its action does

- **WHEN** an Admin changes the action to one that reads no document
- **THEN** the stored document name is cleared rather than kept out of sight

#### Scenario: the refusal is the API's own

- **WHEN** a save is refused for overlap or for triggering itself
- **THEN** the reason the API gave is what the form shows

#### Scenario: opening an edit leaves the page where it was

- **WHEN** an Admin opens an Automation for editing on a tab scrolled away from its top
- **THEN** the form appears over the tab and the page's scroll position is unchanged

#### Scenario: abandoning and saving both leave the page where it was

- **WHEN** an Admin dismisses the panel by keyboard, by its close control, or by the overlay, or
  saves it successfully
- **THEN** the page's scroll position is the same as before the panel opened

#### Scenario: one panel, either surface

- **WHEN** an Admin opens editing from a catalogue entry and then from a workflow node
- **THEN** both open the same panel on the same Automation's stored values

#### Scenario: a deletion is confirmed before it is sent

- **WHEN** an Admin presses delete in the panel
- **THEN** nothing is deleted and a distinct confirmation is offered, and only pressing that sends
  the deletion

#### Scenario: a row offers no destructive control

- **WHEN** an Admin reads the catalogue
- **THEN** no entry offers delete, and deleting is reachable only through the panel

### Requirement: an Admin shapes the pipeline on a canvas

The portal SHALL present a project's Automations as two named things: a **catalogue** of every
Automation the project has, and a **workflow** — the path they form. An Automation SHALL belong to
the workflow exactly when it has an edge: it hands work to another, or another hands work to it.
Every other Automation SHALL appear in the catalogue only, and its absence from the workflow SHALL
NOT be an error or an omission — it is a trigger that acts on its own when somebody applies its
label. Membership SHALL be derived from the edges and SHALL NOT be stored.

The catalogue SHALL show each Automation's trigger label, whether it is enabled, and **its relation
to the workflow** — in the flow, or standalone. That relation SHALL be derived from the same edges
the workflow draws, so the two surfaces cannot disagree about it. An Automation's action and runtime
SHALL be shown in the panel that can change them rather than repeated on a row that cannot. Create,
edit, disable, re-enable and delete SHALL all remain reachable from the tab — create from its
toolbar, the rest through the panel a catalogue entry opens.

The workflow SHALL render each Automation as a node, with **one edge per output label that equals
another Automation's trigger label** — several edges leaving one node when several match. The graph
and its layout SHALL be derived from the Automations themselves, with nothing about the picture
stored. A chain SHALL read top to bottom at every viewport width, in one layout and one interaction
model, and SHALL NOT scroll the page sideways. A node SHALL offer editing, opening the same panel a
catalogue entry opens.

The tab SHALL state how many steps the workflow has and how many times it stops for a person, both
derived — a count of Automations is a fact about the catalogue and says nothing about the pipeline.

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

#### Scenario: a catalogue entry states its relation to the flow

- **WHEN** a project holds both a chained and an unchained Automation
- **THEN** the chained one reads as in the workflow and the unchained one as standalone, matching
  what the workflow drew

#### Scenario: the chain reads top-down and does not scroll the page sideways

- **WHEN** a chain longer than the viewport renders at any width
- **THEN** it reads top to bottom, and neither the chain's own container nor the page scrolls
  sideways

#### Scenario: how big the flow is

- **WHEN** the Automations tab renders a workflow
- **THEN** the tab states its number of steps and how many times it stops for a person, and both
  match the Automations the workflow drew

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

## ADDED Requirements

### Requirement: the Automations tab orders its content by how often it is read

The Automations tab SHALL present the workflow before the catalogue, because the flow is what an
Admin opens the tab to read and the catalogue is the reference it is built from. At a wide viewport
the catalogue SHALL sit beside the workflow rather than above or below it; where it cannot, the
workflow SHALL come first and the Automations the workflow does not already draw SHALL follow it as
their own named group, so no Automation is shown twice and none is hidden.

Setting up a workflow from the repository, and trying a prompt, SHALL be reachable from the tab's
own toolbar and SHALL open over the tab. They SHALL NOT occupy the tab's vertical space as permanent
content: they are reached on a first day or an occasional afternoon, and a tab that opens on them
answers a question its reader did not ask.

**A project with no Automations configured is the exception.** While none exists, the workflow setup
surface SHALL render inline as the content of the tab, because there is no flow to read and setting
one up is the only thing to do there. In that state the toolbar SHALL NOT offer a second route to the
same surface.

Every control this ordering moves SHALL remain reachable at every viewport width — a capability
offered only at one width is a capability withdrawn at the other.

#### Scenario: the workflow is the first thing on the tab

- **WHEN** an Admin opens the Automations tab of a project whose Automations form a chain
- **THEN** the workflow appears before the catalogue, and at a wide viewport the catalogue is beside
  it

#### Scenario: a first run offers setup as the tab's content

- **WHEN** an Admin opens the Automations tab of a project with no Automations
- **THEN** the workflow setup surface is rendered inline on the tab, and the toolbar offers no second
  way to reach it

#### Scenario: the tools open over the tab

- **WHEN** an Admin chooses to try a prompt, or to set up a workflow from the repository, on a
  project that already has Automations
- **THEN** the surface opens over the tab and the tab's own content keeps its position

#### Scenario: a narrow viewport shows the unchained Automations as their own group

- **WHEN** the tab renders at a viewport too narrow for the catalogue to sit beside the workflow
- **THEN** the chain is shown first and the Automations outside it follow under their own group,
  each Automation appearing exactly once

#### Scenario: no capability is width-dependent

- **WHEN** the tab renders at a narrow viewport
- **THEN** creating an Automation, trying a prompt, and setting up from the repository are all
  reachable
