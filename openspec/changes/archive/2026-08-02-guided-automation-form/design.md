# Design: guided-automation-form

## D1 — Three questions, and why these three

*When does it fire* (trigger label, trigger state) → *what does it do* (action, prompt, runtime,
timeout, approval) → *what happens after* (output labels).

This is the Automation's own execution order, not a taxonomy invented for the form: matching reads
the trigger, the executor reads the prompt and runtime, and `HandOn` applies the output labels after
a success. A reader who fills the form top to bottom has walked a Run.

Approval belongs in question two rather than a fourth of its own. It gates *this* execution — the
plan phase stops before the work — so putting it beside the runtime and the timeout keeps the
consequence next to the thing consequenced. It was at the bottom next to Save, which read as a
form-submission option rather than an execution property.

## D2 — The live sentence restates, it does not validate

One line above the questions, assembled from current state: what fires it, what it runs, whether it
waits for a human, and where it hands on. It is prose in the canvas's vocabulary so the two surfaces
agree.

**It is deliberately not validation.** An incomplete form yields an incomplete sentence with the
missing parts named, not an error — the field-level refusals already exist and duplicating them here
would give two voices for one problem. The sentence answers "is this what I meant", which nothing on
this screen answered before Save.

## D3 — Two kit additions, for the reason the last one was added

`shared/ui/` has no `Switch` and no `RadioGroup`. Both arrive with the same discipline
`textarea.tsx` followed in #189: the same border, focus ring and invalid states as the existing
controls, so a taller or rounder control does not read as a different product.

The alternative — a checkbox styled to look like a toggle, radios built from buttons — is precisely
the drift the design contract exists to stop, and the gate would not catch it because the tokens
would be right.

## D4 — The third question makes an absence into a decision

Today, "this Automation ends the chain" is expressed by leaving the output-labels control empty. An
absence is not an answer: nothing distinguishes *decided to stop* from *did not get that far*.

Two options, one of which reveals the existing control. The stored value is unchanged — "stop" is
still an empty array — so nothing in the API or the graph learns a new concept. What changes is that
the Admin has said which they meant.

## D5 — The action select stays, and this is not an oversight

`AUTOMATION_ACTIONS` has one entry since #162, so it renders a select with a single option, which
looks like something to delete.

ADR-0006 forbids it: every implemented capability must be selectable from the control a human
actually uses, and `ProjectPage_Should_Constraint.EveryImplementedRuntimeAndVendor_Should_BeSelectableFromTheForm`
asserts `#action` is present and enabled. That test exists because this project has twice shipped a
complete implementation nobody could reach. A one-option select is the honest rendering of a
one-action catalogue, and it moves into question two beside the prompt it pairs with.
