# automation-configuration Specification

## Purpose
TBD - created by archiving change automation-configuration. Update Purpose after archive.
## Requirements
### Requirement: an Admin configures what a trigger label makes an Agent do

An Admin SHALL configure, per Project, which trigger label starts a Run, on which runtime, with which
timeout and whether the plan requires approval. Every field SHALL be bounded, and the refusal for an
unbounded or unparseable value SHALL name the field.

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

The output labels input SHALL be a picker that also accepts a freely typed value. It SHALL suggest
the trigger labels of the project's **other enabled** Automations, because wiring the next step of a
sequential workflow is what this field is most often for, and SHALL NOT suggest the Automation's own
trigger. Accepting free text SHALL remain possible, because a label may be a mark that triggers
nothing, or a trigger that does not exist yet.

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

**The approval control SHALL state its consequence** — that the Agent plans, stops, and waits for a
human, and that nothing executes until someone approves — and SHALL sit with the execution it gates
rather than with the form's submission.

**Ending the chain SHALL be an answer, not an absence.** The Admin SHALL choose between handing on
and stopping; choosing to stop SHALL store what an empty label set stores today, so nothing
downstream learns a new concept. A label named and then abandoned by choosing to stop SHALL NOT be
sent: the later, explicit answer wins over the field.

Regrouping SHALL NOT change what is sent: for every configuration the form can express, the request
SHALL be identical to the one the ungrouped form produced.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, action, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** an action is offered that no Agent can execute
- **THEN** it is not offered at all — the catalogue is one action, and a selectable action that
  cannot run has no way to exist

#### Scenario: the form offers what is wirable

- **WHEN** an Admin opens the output labels input on a project with other enabled Automations
- **THEN** their trigger labels are offered, and this Automation's own trigger is not

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

#### Scenario: approval says what it does

- **WHEN** an Admin reads the approval control
- **THEN** it states that the Agent plans, stops and waits, and that nothing executes until someone
  approves

#### Scenario: stopping the chain is chosen, not left blank

- **WHEN** an Admin names a label and then chooses to stop rather than hand on
- **THEN** the label control is not offered, and the Automation is stored with the empty label set
  that has always meant this

### Requirement: overlapping triggers are rejected when saved

Saving an Automation whose trigger could match a Story that an existing **enabled** Automation in
the same Project could also match SHALL fail with a domain error naming the conflicting
Automation (BR-003, DEC-033). Two triggers overlap when they share a label and either share a
state or one places no state constraint; different labels never overlap; disabled Automations are
ignored for this purpose.

**Two triggers share a label when the vendor would consider them the same label.** Comparison SHALL
be case-insensitive, for labels and for states, and the *same* comparison SHALL be used when a Story
is matched against a trigger — so a differently-cased Automation cannot be accepted and then silently
never fire.

**An exact duplicate SHALL be refused whether or not either Automation is enabled.** Two rows with the
same label and the same state are the same trigger; permitting them means the conflict surfaces later,
at enable time, to somebody who did not create it. This is distinct from subsumption, which remains
enabled-only because a disabled Automation matches nothing.

**Uniqueness SHALL be enforced by the schema, not only by the handler.** Two concurrent saves of the
same trigger SHALL result in one row and a refusal, and that refusal SHALL be the same domain error an
in-memory conflict produces — never an internal error. The constraint SHALL treat an absent state as a
value, so that two triggers with the same label and no state cannot both exist.

#### Scenario: the same label and state twice

- **WHEN** an Admin saves a second Automation with a trigger already used by an enabled one
- **THEN** the save fails and the response names the Automation it collides with

#### Scenario: the same label with different states

- **WHEN** two Automations use one label but different Story states
- **THEN** both save — no Story carries two states at once, so neither can match both

#### Scenario: a broad trigger subsumes a narrow one

- **WHEN** an Automation triggers on a label with no state constraint, and another uses the same
  label with a state
- **THEN** the save fails: a Story in that state would match both, which is the ambiguity BR-003
  exists to prevent

#### Scenario: the same label in different case

- **WHEN** an Automation triggers on `AI:Implement` and another is saved on `ai:implement`
- **THEN** the save fails, because the vendor would treat those as one label

