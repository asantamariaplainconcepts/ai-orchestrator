# story-hold Specification

## Purpose
TBD - created by archiving change hold-replaces-the-plan-gate. Update Purpose after archive.
## Requirements
### Requirement: a hold on a Story stops every Automation from starting

A Story SHALL be able to carry a **hold** — the reserved label `hitl` — which means a person must
act before anything else does. While a Story carries the hold, the system SHALL create no Run for
it: not from a matched `StoryChanged` event (UC-011), and not from *Run now* (UC-012). The hold
SHALL be a fixed reserved constant, identical in every Project and every habitat; a Project SHALL
NOT be able to rename it.

The hold SHALL be compared the way the vendor compares labels — case-insensitively, the same
comparison BR-003 and matching already use (DEC-056) — so `HITL` and `hitl` are one hold.

The hold SHALL block **every** Automation of the Project, not only the one that would fire next. A
hold says nobody proceeds; a hold that stopped one Automation while another ran would say something
the word does not.

#### Scenario: a held Story matches nothing

- **WHEN** a `StoryChanged` event is handled for a Story that carries the hold and whose labels and
  state match an enabled Automation
- **THEN** no Run is created, nothing is enqueued, and no label on the Story changes

#### Scenario: the hold is not a bypass

- **WHEN** a Member triggers *Run now* for a Story that carries the hold
- **THEN** the request is refused, the refusal names the hold, and nothing is written (BR-013 —
  manual dispatch bypasses detection only)

#### Scenario: the hold folds case

- **WHEN** a Story carries the hold spelled in different case from the reserved constant
- **THEN** it is still held, and no Run is created

### Requirement: a hold gates creation, never execution

A hold SHALL have no effect on a Run that already exists. A Run in any active state — `Queued` or
`Executing` — SHALL continue to completion and SHALL apply its result, including its marks and its
claimed transition, even if the hold arrives while it runs.

This is what makes the hold safe to apply: labelling a Story SHALL never destroy work in flight.
Ending a Run early remains cancellation (BR-012, UC-014), which is a separate, deliberate act.

#### Scenario: a hold arrives mid-Run

- **WHEN** the hold is applied to a Story whose Run is `Executing`
- **THEN** that Run finishes, applies its marks and its transition, and reaches its terminal state
  as though the hold were not there

#### Scenario: the next Run is still refused

- **WHEN** that Run succeeds and its result would ordinarily match another Automation
- **THEN** no further Run is created while the hold remains

### Requirement: an Automation stops for a person by applying the hold

An Automation SHALL be able to apply the hold when its Run succeeds, through the marks it already
applies (`AutomationDetail.OutputLabels`). The hold SHALL travel in the **same licensed write** as
the Automation's other marks and its claimed transition — DEC-062's carve-out for output labels
already permits that write, so applying a hold SHALL require no new vendor write and no new field
on the Automation.

An Automation configured this way is the successor to `requiresApproval`: the flow stops after it
acts, rather than pausing inside it.

#### Scenario: a stopping step marks the Story

- **WHEN** a Run of an Automation whose marks include the hold succeeds
- **THEN** the Story carries the hold alongside that Automation's other marks and its new stage,
  written once

#### Scenario: a stopping step still hands on

- **WHEN** such an Automation also claims a transition
- **THEN** the Story moves to the claimed stage **and** carries the hold — the stage says where the
  work is, the hold says nobody may take it further yet

### Requirement: clearing the hold resumes the flow with no resume machinery

Removing the hold SHALL be an ordinary label change on the Story (UC-008), performed in the vendor
or through the portal, and permitted to a Member (BR-009). The resulting story event SHALL be
matched exactly like any other (BR-015): if an Automation's trigger now matches, its Run SHALL be
created.

The system SHALL hold no state about a held Story beyond the label itself. There SHALL be no paused
Run, no resume endpoint and no timer — a hold is untimed for the same reason BR-006's waits are, and
for a simpler one: nothing is waiting inside the product.

#### Scenario: removing the hold starts the next step

- **WHEN** a person removes the hold from a Story whose labels match an enabled Automation
- **THEN** a Run is created for that Automation, exactly as if the label had just been applied

#### Scenario: nothing is remembered

- **WHEN** a Story is held and then cleared
- **THEN** no Run, record or timer existed for the hold itself at any point — only the label changed

