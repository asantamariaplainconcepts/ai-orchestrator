# Design: drag-the-human-step

## D1 — The block is a break in the chain, drawn where the person stands

An Automation hands work on by writing a label (#115). When it writes nothing, the chain stops and a
person decides whether the work is good enough to continue — which is precisely what reviewing a
proposal is. So the human step is not a new field: it is `outputLabel = null` on the step whose
output is being reviewed.

Dropping the block into the gap after a step clears that step's output label. One field, one
Automation, one update. Nothing about the block is stored, and its position is not a fact of its own
— the absent label is the fact.

This is deliberately **not** `requiresApproval`, which is the other human moment: BR-007's two-phase
Run, approving what a step is about to do rather than what the previous one did. The two must stay
distinguishable in the picture because they behave differently at run time, and the card already
carries the second.

## D2 — Removing is not the reverse of placing, because an absence has no two ends

Placing the block clears a label: one value, gone. Removing it has to *restore* a label, and a label
names a destination — so the gesture cannot be a bare click unless the destination is unambiguous.

Where the workflow already draws a step after the gap, that step's trigger label is the destination
and removal can be a single action. Where nothing follows, there is nothing to reconnect to, and the
control is the existing select that asks whom to hand work to. That asymmetry is not a wart; it is
the shape of the data, and #116 recorded it before this item existed.

## D3 — A move breaks before it reconnects

Moving the block from one gap to another is two updates: clear the new step's output label, restore
the old step's. The API has no compound operation, and inventing one for a drag gesture would put a
use case in the wrong place.

So there is a window where one has applied and the other has not, and the ordering decides which way
it fails. **Break the new gap first, then reconnect the old**: an interrupted move leaves *two*
places where a person is asked, never zero. An extra review costs somebody a click; a missing one
lets work continue unreviewed, which is the thing the block exists to prevent.

## D4 — The refusal is the ordinary one

Every change goes through the Automation update, so BR-003's overlap check and #115's self-trigger
refusal apply without this feature knowing they exist. A refused change returns the workflow to what
is stored and shows the reason the API gave. The canvas is a way of expressing an update, never a
second way of writing — #116's rule, unchanged.

## D5 — Dragging is sugar; the existing controls are the sentence

The button that breaks a connection and the select that restores it both stay, and not as
fallbacks — they say in words what the block says in position. A keyboard user, a touch screen and an
Admin who prefers reading all use them.

Below the width at which the flow reads left to right, dragging is not offered at all. A drag on a
phone competes with the gesture that scrolls, and losing that fight silently is worse than not
having the feature.