#### Scenario: a differently-cased trigger still fires

- **WHEN** a Story is labelled `ai:implement` and an enabled Automation triggers on `AI:Implement`
- **THEN** matching fires that Automation, because the matcher compares labels as the guard does

#### Scenario: a disabled exact duplicate

- **WHEN** an Automation with a label and state exists and is disabled, and another with the same
  label and state is saved
- **THEN** the save fails, because two rows with one trigger are the same trigger regardless of
  whether either is enabled

#### Scenario: a disabled broad sibling does not subsume

- **WHEN** a disabled Automation places no state constraint on a label, and an enabled one with that
  label and a concrete state is saved
- **THEN** it is allowed, because a disabled Automation matches nothing — and enabling the disabled
  one afterwards is refused

#### Scenario: two saves at once

- **WHEN** two identical trigger saves are processed concurrently
- **THEN** exactly one Automation exists afterwards and the other caller receives the same refusal an
  in-memory conflict would have produced

### Requirement: an Automation never carries a credential

An Automation SHALL reference a runtime by name only. No token, key or connection string SHALL be
stored on it or returned by any endpoint that exposes it (BR-010).

#### Scenario: reading a project's Automations

- **WHEN** the Automations of a Project are fetched
- **THEN** the response contains configuration only — no credential in any field

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

### Requirement: an Automation that no Run has used can be deleted

An Admin SHALL be able to delete an Automation, and the deletion SHALL be refused when any Run —
active or terminal — references it. The refusal SHALL state how many Runs do and SHALL point at
disabling as the alternative, and the Automation SHALL be unchanged. A deleted Automation SHALL
disappear from listings and from matching, and its trigger SHALL become available to a new
Automation. Deletion SHALL be scoped to the project: an Automation belonging to another project
SHALL be reported as not found.

#### Scenario: never used

- **WHEN** an Admin deletes an Automation no Run has ever referenced
- **THEN** it is gone from the listing and from matching

#### Scenario: used, so refused

- **WHEN** an Admin deletes an Automation with Runs
- **THEN** the refusal names how many Runs reference it, suggests disabling, and changes nothing

#### Scenario: an in-flight Run is unharmed

- **WHEN** deletion is refused because a Run is active
- **THEN** that Run still resolves its Automation and completes normally

#### Scenario: the trigger is freed

- **WHEN** a new Automation is created with a deleted one's trigger
- **THEN** it is accepted — the overlap it would have collided with no longer exists

#### Scenario: another project's Automation

- **WHEN** deletion names an Automation belonging to a different project
- **THEN** it is reported as not found

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

#### Scenario: the chain does not wrap

- **WHEN** a chain longer than the viewport renders at any width
- **THEN** it reads top to bottom without wrapping, and neither the chain's own container nor the
  page scrolls sideways

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

### Requirement: an Admin places the human review by dragging it where the person belongs

The portal SHALL let an Admin drag a human-review block from the catalogue and drop it into a gap
between two steps of the workflow. Dropping it SHALL clear the **preceding** step's output label, so
the chain stops there and a person reviews what that step produced before the work continues.

The block SHALL NOT change any step's approval requirement. Reviewing what a step produced and
approving what a step is about to do are two different waits with two different run-time behaviours
(BR-007), and the workflow SHALL keep them distinguishable: the block is the first, and the control
on the step's own card remains the second.

The block SHALL NOT be a persisted entity, and its position SHALL NOT be stored — the absent output
label is the fact.

Removing the block SHALL restore the preceding step's output label where the step drawn after the
gap makes the destination unambiguous. Where nothing follows the gap, removal SHALL require naming a
destination, because a label names one and an absence does not.

Moving the block from one gap to another SHALL clear the new gap's preceding output label **before**
restoring the old one, so that an interrupted move leaves a review in both places and never in
neither.

Every change SHALL go through the ordinary Automation update, so its refusals apply unchanged; a
refused change SHALL return the workflow to what is stored and SHALL show the reason given. While a
drag is in progress the gaps that can accept the block SHALL be marked, and a gap that would be
refused SHALL NOT appear as a target.

