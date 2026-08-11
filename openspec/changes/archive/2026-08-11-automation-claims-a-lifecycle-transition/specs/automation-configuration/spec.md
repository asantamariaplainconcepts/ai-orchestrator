## ADDED Requirements

### Requirement: a project's Story lifecycle is a stored, ordered list of stages

A project SHALL hold a **lifecycle**: a linear, ordered list of stage names, stored on the Project and
served to every reader by it. No surface SHALL re-derive the order from the Automations, because an
order a person can rearrange has nowhere else to live (ADR-0022, superseding DEC-053's *"membership is
derived from the edges and never stored"*).

A stage SHALL come into existence only as a consequence of an Automation claiming a transition that
names it, and SHALL NOT be removed when nothing claims it any more. Renaming a stage, removing an
unused one and seeding a lifecycle for a brand-new project are **not** offered.

Stage names SHALL be compared the way the vendor compares labels — case-insensitively (DEC-056), the
same comparison `lower("TriggerLabel")` in `20260729150023_UniqueAutomationTrigger.cs:25-30` and
`StringComparer.OrdinalIgnoreCase` in `src/modules/Runs/.../Features/Matching/StoryChangedHandler.cs:59`
already use — so one stage SHALL NOT appear twice in two spellings.

#### Scenario: the stored order is what every surface reads

- **WHEN** a project's lifecycle is `s1, s2, s3` and any surface renders the flow
- **THEN** it renders those stages in that order, taken from the project rather than recomputed from
  the Automations

#### Scenario: a stage outlives the claim that created it

- **WHEN** the Automation that claimed the transition into `s3` is deleted
- **THEN** `s3` remains a stage of the lifecycle, in its place, and nothing reports that as a problem

#### Scenario: two spellings are one stage

- **WHEN** a claim names a stage differing from an existing stage only in case
- **THEN** the existing stage is used and no second stage is created

### Requirement: an Automation claims one transition of the lifecycle

An Automation SHALL claim **at most one** transition of its project's lifecycle, and SHALL NOT be able
to claim two. The transition's **from**-stage SHALL be the Automation's trigger label — what already
makes it fire — and its **to**-stage SHALL be a single stage name, applied to the Story as the lifecycle
move when a Run of that Automation succeeds.

An Automation claiming no transition SHALL remain expressible: it acts when somebody applies its
trigger label, it MAY mark the Story, and the flow ends there. DEC-053's standalone Automation and the
last stage of a lifecycle both depend on this.

A claim SHALL name two **adjacent** stages of the project's lifecycle. Claiming a transition whose
from-stage is not yet a stage SHALL insert it immediately before the to-stage, leaving the order of
every existing stage unchanged. That invariant SHALL be enforced in exactly one place, because a rule
implemented twice eventually disagrees with itself (`Features/Automations/OverlapGuard.cs:9-13`).

At most one **enabled** Automation SHALL claim any one transition (BR-003). That refusal SHALL name the
Automation already claiming it, SHALL NOT be evaded by a difference of case (DEC-056), and SHALL leave
both Automations unchanged. Enforcement SHALL stay where it already is — the expression index
`IX_automations_trigger_identity`, the in-memory guard that can name the conflict
(`Features/Automations/OverlapGuard.cs:35-53`), and the client-side explanation — and SHALL NOT gain a
fourth home.

Branching SHALL be unrepresentable: no API, form or view SHALL accept or draw a second claimed
transition, and nothing SHALL report that an Automation hands work to more than one place.

#### Scenario: an Automation names one transition

- **WHEN** an Automation's configuration is read
- **THEN** it names one claimed transition or none, and there is no way to express a second

#### Scenario: a second claimant is refused

- **WHEN** an Admin assigns a second enabled Automation to a transition an enabled Automation already
  claims
- **THEN** the save is refused, the refusal names the Automation already claiming that transition, and
  neither Automation changed

#### Scenario: case does not evade the refusal

- **WHEN** the second claim differs from the first only in the case of the from-stage
- **THEN** it is refused for the same reason

#### Scenario: a claim names adjacent stages

- **WHEN** a claim would name two stages that are not adjacent in the project's lifecycle
- **THEN** it is refused, and the lifecycle is unchanged

#### Scenario: an Automation that claims nothing still works

- **WHEN** a Run of an Automation with no claimed transition succeeds
- **THEN** no lifecycle move is written, whatever marks it carries are applied, and nothing reports the
  absent transition as a problem

### Requirement: a transition and a mark are different things

An Automation's remaining output labels SHALL be **marks** and nothing else: applied to the Story
through the licensed write when a Run succeeds, carrying no meaning about the flow. Only the claimed
transition's to-stage SHALL be treated as a lifecycle move.

A label that matches no stage and no other Automation's trigger SHALL be an ordinary mark, and SHALL
NOT be reported as a dangling edge or an incomplete configuration — after this separation there is
nothing for such a warning to be about.

No boundary, column or edge SHALL be drawn for a mark.

#### Scenario: a Story carries both, and only one moves it

- **WHEN** a Run of an Automation claiming `s1 → s2` and marking the Story with `L` succeeds, where `L`
  is not a stage
- **THEN** the Story carries both `s2` and `L`, only `s2` is treated as a lifecycle move, and no
  boundary is drawn for `L`

#### Scenario: a mark is not a fault

- **WHEN** an Automation carries a mark matching no stage and no other Automation's trigger
- **THEN** nothing marks it as dangling, incomplete or misconfigured

### Requirement: an Admin arranges the whole flow where the board is read

The board SHALL be the surface on which an Admin arranges a project's flow, and SHALL render **one
column per stage** of the project's lifecycle, in the stored order, whether or not an Automation claims
the transition into it. A stage SHALL NOT be omitted for having no claimant.

An Automation SHALL be drawn on the **boundary between the two columns it claims**, and on no other
boundary.

An Admin SHALL be able to assign an Automation to a transition, to move it to another transition, and
to place an Automation on a transition whose from-stage is not yet a stage — which SHALL make that stage
the board's first column without disturbing the order of the existing stages. Moving one Automation
SHALL NOT change any other Automation's claimed transition.

Every arrangement change offered by dragging SHALL also be offered by an explicit control, at every
viewport width the board supports, and both SHALL go through the same function. This is not a
preference: an HTML5 drag cannot be performed by the end-to-end suite (`WorkflowCanvas.tsx:248-252`,
citing #110), so the shared function is what puts the logic under test at all.

Every arrangement change SHALL go through the ordinary Automation update, so BR-003's refusal applies
unchanged, and a refused change SHALL return the board to what is stored and show the reason given.

An **ACT-002 Member** SHALL be offered no control that assigns, moves or clears a claimed transition,
**and** a direct API request to change one SHALL be refused on the missing permission — the refusal
SHALL NOT rest on the absence of a button (BR-009).

The end of the flow SHALL state that the flow ends at the last stage and SHALL assert nothing about who
acts next, because BR-007 permits a Run to go straight to Executing and DEC-062 makes pushing the
Agent's own act.

#### Scenario: every stage is a column, claimed or not

- **WHEN** a project whose lifecycle is `s1 → s2 → s3`, with one Automation claiming `s1 → s2`, is
  opened on the board
- **THEN** three columns render in the order `s1, s2, s3`, and `s3` is not omitted for having no
  Automation claiming the transition into it

#### Scenario: an Automation renders on the boundary it claims

- **WHEN** an Automation claiming `s2 → s3` is rendered
- **THEN** it appears on the boundary between column `s2` and column `s3`, and on no other boundary

#### Scenario: a step can be placed first

- **WHEN** an Admin assigns an Automation to the transition `s0 → s1`, where `s1` is the first column
  and `s0` is not yet a stage
- **THEN** `s0` becomes the board's first column, that Automation renders on the `s0 → s1` boundary,
  and the order of the existing stages is unchanged

#### Scenario: the flow can be reordered

- **WHEN** an Admin assigns an Automation claiming `s1 → s2` to `s2 → s3` through the boundary's
  explicit control
- **THEN** it renders on the `s2 → s3` boundary only, the `s1 → s2` boundary reads as waiting for a
  person, and no other Automation's claimed transition changed

#### Scenario: no arrangement change is drag-only

- **WHEN** the board renders at any width it supports
- **THEN** every arrangement change offered by dragging is offered by an explicit control, and both go
  through the same function

#### Scenario: a Member cannot rearrange it

- **WHEN** a signed-in ACT-002 Member opens the board and then calls the API directly to change a
  claimed transition
- **THEN** no such control was offered, and the request is refused on the missing permission

#### Scenario: the end states the fact, not the actor

- **WHEN** the board renders the last stage of a project's lifecycle
- **THEN** the end of the flow states that the flow ends there and asserts nothing about who acts next

### Requirement: an unclaimed transition is a person's turn, not a fault

A boundary between two adjacent stages that no Automation claims SHALL be labelled as **waiting for a
person**, and SHALL carry no validation error, no "incomplete configuration" marker, and no elapsed-time
or overdue indication (BR-006 — a human wait is untimed).

A human step SHALL have no representation of its own: it SHALL NOT be a stored entity, a position, or a
flag. It is a transition nobody claims, and nothing fires until a person moves the label — which works
because a person applying a label and an Automation applying one are already the same mechanism
(`src/modules/Runs/.../Features/Execution/RunExecutor.cs:196-231` →
`src/modules/Runs/.../Features/Matching/StoryChangedHandler.cs:59`).

An unclaimed transition and a Run awaiting approval SHALL remain distinguishable (BR-007, UC-013): the
approval gate stays on the step that asks for it, and SHALL NOT be drawn as an unclaimed boundary.

#### Scenario: an unclaimed boundary waits for a person

- **WHEN** a lifecycle is `s1 → s2 → s3`, an Automation claims `s1 → s2`, and none claims `s2 → s3`
- **THEN** the `s2 → s3` boundary is labelled as waiting for a person and carries no validation error,
  no incomplete-configuration marker and no elapsed-time or overdue indication

#### Scenario: a person moving the label runs the next step

- **WHEN** a person applies the to-stage label of an unclaimed transition to a Story
- **THEN** it is matched exactly as a label an Automation applied would be, with no board-specific
  dispatch involved

#### Scenario: the two waits stay different things

- **WHEN** a step requires approval and the boundary after it is unclaimed
- **THEN** the step shows its approval gate, the boundary reads as a person's turn, and neither is
  drawn as the other

### Requirement: a configured hand-off survives the move to claimed transitions

Every hand-off configured before this change SHALL be carried across, and the carrying SHALL be
verified by counting hand-offs rather than by inspecting the schema (ADR-0001).

Each Automation SHALL come to claim the transition
`(its trigger label → the first of its output labels that matches another enabled Automation's trigger
label, compared case-insensitively)`. Each project's lifecycle SHALL hold exactly those labels, in the
order the board drew them (`src/frontend/features/backlog/KanbanBoard.tsx:110-137`). Every remaining
output label SHALL be kept as a mark, including one that matches no sibling trigger. The number of
configured hand-offs before SHALL equal the number after.

Comparison SHALL fold case. A migration reading edges case-sensitively would drop edges the canvas
draws today, because `buildChains` compares through a plain `Map` while product identity is
case-insensitive (`src/frontend/features/automations/planHandoff.ts:16-20` records this).

The migration SHALL be hand-written. A scaffolded `DropColumn` + `AddColumn` SHALL NOT be accepted:
`src/modules/Projects/.../Persistence/Migrations/20260730222648_OutputLabelSet.cs:9-19` records that the
generated form "would have silently discarded every hand-off configured in the deployment: every
workflow edge, gone, with the schema perfectly correct afterwards."

#### Scenario: every configured hand-off arrives as a claimed transition

- **WHEN** the migration is applied to a database whose Automations have hand-offs configured
- **THEN** the count of configured hand-offs after equals the count before, and each Automation claims
  the transition its output label described

#### Scenario: a differently-cased edge is not dropped

- **WHEN** an Automation's output label matches a sibling's trigger label in a different case
- **THEN** that hand-off becomes a claimed transition, exactly as an identically-cased one does

#### Scenario: an output label matching nothing becomes a mark

- **WHEN** an Automation's output label matches no other enabled Automation's trigger label
- **THEN** it is kept as a mark and no transition is claimed for it

## MODIFIED Requirements

### Requirement: an Automation can hand work on by writing a label when it succeeds

An Automation SHALL apply, when a Run of it succeeds and at no other time, its claimed transition's
**to-stage** where it has one, together with every **mark** it carries. All of them go through the
licensed label write. An Automation with no claimed transition and no marks SHALL end silently. Saving
an Automation whose to-stage or whose marks contain its own trigger label SHALL be refused, naming the
reason — the refusal SHALL apply to every member, not only to a single value.

Labels SHALL be compared the way the vendor compares them, so the marks SHALL NOT hold the same label
twice in two spellings, and a mark SHALL NOT repeat the to-stage.

Every label SHALL be attempted, and a label the vendor could not ensure SHALL be reported to the human
rather than silently skipped; the Run SHALL fail naming every label that did not land. A Run that
failed at hand-off MAY already have handed on through the labels that did land, which is what a
partially applied write means and SHALL be visible on the Story.

#### Scenario: the chain continues past a step

- **WHEN** an Automation claiming a transition has a Run that succeeds
- **THEN** the to-stage label reaches the vendor, and after reconciliation an Automation triggered by
  that label has a Run of its own

#### Scenario: silence is the default

- **WHEN** an Automation with no claimed transition and no marks has a Run that succeeds
- **THEN** no label is written

#### Scenario: only success hands work on

- **WHEN** a Run of an Automation claiming a transition fails or is cancelled
- **THEN** no label is written

#### Scenario: an Automation may not trigger itself

- **WHEN** an Automation is saved whose to-stage or whose marks equal its trigger label
- **THEN** the save is refused with the reason

#### Scenario: the transition and its marks leave together

- **WHEN** an Automation claiming a transition and carrying marks has a Run that succeeds
- **THEN** every one of them reaches the vendor through the same write path

#### Scenario: one label the vendor refuses does not hide the others

- **WHEN** one label of several cannot be ensured
- **THEN** the remaining labels are still attempted, and the Run fails naming every label that did
  not land

#### Scenario: the same label twice is one label

- **WHEN** marks are saved containing the same label in two spellings the vendor treats as one
- **THEN** it is stored once

#### Scenario: what was configured before still works

- **WHEN** an Automation configured with a single output label runs after this change
- **THEN** it behaves exactly as it did, as a claimed transition with no marks

### Requirement: the workflow shows the board it produces

Where a project's lifecycle has at least one stage, the Admin SHALL be shown, in the Automations tab,
what its catalogue makes of the Backlog: one column per stage, in the stored order, preceded by where
Stories start.

That view SHALL be the project's stored lifecycle read back — never a second description of it, which
could disagree with the first. It SHALL mark the columns that wait for a person's approval, and SHALL
show that the flow ends at the last stage.

It SHALL be read-only. Arranging happens on the board; this reacts to it.

#### Scenario: the columns are the lifecycle's stages

- **WHEN** a project's lifecycle holds three stages
- **THEN** the view shows where Stories start followed by those three columns, in the stored order

#### Scenario: a gate and an end are both visible

- **WHEN** one step waits for approval and the lifecycle's last stage is reached
- **THEN** that column is marked as gated, and the end of the flow is shown as ending there

#### Scenario: it cannot be used to arrange anything

- **WHEN** an Admin looks at this view
- **THEN** it offers no control that assigns, moves or clears a claimed transition


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

#### Scenario: approval says what it does

- **WHEN** an Admin reads the approval control
- **THEN** it states that the Agent plans, stops and waits, and that nothing executes until someone
  approves

#### Scenario: stopping the chain is chosen, not left blank

- **WHEN** an Admin names a label and then chooses to stop rather than hand on
- **THEN** the label control is not offered, and the Automation is stored with the empty label set
  that has always meant this

### Requirement: the Automations tab orders its content by how often it is read

The Automations tab SHALL present the **catalogue** as its content, followed by a read-only picture of
the flow that catalogue produces. It SHALL NOT present a second surface on which the flow can be
arranged: the flow is arranged on the board, where an Admin already reads it, and two places to change
one arrangement is the duplicate description ADR-0022 records.

Setting up a workflow from the repository, and trying a prompt, SHALL be reachable from the tab's
own toolbar and SHALL open over the tab. They SHALL NOT occupy the tab's vertical space as permanent
content: they are reached on a first day or an occasional afternoon, and a tab that opens on them
answers a question its reader did not ask.

**A project with no Automations configured is the exception.** While none exists, the workflow setup
surface SHALL render inline as the content of the tab, because there is nothing to read and setting one
up is the only thing to do there. In that state the toolbar SHALL NOT offer a second route to the same
surface.

Every control this ordering moves SHALL remain reachable at every viewport width — a capability
offered only at one width is a capability withdrawn at the other.

#### Scenario: the catalogue is the tab's content

- **WHEN** an Admin opens the Automations tab of a project whose Automations claim transitions
- **THEN** the catalogue is shown, followed by the read-only picture of the flow, and each Automation
  appears exactly once

#### Scenario: the tab arranges nothing

- **WHEN** an Admin opens the Automations tab
- **THEN** it offers no control that assigns, moves or clears a claimed transition

#### Scenario: a first run offers setup as the tab's content

- **WHEN** an Admin opens the Automations tab of a project with no Automations
- **THEN** the workflow setup surface is rendered inline on the tab, and the toolbar offers no second
  way to reach it

#### Scenario: the tools open over the tab

- **WHEN** an Admin chooses to try a prompt, or to set up a workflow from the repository, on a
  project that already has Automations
- **THEN** the surface opens over the tab and the tab's own content keeps its position

#### Scenario: no capability is width-dependent

- **WHEN** the tab renders at a narrow viewport
- **THEN** creating an Automation, trying a prompt, and setting up from the repository are all
  reachable

### Requirement: a hand-off broken by exclusion is shown, and never blocks

Where an excluded step was handing work to a step that is still selected, the plan SHALL mark that
the hand-off no longer happens — a person hands on at that point instead. The mark SHALL appear as
the selection changes, without a further read of the repository.

The confirm SHALL NOT be blocked, disabled, or gated behind an extra confirmation by such a break. A
workflow with a human hand-off is a workflow this product already supports; the break is
information, not an error.

A step SHALL be understood to move work into another exactly when its **claimed transition's to-stage**
is the other's trigger, compared case-insensitively — the same identity BR-003 compares triggers with
(DEC-056). A to-stage naming no step in the plan SHALL NOT be treated as a hand-off, so excluding a
step that claims no transition, or one whose transition this installation does not fill, SHALL mark
nothing.

For the plan to answer this without a further read, each row SHALL carry the transition its step claims,
from the discovery the card has already performed. That transition is single-valued, so a step SHALL
have at most one provider and "losing one of two providers" SHALL NOT arise.

#### Scenario: excluding a step that feeds another marks the gap

- **WHEN** a step whose claimed to-stage is another selected step's trigger is excluded
- **THEN** the plan marks that the receiving step is no longer handed work

#### Scenario: the mark never blocks the press

- **WHEN** a hand-off gap is marked
- **THEN** the confirm remains available and needs no additional confirmation

#### Scenario: excluding a step that hands work to nobody marks nothing

- **WHEN** a step claiming no transition into another step in the plan is excluded
- **THEN** no hand-off gap is marked

#### Scenario: the gap is computed from what discovery already returned

- **WHEN** the selection changes
- **THEN** the mark updates without an additional read of the repository

## REMOVED Requirements

### Requirement: an Admin shapes the pipeline on a canvas

**Reason**: the canvas was a second drawing of the flow, derived from labels, and both of #310's
complaints are unrepresentable in it — there is no "before the first step" in a derived graph and no
stored order to change. The board is now the one surface where the flow is drawn and arranged, so
`WorkflowCanvas.tsx`, `Connector.tsx` and `DropSlot.tsx` are deleted (522 lines).

**Migration**: the catalogue half of this requirement survives unchanged and is asserted separately —
create, edit, disable, re-enable and delete stay reachable from the Automations tab, create from its
toolbar and the rest through the panel a catalogue entry opens (see `an Admin edits, disables and
re-enables an Automation`). The workflow half is replaced by *a project's Story lifecycle is a stored,
ordered list of stages*, *an Automation claims one transition of the lifecycle* and *an Admin arranges
the whole flow where the board is read*. The clause that several edges may leave one node is retired
by branching becoming unrepresentable, so the BR-001 serialisation note it required has nothing left
to warn about.

### Requirement: an Admin places the human review by dragging it where the person belongs

**Reason**: a human step needs no representation. It is a transition no Automation claims, so there is
no block to place, no gap to place it in, and no move to sequence. The requirement's careful rules
about clearing the new gap before restoring the old one exist only because the block had a position;
it does not.

**Migration**: replaced by *an unclaimed transition is a person's turn, not a fault*, which keeps the
two clauses that were doing real work — that the block never changed a step's approval requirement, so
reviewing what a step produced and approving what it is about to do stay two different waits (BR-007),
and that the wait is untimed (BR-006).

### Requirement: the workflow's shape is edited from the picture that draws it

**Reason**: it is the canvas's editing contract — drop slots, the sentence a slot renders before a
drop, and four refusals of which three (`self`, `cycle`, `already`) become impossible in a linear
ordered lifecycle. The picture it edited no longer exists.

**Migration**: the two clauses that outlive it move to *an Admin arranges the whole flow where the board
is read*: that every capability reachable by direct manipulation is also reachable without it, through
the same function; and that the change goes through the ordinary Automation update so BR-003's refusal
applies unchanged and is explained where the person is looking rather than enforced twice.

### Requirement: the workflow reads top-down at every width

**Reason**: it fixes the canvas's layout — a single vertical chain, no horizontal scroll inside its
container, a branch indented under the step it leaves. The canvas is deleted and branches are
unrepresentable, so nothing it constrains remains.

**Migration**: two clauses are carried into *an Admin arranges the whole flow where the board is read*:
that reordering is available at every width, because a capability offered only above a breakpoint is a
capability the narrower reader does not have; and that a gated step wears the same chip the board's
column header uses, so the two surfaces cannot disagree about what a human gate is called. The clause
about announcing output labels that reach no other Automation is retired deliberately — after the
transition/mark separation such a label is a mark, not a fault (see *a transition and a mark are
different things*).
