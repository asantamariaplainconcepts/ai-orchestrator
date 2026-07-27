# Design: grill-action

## D1 — The verdict is a first-line contract, like Estimate's number

The agent's reply either starts with `READY` or it is the questions, verbatim. The same shape
that made Estimate honest (a reply that does not start with a number fails rather than guesses)
makes grill simple: no JSON envelope for a model to malform, and the failure mode — a rambling
reply — degrades into questions a human reads, not into a wrong state.

## D2 — Fail on a missing rubric before touching the Story

The rubric read happens before any write. A grill that comments "I could not find your
Definition of Ready" would put its own configuration error on somebody's backlog; a Run that
fails naming the path puts it where operators look (BR-014, UC-011).

## D3 — The conversation is read from the Run's birth, not its last wait

`WaitingSince` is the resume checker's watermark and clears on resume — by the time the next
pass executes, it is gone. The pass instead reads every comment since the Run was created and
filters the agent's own by marker. Stateless, idempotent, and it survives the human answering
twice or editing history.

## D4 — Ready is a label write, deliberately chainable

The ready label goes through the same write path as UC-008, so it lands at the vendor, comes
back through reconciliation as an ordinary `StoryChanged`, and can trigger the next Automation
(DEC-027's both-sides labelling doing the wiring). Grill→propose chaining is therefore matching
behaving normally — no orchestration code exists to maintain.

## D5 — Settings are nullable columns, defaults in code

`RubricPath` and `ReadyLabel` are nullable on Automation; the executor applies the defaults.
Null means "the framework's convention", so existing rows and the defaults button need no
migration of meaning — and the form shows the fields only for this action, because a rubric path
on a TransitionState Automation is noise.
