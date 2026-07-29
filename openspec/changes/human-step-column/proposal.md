# Proposal: human-step-column

## Why

Issue #128 (ACT-002 watches, ACT-001 places; UC-026, UC-013, UC-008). The board's columns are the
pipeline's steps in configuration order, not in the order work moves through them, and the places
where a person must act are invisible.

A Story whose step finished and whose chain stops there is waiting for a human: somebody has to look
at what that step produced and carry the work on. Today it sits in the finished step's column looking
exactly like one still being worked. The board answers "which Stories carry which labels" when the
question somebody opens a pipeline to ask is "where is work, and where is it stuck".

## What changes

- **Columns ordered by the chain** (design D1), with the Automations that are not in the workflow
  (DEC-053) after them — the board still shows every label a Story can carry.
- **A human column between X and Y when X hands work to nobody** (design D2), holding the Stories X
  finished. Its own header, count and empty state.
- **Placing it clears X's output label** (design D3) — the same write #137's block makes on the canvas,
  so the two surfaces cannot disagree about what a person between two steps means.
- **The other wait stays a state** (design D4): `requiresApproval` keeps its badge on the step's own
  column, and a Run awaiting an approval or an answer stays in that step's column with its state on
  the card.

## The premise this corrects

#128's first version said the column appears "whenever the second step requires approval" and that
placing it sets `requiresApproval`. That is the wait the *card* carries, not the one a position
between two steps describes — the same error #137's first draft made, which the owner corrected before
any code existed.

The correction matters on the board more than on the canvas, because the two waits live in different
places: "X finished and nobody moved it on" is a position between X and Y, while "Y planned and awaits
approval" is a state of a Run that has already reached Y. Drawing the second as a column before Y
would claim the work has not arrived when it has.

## Impact

- Specs: `backlog-mirror` — one MODIFIED requirement (the board), carrying its existing scenarios.
- Code: the board's column derivation gains the chain order and the human columns; `BoardAutomation`
  gains `outputLabel`, which it needs to see an edge at all.
- No schema change and no new persisted field: the order is the chain, the column is an absent output
  label, the states stay states.

## Out of scope

- Merging the canvas into the board. The canvas edits the shape, the board runs the work.
- Reordering by dragging columns: the order is the chain, edited on the canvas.
- Any change to how Runs execute or how matching works.
