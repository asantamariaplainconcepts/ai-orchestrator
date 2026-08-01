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

**The product SHALL NOT write any starter to any repository.** It offers the content; the Admin puts
the file in their repository. No agent pass SHALL be spent producing content the product already
holds, and no repository write capability SHALL be added for this purpose.

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

### Requirement: starters are labelled by what they require

The set SHALL be presented in tiers distinguished by prerequisite, and the prerequisites SHALL be
stated on the surface rather than discovered when an agent cannot find a file.

A **portable** tier SHALL contain starters that name no document outside the project's own
repository, and each SHALL state the capability it still needs where it needs one — push access, a
vendor command line, a test command.

A **workflow** tier MAY contain starters belonging to a particular way of working, and SHALL state
what that way of working requires. A starter that reads documents a fresh repository does not have
SHALL NOT be presented as though it were portable.

#### Scenario: a prerequisite is visible before it is needed

- **WHEN** a starter requires a tool or document beyond the repository
- **THEN** that requirement is shown with the starter, not learned from a failed Run

#### Scenario: the tiers are distinguishable

- **WHEN** the set is offered
- **THEN** a starter that assumes only the repository is distinguishable from one that assumes a way
  of working

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
