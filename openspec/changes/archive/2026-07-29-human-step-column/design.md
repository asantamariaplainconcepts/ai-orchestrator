# Design: human-step-column

## D1 — The order is the chain, and what is not in the chain is not in the flow

Columns today are trigger labels in configuration order, which is an implementation detail of when
somebody happened to create each Automation. The chain (#115's output labels, #116's graph, #136's
membership) already knows the real order.

Automations outside the workflow still get columns, after the ordered ones. That is not a
contradiction of DEC-053 — they are not in the *workflow*, and the board is not only the workflow: a
Story can carry `ai:estimate` and has to be somewhere. The distinction the board draws is order, not
existence.

## D2 — The column holds the wait that is a position, not the wait that is a state

A step that hands work to nobody stops the chain. The Stories it finished are waiting for a person to
decide whether the work continues — and that is a place between two steps, which is what a column is.

`requiresApproval` is the other wait and it is not a position: a Run in `AwaitingApproval` has already
reached its step and is mid-flight there. Rendering it as a column *before* that step would tell the
reader the work has not arrived yet, which is false. It stays a badge on the step's column and a state
on the card, which is what the board already does correctly.

Getting this backwards is what the issue's first version did, and it is worth a scenario of its own
rather than a comment, because the two look similar in a sentence and behave differently at run time.

## D3 — Placing the column is the canvas's write, on the other surface

Dropping the human block on the canvas clears the preceding step's output label (#137). Placing the
column on the board does the same thing, through the same ordinary Automation update, so BR-003's
overlap check and #115's self-trigger refusal apply here too.

One meaning, two surfaces. If the board wrote something else, an Admin who placed a person on one
screen would find a different arrangement on the other, and neither would be wrong.

## D4 — Nothing new is stored

The order derives from output labels. The human column derives from an output label being absent. Its
contents derive from which Runs finished. `AwaitingApproval` and `AwaitingInput` are already states on
Runs.

A position field or an "is a human column" flag would be a second source for facts the data already
carries, and the first time the two disagreed the picture would be the one lying — the board draws
what will happen, and a stored layout can claim a chain that would not fire.
