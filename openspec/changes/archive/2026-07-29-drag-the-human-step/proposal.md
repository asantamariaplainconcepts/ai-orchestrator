# Proposal: drag-the-human-step

## Why

Issue #137 (ACT-001 configures; UC-008, UC-013). A person reviewing the pipeline's work is a real
step in it, and today it is expressed by a button that breaks a connection — correct, and not the
thing an Admin pictures. They picture a person standing *between* two steps, reading what the first
produced before the second may start. To call a proposal good, somebody has to approve it.

Now that #136 has made the catalogue and the workflow two separate places, the block has somewhere
to come from, and the gesture becomes available.

## What the block means, and what it is not

The product already has **two** human moments and keeps them apart. This item is about the first
only.

- **Reviewing what a step produced** — the step's `outputLabel` is cleared, so the chain stops there
  and a person carries the work onward by applying the next label. This is what the block in a gap
  means, and it is the one an Admin points at when they say "somebody has to approve the proposal".
- **Approving what a step is about to do** — `requiresApproval` on that step, BR-007's two-phase
  Run: the agent plans, waits, then acts. Drawn on the *card*, unchanged by this item.

The first draft of this proposal said the drop sets `requiresApproval` on the following step. That
was the second moment wearing the first's clothes, and it would have made the two indistinguishable
in the picture while behaving differently at run time.

## What changes

- **The human block is draggable** from the catalogue into a gap (design D1). Dropping it clears the
  preceding step's output label — one field, on one Automation.
- **Removing it names a destination** (design D2), because an absence has no two ends: clearing was
  one field, restoring has to say *to whom*.
- **Moving it breaks before it reconnects** (design D3), so an interrupted move leaves an extra
  review rather than none.
- **Refusals are the ordinary update's** (design D4), so BR-003 and #115's self-trigger check apply
  unchanged.
- **Dragging stays sugar** (design D5): the existing controls remain at every width, and dragging is
  not offered where the flow reads vertically.

## Impact

- Specs: `automation-configuration` — the canvas requirement gains the gesture and what a drop
  means.
- Code: a draggable block in the catalogue, drop targets in the gaps, and the move's two updates.
  No API change and no schema change — `outputLabel` already exists and already means this.

## Out of scope

- **`requiresApproval`.** It is the other human moment and it already has its control on the card.
  This item must not make the two look like one thing.
- Dragging ordinary Automations, and reordering steps by dragging.
- The board's human column — #128, which follows the same meaning on the other surface.
