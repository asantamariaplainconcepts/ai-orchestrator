# Design: actionable-failure-inbox

## D1 — Re-running is the Run-now path, called from somewhere else

*Run now* already takes a Story and an Automation and creates a Run through `RunCreator`, where
BR-001, BR-002, BR-013 and the approval gate all live. A failed Run knows both values.

So "Run again" is not a new capability, it is the existing one reached from where the failure is
visible. No endpoint, no dispatch code, no second rule to keep in step — and that is the whole
argument for it: a second creation path would be a second place for BR-001 to be forgotten, which is
exactly what `RunCreator` was extracted to prevent.

What the button adds is one fact the Run page did not have: which Automation to re-run. It comes from
the Run, not from a picker, because choosing a *different* Automation is a different intention and it
already has a control in the backlog.

## D2 — Dismissal is stored, and that is an addition to #94's D2 rather than a contradiction

#94 decided that a failure leaves the inbox *by query, never by a stored flag*, because BR-013 has two
re-trigger paths — *Run now* and re-labelling — and a flag would be forgotten by one of them. That
reasoning is right and it stays: the newer-Run condition remains derived.

Dismissal is a different kind of fact. Nothing in the data distinguishes "nobody has decided yet"
from "somebody decided not to re-run this" — the two are identical rows. A query cannot derive a
decision that was never written down, so this one is written down.

The distinction worth keeping: derived facts are about *the world* (a newer Run exists), stored facts
are about *a person* (somebody looked and chose). #94's rule applies to the first and this is the
second.

## D3 — When, not who, and the Run stays Failed

The dismissal records its timestamp and is shown on the Run. Who dismissed it is deliberately absent:
identity is real but unauthenticated until OPN-002, so recording a principal now would store
"anonymous" and read as an answer.

The Run stays `Failed` — terminal, and nothing re-runs (BR-004). A dismissal is somebody saying "I
have seen this", not the product deciding the Run succeeded. Anything that changed the state would be
rewriting history to clear a list, which is the opposite of BR-014's promise.

## D4 — The inbox still acts on nothing

Both controls live on the Run page and the inbox links to it. That is #94's v1 shape and it holds for
a reason beyond consistency: an action in a list row acts on an item the reader has not opened, and a
re-run costs money. The list points; the page decides.

## D5 — Two counts, one condition

The pulse's failure count and the inbox's failure lane must agree, or a Member sees "1 waiting" and an
empty list. They share the condition — failed, no newer Run, not dismissed — and the change puts it
in one place rather than writing it twice, because writing it twice is how they would come to
disagree.