Dragging SHALL remain sugar: the controls that break and restore a connection SHALL stay available at
every viewport width, and below the width at which the flow reads left to right, dragging SHALL NOT
be offered at all.

#### Scenario: placing a person after a step

- **WHEN** an Admin drops the human block into the gap after a step that hands work on
- **THEN** that step's output label is cleared, the block is drawn in that gap, and no step's
  approval requirement changes

#### Scenario: the two waits stay different things

- **WHEN** a step requires approval and the gap after it holds no block
- **THEN** the card shows the approval requirement and the gap shows a connected chain, and neither
  is drawn as the other

#### Scenario: removing the person where the destination is known

- **WHEN** an Admin removes a block from a gap that has a step drawn after it
- **THEN** the preceding step's output label becomes that step's trigger label and the chain closes

#### Scenario: removing the person with nothing after the gap

- **WHEN** an Admin removes a block from the end of a chain
- **THEN** they are asked which Automation the work should be handed to, because an absence names no
  destination

#### Scenario: moving the person along the flow

- **WHEN** an Admin drags a placed block from one gap to another
- **THEN** the step before the new gap stops handing work on and the step before the old gap resumes

#### Scenario: an interrupted move fails safe

- **WHEN** the new gap has been broken and the old one has not yet been reconnected
- **THEN** a person is asked in both places, and no work continues unreviewed

#### Scenario: a refused change changes nothing

- **WHEN** a change would be refused by the ordinary Automation update
- **THEN** the workflow returns to what is stored and the reason is shown

#### Scenario: an impossible gap is not a target

- **WHEN** a drag is in progress
- **THEN** the gaps that can accept the block are marked and one that would be refused is not
  offered

#### Scenario: the same change without dragging

- **WHEN** an Admin uses the explicit break or restore control instead
- **THEN** the output label changes identically, at every viewport width

#### Scenario: no dragging where the flow is vertical

- **WHEN** the workflow renders below the width at which it reads left to right
- **THEN** dragging is not offered and every change remains reachable

### Requirement: a phase timeout is bounded, and the infrastructure honours the bound

An Automation's phase timeout SHALL be configurable by an Admin up to a ceiling the product states,
and a value above that ceiling SHALL be refused at save naming it. The ceiling exists so the platform
budget that hosts a phase can be provably sufficient: without an upper bound, "configurable" means
"configurable up to whatever the infrastructure happens to allow", which is not a promise the product
can keep.

The provisioned execution budget SHALL be at least the ceiling plus a margin for a worker to finish
writing its outcome after a phase ends. The ceiling, the provisioned budget and the business rule
SHALL each carry a reference to the other two, because no automated check can span a code constant, an
infrastructure value and a documented rule.

#### Scenario: a timeout above the ceiling

- **WHEN** an Admin saves an Automation whose phase timeout exceeds the ceiling
- **THEN** the save is refused, naming the ceiling, and nothing is stored

#### Scenario: a timeout at the ceiling

- **WHEN** an Admin saves an Automation whose phase timeout equals the ceiling
- **THEN** it is accepted

#### Scenario: the provisioned budget covers the ceiling

- **WHEN** the deployed execution budget is compared with the ceiling
- **THEN** it is at least the ceiling plus a drain margin

### Requirement: a new project is offered a starter set of prompts

The portal SHALL offer a versioned set of starter prompts, so that a project with an empty prompts
directory has something to take rather than only a refusal naming a path that does not exist.

Every starter SHALL be presented with its purpose in one sentence, the filename it is meant to be
saved as, and its content. The filename and the directory it belongs in SHALL be the ones the Run
path already resolves, so taking a starter and creating an Automation that names it is two steps and
no translation.

Every starter SHALL carry frontmatter of the kind the Run path already strips, so a file taken from
the set behaves identically whether this product runs it or a local agent runner does.

