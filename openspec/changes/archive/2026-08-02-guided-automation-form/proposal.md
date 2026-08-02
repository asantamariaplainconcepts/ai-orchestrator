# Proposal: guided-automation-form

## Why

[#231](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/231). Creating an
Automation is the main configuration act in this product, and the form is a four-column grid of
eight peer fields with no hierarchy — trigger, state, action, runtime, prompt, timeout, output
labels, and an approval checkbox stranded at the bottom beside the submit button.

Two consequences. The reader must already hold the mental model to fill it: nothing on screen says
that the first two fields decide *when*, the middle four decide *what*, and the last one decides
*what happens next*. And the consequence of what they filled is invisible until after Save, on the
canvas — so a wrong trigger is discovered by a Run that fired when nobody expected it.

## What changes

- **Three numbered questions** in the order people think: *when does it fire* → *what does it do* →
  *what happens after*. The existing fields are regrouped, not replaced.
- **A live sentence** at the top, restating the configuration in prose as it is filled, in the same
  vocabulary the canvas uses. A mistake becomes visible before Save.
- **Approval states its consequence** where the execution it gates is described, rather than being a
  bare checkbox next to the submit button: the Agent plans, stops, and waits in the Inbox; nothing
  executes until someone approves.
- **The third question is a choice, not a field.** "Hand to the next step" reveals the existing
  chips-and-datalist control; "stop — a person takes over" hides it. Today an empty label set is the
  same answer, expressed as an absence nobody reads as a decision.

## What does not change

- **The request shape.** Byte-identical for every configuration the form can produce. This is
  regrouping; the API, its validation and the overlap guard (BR-003) are untouched.
- **The prompt field**, its `required`, its datalist of the repository's own prompts (#215), and its
  degradation to a plain input with a readable reason.
- **The output-labels control** — chips, free text, Enter-adds-not-submits, and the branch note that
  restates BR-001.
- **The action select.** One option since #162, and ADR-0006's reachability test asserts it is
  present and enabled. A capability that ships must be selectable from the control a human uses, so
  a one-option select is the honest rendering — it moves into question two, it does not disappear.

## Impact

- `src/frontend/features/automations/AutomationsSection.tsx` and the i18n catalogue. No API, no
  module, no migration.
- **Two kit additions** (design D3): `shared/ui/switch.tsx` and `shared/ui/radio-group.tsx`. The kit
  has neither, and the alternative — building a toggle out of a checkbox and a radio out of buttons —
  is the drift `textarea.tsx` was added to avoid in #189.
