# Proposal: drag-the-human-step

## Why

Issue #137 (ACT-001 configures; UC-008, UC-013). The human gate exists and is set by a button on
the preceding step's card. That is a correct control and a poor model: it asks an Admin to think
"which step should require approval" when what they picture is "a person reviews *here*, between
these two".

The original ask was a thing you can pick up and put somewhere else in the flow. A button on a card
is not a thing, and a rule with a caption is a drawing of the consequence rather than the cause.
Now that #136 has made the catalogue and the workflow two separate places, the block has somewhere
to come *from*, and the gesture becomes available.

What makes this more than decoration is that the drop is not a new mechanism. It writes
`requiresApproval` on the step after it — BR-007's flag, set by putting a person where the person
goes. The picture and the configuration become the same act.

## What changes

- **The human step becomes a draggable block** (design D1), dragged out of the catalogue and
  dropped between two steps.
- **Moving it moves the approval** (design D2): cleared on the step it left, set on the step it now
  precedes. One update per side, in one gesture.
- **A refused drop is refused visibly** (design D3), through the ordinary Automation update, so
  BR-003's overlap check and #115's self-trigger refusal apply unchanged.
- **Valid targets are visible before the drop** (design D4) — a drag that cannot land should say so
  before the mouse is released, not after.
- **Every gesture keeps its explicit control** (design D5). The existing approval button is that
  control and stays; nothing here becomes drag-only, and on a phone dragging is not the path.

## Impact

- Specs: `automation-configuration` — the canvas requirement gains the drag gesture and what a drop
  means.
- Code: a draggable block in the catalogue, drop targets in the workflow, and the two-sided update.
  No API change and no schema change.

## Out of scope

- Dragging ordinary Automations into or out of the flow, and reordering steps by dragging. The order
  is the chain, edited by the hand-off select; changing that is its own item.
- Creating an Automation by dragging.
- The board's human column — #128, which follows the same meaning on the other surface.
