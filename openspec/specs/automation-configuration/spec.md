# automation-configuration Specification

## Purpose
TBD - created by archiving change automation-configuration. Update Purpose after archive.
## Requirements
### Requirement: an Admin configures what a trigger label makes an Agent do

An Admin SHALL create an Automation on a Project consisting of a trigger label, an optional Story
state, an action from the locked catalogue (DEC-026), a runtime, a `requiresApproval` flag, and a
phase timeout defaulting to 30 minutes (BR-005). The trigger label SHALL be required and
non-empty; the state SHALL be optional and compared as the vendor's own opaque string.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, action, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** an action from the catalogue has no executing Agent yet
- **THEN** it remains selectable and the interface says it cannot run yet — a configurable
  action that silently never executes is a trap

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

Because the update endpoint is a full replace, an edit SHALL send every field, seeded from the
Automation's stored values — a field the form omits SHALL NOT be silently reset to its default. The
timeout SHALL therefore be a visible field in both modes: a value resent on the Admin's behalf is one
they are entitled to see.

Changing the action to one that reads no document SHALL clear the document name, because a value no
visible control can reach is a value the Admin cannot manage.

An edit SHALL NOT change whether the Automation is enabled.

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

### Requirement: a project can be given the framework's default Automations in one action

An Admin SHALL be able to apply a default set of Automations to a project in a single action. The
set SHALL be defined in code and SHALL cover **every** action in the catalogue, each with a
trigger label, a runtime, and an approval setting. Where two defaults compose — one produces a
label another triggers on — the set SHALL wire them, so the workflow they describe works without
further configuration. Applying it SHALL be safe to repeat: the action SHALL create only what is
absent and SHALL report what already existed, separately from what it created, which SHALL also
make a later, larger set applicable to a project seeded from an earlier one. A trigger that
overlaps an existing Automation SHALL be skipped rather than failing the whole operation, and the
remaining defaults SHALL still be created.

A default whose action is irreversible SHALL require approval (DEC-040). Opening a pull request
and closing a change by merging are the two the catalogue holds today; every other default SHALL
run unattended, so the gate marks what cannot be undone rather than becoming a habit.

#### Scenario: an unconfigured project

- **WHEN** an Admin applies the defaults to a project with no Automations
- **THEN** one Automation exists per catalogue action, and the response reports them as created

#### Scenario: applied a second time

- **WHEN** an Admin applies the defaults again
- **THEN** nothing is duplicated, the response reports every default as already present, and the
  operation succeeds

#### Scenario: a project seeded before the set grew

- **WHEN** the defaults are applied to a project holding an earlier, smaller set
- **THEN** only the additions are created and the rest are reported as already handled

#### Scenario: the seeded workflow chains

- **WHEN** a seeded Automation applies a label another seeded Automation triggers on
- **THEN** that Automation triggers through ordinary matching, with no configuration by the Admin

#### Scenario: one trigger is already taken

- **WHEN** a default's trigger label is already used by an Automation with a different action
- **THEN** that one is reported as skipped, the others are created, and the existing Automation
  is left exactly as it was

#### Scenario: only the irreversible defaults ask

- **WHEN** an Admin inspects the seeded set
- **THEN** exactly the default that opens a pull request and the default that closes a change
  require approval, and every other default does not

### Requirement: the default trigger labels are ensured in the connected backlog

Applying the defaults SHALL ensure each trigger label exists in the project's connected
repository, so a Member can choose it in the vendor's own interface without any Story having been
labelled first. Ensuring labels SHALL NOT be a precondition for creating the Automations: a
vendor failure SHALL be reported naming the labels affected, while the Automations remain
created. A project with no Connector SHALL still receive its Automations, and the response SHALL
say that no labels could be ensured.

#### Scenario: labels become selectable at the vendor

- **WHEN** the defaults are applied to a project connected to a repository with none of them
- **THEN** each trigger label exists in that repository, without any Story having been modified

#### Scenario: the vendor refuses

- **WHEN** the vendor rejects a label
- **THEN** the Automations are still created, the failure names the label, and the Story mirror
  is unchanged

#### Scenario: no Connector

- **WHEN** the defaults are applied to a project with no Connector
- **THEN** the Automations are created and the response states that labels could not be ensured

### Requirement: grill Automations carry their rubric path and ready label

An Automation whose action is the grill SHALL carry an optional rubric path, defaulting in code
to the framework's convention (`docs/process/definition-of-ready.md`). The label the grill applies
when the bar is met SHALL be the Automation's output label, defaulting in code to
`ready-for-proposal` for the grill action only. The portal SHALL offer the rubric path only for
the grill action, and the output label for every action.

#### Scenario: defaults apply when unset

- **WHEN** a grill Automation is created without either setting
- **THEN** execution uses the framework defaults

#### Scenario: the settings are the Admin's

- **WHEN** a rubric path or output label is set
- **THEN** execution uses exactly those values

#### Scenario: a grill configured before the field widened

- **WHEN** a grill Automation configured with a ready label is read after the change
- **THEN** that value is its output label and its behaviour is unchanged

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

An Automation SHALL carry an optional output label, applied to the Story through the licensed
label write when a Run of that Automation succeeds, and applied at no other time. An unset output
label SHALL mean the Automation ends silently. Saving an Automation whose output label equals its
own trigger label SHALL be refused, naming the reason.

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

### Requirement: an Admin shapes the pipeline on a canvas

The portal SHALL present a project's Automations as two named things: a **catalogue** of every
Automation the project has, and a **workflow** — the path they form. An Automation SHALL belong to
the workflow exactly when it has an edge: it hands work to another, or another hands work to it.
Every other Automation SHALL appear in the catalogue only, and its absence from the workflow SHALL
NOT be an error or an omission — it is a trigger that acts on its own when somebody applies its
label. Membership SHALL be derived from the edges and SHALL NOT be stored.

The catalogue SHALL show each Automation's trigger label, action, runtime and whether it is
enabled, and SHALL offer every action already available: create, edit, disable, re-enable, delete.

The workflow SHALL render each Automation as a node, with an edge exactly where one Automation's
output label equals another's trigger label. The graph and its layout SHALL be derived from the
Automations themselves, with nothing about the picture stored. At a wide viewport the chain SHALL
be a single row read left to right, scrolling horizontally **within its own container** when it
exceeds it, and SHALL NOT wrap onto a second line; the page itself SHALL NOT scroll sideways. At a
narrow viewport it SHALL read top to bottom instead. The workflow SHALL state how many steps it has
and how many times it stops for a person, both derived.

The workflow SHALL let an Admin connect one Automation to another, which sets the upstream output
label to the downstream trigger label, and disconnect them, which clears it. It SHALL show where a
human is required — a broken chain between two Automations, and an Automation that requires
approval — and let the Admin add or remove that requirement in both positions. Every change
available by dragging SHALL be available from an explicit control at every viewport width. All
changes SHALL go through the ordinary Automation update, so its refusals apply unchanged.

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