**Offering SHALL write nothing.** The offer presents content; no agent pass SHALL be spent
producing content the product already holds. Writing happens only through the explicit install
action (#214), which is its own requirement below — it spends no agent pass either, and it never
touches the default branch.

#### Scenario: an empty project has something to take

- **WHEN** an Admin looks at a project with no prompts
- **THEN** the starter set is offered, each entry with its purpose, its filename and its content

#### Scenario: a starter is a prompt an Automation can name

- **WHEN** an Admin saves a starter under the filename offered, in the project's prompts directory
- **THEN** an Automation naming that file resolves it, with no renaming or path translation

#### Scenario: a starter behaves the same outside this product

- **WHEN** a starter's frontmatter is stripped as the Run path strips it
- **THEN** a non-empty body remains

#### Scenario: offering writes nothing

- **WHEN** the starter set is offered
- **THEN** nothing is written to the project's repository and no agent pass is run

### Requirement: a starter can be installed as a draft pull request

Where a project has a Connector, each starter SHALL offer an **install** action that writes the
starter's bytes at `<prompts directory>/<filename>` on a starter-scoped branch and opens a
**draft pull request** — through the same workspace publish pipeline Runs use (clone with the PAT
resolved by name at use, BR-010; commit; push; PR), with no agent pass spent. The default branch
SHALL never be written; a human merges.

The opened PR's URL SHALL be shown where the install was asked for, stating that review is the
human's next step. Each pipeline failure SHALL name its stage — clone, push, or PR — in the same
voice as implement's refusals.

A starter already present at its target path on the default branch SHALL refuse install naming the
path — an existing file always wins, now enforced at the moment it matters rather than holding by
construction. Where the project has no Connector, install SHALL NOT be offered; the offer itself
remains usable as today.

#### Scenario: one click, one reviewable PR

- **WHEN** an Admin installs a starter on a project with a Connector
- **THEN** a branch carries the file at the prompts directory under the starter's filename, a
  draft pull request exists, and its URL is shown on the starter

#### Scenario: the default branch is never written

- **WHEN** any install completes
- **THEN** the default branch is unchanged until a human merges the pull request

#### Scenario: already present refuses by name

- **WHEN** the starter's target path already exists on the default branch
- **THEN** install is refused naming that path, and no branch or PR is created

#### Scenario: a stage failure says which stage

- **WHEN** the clone, the push, or the PR creation fails
- **THEN** the refusal names that stage and repeats the vendor's reason

#### Scenario: no Connector, no install

- **WHEN** a project has no Connector
- **THEN** the starter is offered without the install action

### Requirement: starters are labelled by what they require

The set SHALL be presented in tiers distinguished by prerequisite, and the prerequisites SHALL be
stated on the surface rather than discovered when an agent cannot find a file.

A tier that names no document outside the project's own repository SHALL declare no prerequisite, and
each of its starters SHALL state the capability it still needs where it needs one — push access, a
vendor command line, a test command.

A tier MAY contain starters belonging to a particular way of working, and SHALL state what that way of
working requires. A starter that reads documents a fresh repository does not have SHALL NOT be
presented as though it assumed only the repository.

The tiering SHALL describe the catalogue as it is, and SHALL NOT require any particular tier to exist.
A catalogue of one tier is lawful: what the requirement fixes is that a tier's assumptions are
declared, not how many tiers ship.

#### Scenario: a prerequisite is visible before it is needed

- **WHEN** a starter requires a tool or document beyond the repository
- **THEN** that requirement is shown with the starter, not learned from a failed Run

#### Scenario: the tiers are distinguishable

- **WHEN** the set is offered
- **THEN** a starter that assumes only the repository is distinguishable from one that assumes a way of
  working

#### Scenario: one tier is a lawful catalogue

- **WHEN** the catalogue ships a single tier that declares a prerequisite
- **THEN** the set is offered with that tier's requirement stated, and nothing is presented as
  assuming only the repository

### Requirement: a starter that a project already has is reported, never replaced

Where a project has a Connector, the offer SHALL report which starters already exist at their target
path in that project's repository, so an Admin is told before copying rather than after overwriting.

An existing file SHALL always win. Since nothing is written, this holds by construction — the
reporting is what makes it useful rather than merely true.

Where a project has no Connector there is nothing to read, and the offer SHALL say so and remain
usable: looking at the set before configuring a Connector is an ordinary first step, not an error.

#### Scenario: an existing file is reported

- **WHEN** the project's repository already contains a file at a starter's target path
- **THEN** that starter is marked as already present, and nothing is written

#### Scenario: no Connector is an ordinary state

- **WHEN** the project has no Connector
- **THEN** the set is still offered, and the presence of each starter reads as unknown rather than as
  absent or as an error

### Requirement: every shipped starter loads and has a body

Every starter in the set SHALL be covered by a test asserting that it loads and that a non-empty body
remains once frontmatter is stripped by the same routine the Run path uses. A starter prompt that
fails to load is worse than none, because it is offered as working.

#### Scenario: the shipped bytes are the tested bytes

- **WHEN** the test suite runs
- **THEN** every starter the endpoint would serve is loaded and asserted to have a body after
  frontmatter is stripped

### Requirement: bulk creation converges instead of colliding

The one bulk-creation path (default-automations setup) SHALL reuse the single-Automation
creation semantics — the same validation, the same BR-003 normalised-trigger comparison — and
SHALL treat losing a uniqueness race as "already exists, skipped", never as a failure surfaced
to the Admin. Convergence is the promise: after the action, the wired set exists exactly once
regardless of what existed before or ran concurrently.

#### Scenario: a concurrent duplicate is a skip, not an error

- **WHEN** two set-up-defaults requests race on one project
- **THEN** both answer successfully, each trigger exists exactly once, and the union of the two
  responses' created+skipped lists covers the whole wired set

### Requirement: setting a project up adopts the pipeline it already has

Setting up a project's Automations SHALL begin by finding the prompt files the repository already
carries, and SHALL wire Automations to those. Installing a starter SHALL happen only for a
pipeline step the repository has no file for.

A repository that already carries its own pipeline SHALL NOT receive a second copy of one. The
reason is the reason DEC-048 already gives for reading the grill's rubric from the project: a
product-wide version of a team's own document imposes one team's standards on every repository it
touches, and the copy is the weaker of the two.

That comparison presumes there are two. Where a repository has **no** file at a path a consented tier
would write, there is no team's own version to be weaker than, and the product MAY seed one — revised by
DEC-064 and recorded in `docs/adr/0012-a-seeded-document-is-the-projects-own.md`. The rule above is
unchanged in the case it was written about: an existing file still always wins.

**Discovery SHALL propose, never choose.** The conventional locations SHALL be searched — the
Connector's configured directory first, then `ai/prompts`, then `.claude/commands` and its
immediate subdirectories — and what was found SHALL be shown before anything is written. Where
more than one candidate holds files, all SHALL be offered and none SHALL be selected silently.
The prompts directory SHALL be saved only once a human has confirmed it.

Search SHALL go one subdirectory deep and no further: a form action that crawls a repository is a
different thing from one that looks where prompts conventionally live.

#### Scenario: a repository with its own pipeline is wired, not duplicated

- **WHEN** setting up a project whose repository already holds prompt files named for pipeline
  steps
- **THEN** Automations are wired to those files, and no starter is installed for those steps

#### Scenario: nothing is written before the human sees what was found

- **WHEN** discovery completes
- **THEN** the candidate directories and their files are reported, and no directory is saved and
  no Automation created until the choice is confirmed

#### Scenario: two candidates are both offered

- **WHEN** more than one conventional location holds prompt files
- **THEN** both are offered and neither is chosen automatically

#### Scenario: an empty repository gets the starters

- **WHEN** no conventional location holds a prompt file
- **THEN** every pipeline step is a gap, and the starter set is what fills it

#### Scenario: the seeding revision is reachable from the rule it narrows

- **WHEN** a reader finds the adoption rationale citing DEC-048
- **THEN** the decision that narrowed it, and the ADR recording why, are named there

### Requirement: a file is wired by its name, and an unrecognised one is reported

A prompt file whose name matches a pipeline step SHALL be wired to that step's trigger and
hand-off labels. A file that matches no step SHALL be reported as found and not wired, and SHALL
NOT produce an Automation.

A trigger SHALL NOT be invented from a filename. An Automation on a label nobody applies is the
configurable thing that silently never executes, which this capability already forbids elsewhere.

Where a step's trigger is already used by an enabled Automation, it SHALL be skipped and named —
the convergence rule this action already follows, so BR-003 can never fire from this path.

#### Scenario: a recognised name is wired

- **WHEN** the chosen directory holds a file named for a pipeline step
- **THEN** an Automation is created on that step's trigger, naming that file

#### Scenario: an unrecognised name is reported, not guessed

- **WHEN** the chosen directory holds a file matching no pipeline step
- **THEN** it is reported as found and not wired, and no Automation exists for it

#### Scenario: an existing trigger is skipped by name

- **WHEN** a step's trigger is already used by an enabled Automation
- **THEN** it is skipped and named in the report, and nothing collides

### Requirement: the setup reports what it did, in one place

The action SHALL report, in one summary: the directory chosen, the Automations created, the
Automations skipped and why, the files found but not wired, and the starters installed together
with the pull request carrying them.

Starters filling gaps SHALL be installed as **one** pull request rather than one per file: four
gaps are one decision, and four reviews of one decision is the cost this consolidation removes.

#### Scenario: one report, five facts

- **WHEN** the action completes
- **THEN** the summary names the directory, what was created, what was skipped and why, what was
  found but not wired, and what was installed

#### Scenario: gaps arrive as one pull request

- **WHEN** more than one step needs its starter installed
- **THEN** a single pull request carries them all

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
changes nothing is noise in a list whose whole purpose is to say what the press will do. A step whose
tier has not been consented to is such a step, so consenting to a tier SHALL bring its installable
steps into the plan, and withdrawing that consent SHALL remove them.

**No separate consent SHALL be required for installing the starters the plan names.** The rows state
which files would be written; a control asking whether to write them restates the preview, and a
confirmation of a confirmation trains a reader past both.

That rule governs the prompts a row names. It SHALL NOT be read as forbidding the tier consent, which
authorises a **different** act: writing files outside the prompt directory, at paths no row names, on
the terms of a methodology the plan does not describe. The test is whether the control asks a question
the plan has already answered — the tier consent asks one the plan cannot.

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
- **THEN** no separate control asks whether to install the starter files its rows name — the tier
  consent above governs a different act, at paths no row names

#### Scenario: a step nothing would happen for is not offered

- **WHEN** a step has no file in the chosen directory and no starter can be installed for it
- **THEN** it does not appear as a row in the plan

#### Scenario: consenting to a tier grows the plan

- **WHEN** a tier that declares a prerequisite is consented to
- **THEN** its installable steps appear as rows, each stating that a starter would be installed, and
  withdrawing the consent removes them again

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

### Requirement: a tier that writes beyond prompts is consented to by name

Where a starter tier declares a prerequisite, the setup card SHALL present that tier with a consent
control that is **off** by default, and the action SHALL install that tier's prompts only where the
caller named it.

The control SHALL state its consequence before it is given: the tier's prerequisite text, and the paths
a press with it on would write — both the prompts and the files outside the prompt directory. The
statement SHALL be computed from the discovery already performed, and SHALL NOT cost a vendor read per
tier.

The control SHALL remain reachable when the plan is empty. A repository with no pipeline and no consent
has no rows at all, and a consent that lived inside the row list would be unreachable in exactly the
case it exists for.

Consent SHALL be per-invocation and SHALL NOT be persisted. Reopening the card SHALL show the control
off, whatever an earlier press consented to.

Consent SHALL compare tier identifiers exactly as declared in the catalogue.

#### Scenario: the control is off until it is turned on

- **WHEN** the setup card shows a tier that declares a prerequisite
- **THEN** its consent control is off, and none of that tier's steps is selected

#### Scenario: the consent says what it will write

- **WHEN** the consent control is shown
- **THEN** the tier's prerequisite text and the paths a press would write are shown with it, computed
  without an additional vendor read

#### Scenario: the consent is reachable with an empty plan

- **WHEN** the chosen directory holds no file for any step, so the plan has no rows
- **THEN** the consent control is still shown and can still be turned on

#### Scenario: consent is not remembered

- **WHEN** a press consented to a tier and the card is opened again
- **THEN** the control is off

#### Scenario: an unconsented tier installs nothing

- **WHEN** the action runs with no tier named
- **THEN** no starter is installed, no branch is created, and no pull request is opened

### Requirement: a step from an opt-in tier is adopted, and installed only on consent

A pipeline step belonging to a starter tier that declares a prerequisite SHALL be recognised and wired
when the repository already holds its file.

Where the repository does not hold its file, the step SHALL be installed **only where the caller has
consented to its tier by name**, and SHALL NOT be installed otherwise.

Reading a file a team wrote is not the same act as writing one they did not ask for. A tier that
declares what it assumes is opt-in by construction, and a button that installed it *unprompted* would
push a methodology into a repository whose team never chose it — the failure the tiering was introduced
to prevent. A consent that is off by default, names the tier, and states the paths it will write is not
unprompted; it is the prompt.

#### Scenario: an opt-in step with a file is wired

- **WHEN** the chosen directory holds a file named for a step from a tier that declares a prerequisite
- **THEN** an Automation is created on that step's trigger, naming that file

#### Scenario: an opt-in step with no file and no consent is not installed

- **WHEN** a step from a tier that declares a prerequisite has no file in the chosen directory and its
  tier was not consented to
- **THEN** no starter is written for it and it does not appear in any pull request

#### Scenario: an opt-in step with no file is installed once its tier is consented to

- **WHEN** a step from a tier that declares a prerequisite has no file in the chosen directory and its
  tier was consented to
- **THEN** its starter is written, an Automation is created on its trigger naming the installed file,
  and both arrive in one draft pull request

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

### Requirement: a Project resolves its Automations' runtime by default

A Project SHALL carry runtime settings an Admin edits from Settings: a **default runtime**, and an
optional **credential secret name per runtime**. Names SHALL be stored and values never (BR-010),
and the credential names SHALL be readable only where the rest of the project's configuration is
(BR-009) — never on a surface a Member reads.

An Automation's runtime SHALL be optional: unset means the Project default, resolved **at
execution time**, so changing the default changes future Runs without touching any Automation. An
Automation naming a runtime explicitly SHALL win over the default. Existing Automations keep the
explicit runtime they already carry — the migration SHALL change no behaviour.

The form SHALL offer "Project default" as the runtime's first option and SHALL say what it
currently resolves to, so choosing it is informed rather than blind. An update that sends no
runtime SHALL mean the default — the same absent-versus-set discipline the setup action's
selections already use.

#### Scenario: the default applies at execution time

- **WHEN** an Automation with no explicit runtime fires after the Admin changes the Project
  default
- **THEN** the new Run resolves to the new default, and no Automation row changed

#### Scenario: an explicit runtime wins

- **WHEN** an Automation naming a runtime fires on a Project whose default differs
- **THEN** the Run executes on the Automation's runtime

#### Scenario: existing Automations survive the migration unchanged

- **WHEN** the schema change lands on a project with Automations
- **THEN** every existing Automation keeps its explicit runtime and its Runs behave as before

#### Scenario: credential names are stored, values never

- **WHEN** an Admin sets a credential name for a runtime and the settings are read back
- **THEN** the name is present, no secret value appears anywhere, and a non-Admin read does not
  include the names


### Requirement: an Automation names the model its Runs think with

An Automation SHALL carry an **optional model** beside its optional runtime. Unset means the
deployment's, resolved at execution time, so changing the deployment default changes future Runs
without touching any Automation. An Automation naming a model explicitly SHALL win over it.
Existing Automations SHALL keep behaving exactly as they do — the migration SHALL change nothing.

The form SHALL offer the model as a choice whose options come from the **selected runtime**, and
the two fields SHALL stay consistent: changing the runtime SHALL re-ask what models are available
rather than leaving an offer that belongs to the previous one.

The form SHALL distinguish three states a chooser can be in, because they mean different things to
the person reading it:

- the runtime's models were obtained and are offered;
- the runtime cannot be asked and none are declared for it in configuration;
- the machine could not be asked right now.

In every one of the three, a written value SHALL remain acceptable and leaving the field empty to
inherit SHALL remain valid. An unasked machine SHALL NOT be rendered as a runtime with no models.

#### Scenario: the deployment default applies at execution time

- **WHEN** an Automation with no explicit model fires after the deployment default changes
- **THEN** the new Run resolves to the new default, and no Automation row changed

#### Scenario: an explicit model wins

- **WHEN** an Automation naming a model fires
- **THEN** the Run executes on that model rather than the deployment's

#### Scenario: existing Automations survive the migration unchanged

- **WHEN** the schema change lands on a project with Automations
- **THEN** every existing Automation carries no model and its Runs behave exactly as before

#### Scenario: changing the runtime re-asks the question

- **WHEN** an Admin switches an Automation's runtime while editing it
- **THEN** the model choices offered are the new runtime's, and a model belonging only to the
  previous runtime is not left standing as though it were still valid

#### Scenario: a machine that cannot be asked still lets the Automation be edited

- **WHEN** the machine that runs agents cannot answer while an Admin edits an Automation
- **THEN** the form says the models could not be obtained, accepts a written value, and saves —
  never an empty list implying the runtime has none, and never a form that cannot be submitted

### Requirement: the workflow's shape is edited from the picture that draws it

An Admin SHALL be able to place an Automation into the drawn chain, and take one out of it, by
direct manipulation of the picture. Every such gesture SHALL be an ordinary Automation update that
changes **output labels only** — the graph stays derived, so the picture cannot come to claim a
chain that would not fire.

Before a placement happens, the position it would take SHALL state the wiring it performs, naming
each hand-off it would create. A placement onto the end of a chain states the one hand-off it makes.

A placement that cannot be performed SHALL refuse **at the position it was offered**, naming the
rule that stops it rather than the symptom: a trigger shared with another enabled Automation
(BR-003), a loop, a step handed to itself, or an edge that already exists. The refusal SHALL be
shown while the gesture is still in progress, never reported after it.

Refusing here SHALL NOT be the enforcement. The update the gesture performs is checked where every
other Automation update is checked, so a rule lives in one place and is explained in another.

Every capability reachable by direct manipulation SHALL also be reachable without it.

After any such change, the drawn chain, the catalogue's account of which Automations are wired in,
and the count of steps SHALL agree with one another.

#### Scenario: the position says what it would wire

- **WHEN** an Admin holds a standalone Automation over a position between two chained steps
- **THEN** that position names both hand-offs the placement would create, before it happens

#### Scenario: placing rewrites labels and nothing else

- **WHEN** the Automation is placed between those two steps
- **THEN** the preceding step hands to it, it hands to what followed, no other field of any
  Automation changes, and the drawn chain shows the new order

#### Scenario: placing at the end makes one hand-off

- **WHEN** an Automation is placed at the end of a chain
- **THEN** the last step hands to it, and nothing else changes

#### Scenario: a refused placement names its rule

- **WHEN** a placement would share a trigger with another enabled Automation, close a loop, hand a
  step to itself, or repeat an edge that exists
- **THEN** the position refuses while the gesture is in progress, names that rule, and performs
  nothing

#### Scenario: taking a step out returns it to the catalogue

- **WHEN** a step drawn in the chain is placed back on the catalogue
- **THEN** whatever handed to it stops doing so, and nothing is invented in its place

#### Scenario: the surfaces agree

- **WHEN** any placement completes
- **THEN** the chain, the catalogue's in-workflow marking and the step count describe the same
  workflow

### Requirement: the workflow shows the board it produces

Where a workflow has at least one chain, the Admin SHALL be shown what that workflow makes of the
Backlog: one column per step, in the order work moves through them, preceded by where Stories
start.

That view SHALL be derived from the same chains the canvas draws — never a second description of
the workflow, which could disagree with the first. It SHALL mark the columns that wait for a
person's approval, and SHALL show where the flow stops and a person carries the work onward.

It SHALL be read-only. Wiring happens on the chain; this reacts to it.

A column that a placement has just added SHALL be distinguished, so the consequence of the gesture
is visible where it landed.

#### Scenario: the columns are the workflow's steps

- **WHEN** a workflow of three chained steps is drawn
- **THEN** the preview shows where Stories start followed by those three columns, in that order

#### Scenario: a gate and a stop are both visible

- **WHEN** one step waits for approval and the chain ends without handing on
- **THEN** that column is marked as gated, and the end of the flow shows the person who carries it

#### Scenario: a new column is shown where it landed

- **WHEN** an Automation is placed into the chain
- **THEN** its column appears in the preview and is distinguished from the others
