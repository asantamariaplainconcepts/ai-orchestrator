# Design: vertical-workflow-canvas

## D1 — One layout, because two were never justified by two needs

The horizontal fork existed to fit more of a chain on a wide screen. It cost: a hidden drag below
`xl`, a sideways scroll above it, and every subsequent change having to be made twice. A pipeline
flows in one direction; rendering it in the direction it flows is the simpler thing and the truer
one.

## D2 — `shrink-0` was load-bearing for the old layout and wrong for this one

The node wrapper carried `shrink-0` so a step kept its width while the row scrolled. In a column
that stops the card shrinking below its content, which reintroduces exactly the horizontal scroll
this change removes — the canvas overflowed its own container at 375px until it was replaced with
`min-w-0`.

Found by measuring `scrollWidth` against `clientWidth` in the browser, not by looking at the diff.
A class that was correct for one axis is not obviously wrong on the other.

## D3 — The gate chip is shared, and its hint is not

The board's column header and a canvas node both say "a person approves here", so the chip is one
component. Its *tooltip* is not shared: the board's reads "Dropping here starts a plan for a human
to approve", which is meaningless on a surface nothing is dropped onto.

Sharing it wholesale shipped that sentence to the canvas. The hint became a prop with the board's
text as the default — the general lesson being that components should be shared by **meaning**, and
the parts that differ by surface have to be parameterised rather than inherited.

## D4 — The select is revealed, and that is still reachable

ADR-0006 requires that an implemented capability be selectable from a control a human uses. It does
not require the control to be permanently on screen. A named button that reveals the select keeps
the capability one click away, and stops the flow reading as a form at every gap.
