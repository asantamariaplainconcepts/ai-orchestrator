# Design: drag-the-human-step

## D1 — The block is the flag, drawn where it acts

`requiresApproval` is a boolean on an Automation. The human step is that boolean, and the only
honest way to draw it is at the place it takes effect: immediately before the step that will wait.

So the block is not a new entity with its own record. Dropping it between two steps sets the flag on
the second; removing it clears it. There is nothing to persist about the block itself, and if the
implementation reaches for a "human step" row or a position, the model has been misunderstood — the
flag already says where it is.

## D2 — Moving it is two updates, and both must happen

Dragging the block from one gap to another means: clear approval on the step it used to precede, set
it on the step it now precedes. Two Automations change.

They are two ordinary updates, not a transaction, because the API has no compound operation and
inventing one for a UI gesture would put a use case in the wrong place. What that costs is a window
where the first has applied and the second has not: approval briefly on neither step, or on both.
Neither state is dangerous — an extra gate stops a Run, a missing gate lets one proceed as it did
before the drag — and both are visible and correctable. The order is chosen so the risky half fails
safe: **set the new gate first, then clear the old one**, so an interrupted move leaves an extra
approval rather than none.

## D3 — The refusal is the ordinary one

Every change goes through the Automation update, so BR-003's overlap check and #115's self-trigger
refusal apply without this feature knowing they exist. A refused drop returns the canvas to what is
stored and shows the reason the API gave.

This is the same discipline #116 recorded: the canvas is a way of expressing an update, never a
second way of writing.

## D4 — A drop that cannot land says so before the release

A drag whose invalid targets look identical to valid ones teaches the Admin by failure. The valid
gaps are marked while the drag is in progress, and a gap that would be refused is visibly not a
target.

That is a property of the drag, not of the drop: the refusal in D3 still exists for what only the
server can know, and this only removes the cases the client already knows are impossible.

## D5 — Dragging is sugar, and the button is the sentence

The approval button on the card stays, and not as a fallback — as the control that says in words
what the block says in position. A keyboard user, a touch screen, and an Admin who prefers reading
all use it, and #110 and #116 both already established that no gesture in this product is
drag-only.

Below the wide breakpoint the flow reads top to bottom and dragging is not offered at all. A drag
gesture on a phone competes with the gesture that scrolls, and losing that fight silently is worse
than not having the feature.
