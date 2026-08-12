## REMOVED Requirements

### Requirement: an Admin configures what a trigger label makes an Agent do

**Reason**: the requirement described a form with an approval control, and its scenario "approval
says what it does" asserted the sentence that control had to state. There is no such control after
DEC-067: a step that stops for a person marks the hold among its output labels, and the wait moved
from inside the Run to the Story it finishes with. The requirement is restated below rather than
left describing a field the form no longer has — the same treatment `azure-devops-connector` gave a
requirement whose scenario had been overtaken by a decision.

**Migration**: an Admin who wants the flow to stop configures the Automation to mark the hold, in
the marks field the form already offers. Everything else the requirement said — the one action, the
required prompt name, the picker, the three questions, the restatement sentence, the transition and
marks fields — is unchanged and carried into the restatement verbatim.

## ADDED Requirements

### Requirement: an Admin configures what a trigger label makes an Agent do, and there is no approval to give

An Admin SHALL configure, per Project, which trigger label starts a Run, on which runtime and with
which timeout. Every field SHALL be bounded, and the refusal for an unbounded or unparseable value
SHALL name the field.

**There SHALL be no approval control.** An Automation does not pause inside itself for a human;
where the flow must wait, the Automation applies the hold on success and the next Automation is
refused until a person clears it (see *story-hold*). The form SHALL therefore offer no approval
flag, the request it produces SHALL carry none, and the stored Automation SHALL hold no such
property — a caller that sends one configures nothing.

**The action is one and only one: run the repository's prompt (#162).** Any other action SHALL be
refused with the ordinary unknown-action refusal. What an Automation *does* is decided by the prompt
file in the project's repository, read live at Run time, and not by anything chosen here.

**Naming that prompt SHALL be required.** With one action, an Automation that names no prompt can
never run, and a configurable thing that silently never executes is the trap this spec already
forbids. The refusal SHALL land where the Admin is looking — at save — rather than at the first Run,
in front of somebody who did not configure it.

**The prompt-name input SHALL be a picker that also accepts a freely typed value (#215).** It SHALL
offer the `.md` files currently in the project's prompts directory, read live from the repository's
default branch through the Connector at the moment the field is used — never cached and never
mirrored. Names SHALL be offered relative to the prompts directory, one directory level deep,
because that is what the Automation stores. Free text SHALL remain accepted, because a prompt may
be arriving in a pending pull request. When the directory does not exist, the listing fails, or the
project has no Connector, the field SHALL degrade to the plain text input with the reason readable —
configuration SHALL never be blocked by discovery.

The form SHALL ask for the claimed transition and for the marks as **two separate fields**, because
they are two separate things (see *a transition and a mark are different things*). The transition's
field SHALL be **single-valued**, so no form on this surface can express a second one, and the marks'
field SHALL be a set offered whichever answer the hand-on question holds — an Automation that claims no
transition may still mark the Story.

Both SHALL be pickers that also accept a freely typed value. They SHALL suggest the trigger labels of
the project's **other enabled** Automations, because naming the next stage of a sequential workflow is
what the transition's field is most often for, and SHALL NOT suggest the Automation's own trigger.
Accepting free text SHALL remain possible: a stage may not exist yet, and a mark may name nothing that
triggers.

A suggestion SHALL be a convenience only: every refusal SHALL also be enforced where the Automation
is saved, so a caller that does not use the portal is refused identically — and the prompt picker
SHALL follow the same rule: a name not among the suggestions saves exactly as a typed one, and the
missing-prompt refusal stays where it always was, at Run time (#150).

**The form SHALL be organised as three questions, in the Automation's own execution order:** when it
fires, what it does, and what happens after. The grouping SHALL be visible, so that a reader who has
not been told the model can acquire it from the form rather than needing it beforehand.

**The form SHALL restate its own configuration in prose as it is filled**, in the vocabulary the
workflow surface uses. That restatement SHALL NOT be a second validation channel: an incomplete
configuration SHALL yield an incomplete sentence naming what is missing, and the field-level
refusals SHALL remain the only place a value is rejected.

**Ending the chain SHALL be an answer, not an absence.** The Admin SHALL choose between handing on
and stopping; choosing to stop SHALL store what an empty label set stores today, so nothing
downstream learns a new concept. A label named and then abandoned by choosing to stop SHALL NOT be
sent: the later, explicit answer wins over the field.

Regrouping SHALL NOT change what is sent: for every configuration the form can express, the request
SHALL be identical to the one the ungrouped form produced.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, action and runtime
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: no approval flag is offered or stored

- **WHEN** an Admin opens the create or edit form
- **THEN** no approval control is present
- **AND WHEN** a caller sends a request carrying an approval flag
- **THEN** the Automation is stored with no such property, and nothing about its execution differs

#### Scenario: an action with no implementation yet

- **WHEN** an action is offered that no Agent can execute
- **THEN** it is not offered at all — the catalogue is one action, and a selectable action that
  cannot run has no way to exist

#### Scenario: the form offers what is wirable

- **WHEN** an Admin opens the next-stage input on a project with other enabled Automations
- **THEN** their trigger labels are offered, and this Automation's own trigger is not

#### Scenario: the claim is one field and the marks are another

- **WHEN** an Admin opens the form
- **THEN** the claimed transition has one single-valued field, the marks have a field of their own, and
  there is no way to name a second transition

#### Scenario: a disabled Automation is not offered

- **WHEN** another Automation in the project is disabled
- **THEN** its trigger is not among the suggestions

#### Scenario: a label nobody listens to is still allowed

- **WHEN** an Admin types a label that matches no trigger
- **THEN** it is accepted and applied on success like any other

#### Scenario: an Automation must name its prompt

- **WHEN** an Automation is saved naming no prompt
- **THEN** it is refused, because with one action it could never run

#### Scenario: the vocabulary is one word

- **WHEN** an Automation is saved naming any of the retired actions
- **THEN** it is refused, because they are not actions any more

#### Scenario: the prompt field offers what the repository holds

- **WHEN** an Admin opens the prompt-name input on a project whose prompts directory holds markdown
  files on the default branch
- **THEN** those file names are offered, relative to the prompts directory

#### Scenario: a prompt not yet merged can still be named

- **WHEN** an Admin types a prompt name that the listing did not offer
- **THEN** it saves exactly as a listed one would, and a Run finding no such file fails with the
  resolved path, as #150 specified

#### Scenario: discovery failure does not block configuration

- **WHEN** the prompts directory does not exist, the listing fails, or the project has no Connector
- **THEN** the field renders as the plain text input with the reason readable, and the Automation
  can still be saved

#### Scenario: the form teaches its own model

- **WHEN** an Admin opens the New Automation form
- **THEN** its fields are grouped as when-it-fires, what-it-does and what-happens-after, and the
  grouping is visible without opening documentation

#### Scenario: a mistake is visible before saving

- **WHEN** an Admin fills the form
- **THEN** a sentence restates the configuration as prose and updates as the fields change

#### Scenario: an incomplete form is not an error

- **WHEN** required fields are still empty
- **THEN** the sentence names what is missing, and no rejection is raised outside the field-level
  refusals that already exist

#### Scenario: stopping the chain is chosen, not left blank

- **WHEN** an Admin names a label and then chooses to stop rather than hand on
- **THEN** the label control is not offered, and the Automation is stored with the empty label set

## MODIFIED Requirements

### Requirement: an Admin edits, disables and re-enables an Automation

An Admin SHALL be able to change an Automation's trigger, action, runtime and timeout, and to
disable and re-enable it. An edit SHALL face the same BR-003 overlap validation
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
