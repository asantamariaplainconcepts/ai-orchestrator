# Design: catalogue-and-workflow

## D1 — Two things, two places, one screen

The catalogue answers "what can this project do"; the workflow answers "what shape does its
pipeline have". They are different questions with different answers, and they have been sharing a
list.

They stay on one tab rather than becoming two, because the relationship is the point: the workflow
is built out of the catalogue, and a reader who cannot see both at once cannot see that. #109
separated *operating* from *configuring* and that separation holds — this orders the configuring
from the inside, and does not move anything back to the operating tab.

## D2 — Membership is derived, and that is what removes the special case

An Automation belongs to the workflow when it has an edge: it hands work to another, or another
hands work to it. Everything else is a catalogue entry.

That single sentence is what #122 was reaching for and could not express, because it was trying to
give non-members a position among members. There is no position to give. `estimate` is not "at the
end of the pipeline"; it is not in the pipeline, and a person applying its label is not interrupting
a flow.

If the implementation reaches for an "in the workflow" flag, the design has been misunderstood: the
edges already say it, and a stored flag could disagree with them.

## D3 — One row, scrolling inside itself

The canvas wraps because it is a grid. A wrapped pipeline is a false picture: the reader has to
work out that the last item of one row continues into the first of the next, and nothing on screen
says so.

So the flow is one row that scrolls horizontally inside its own container. Inside its own container
is the part that is easy to get wrong and expensive to get wrong: a row that lets the page scroll
sideways breaks every other screen on a phone. Below the wide breakpoint the flow reads top to
bottom instead, because a horizontal scroll on a phone competes with the gesture that navigates.

## D4 — Counting steps is not counting Automations

The header says "6 Automations", which is a fact about the catalogue and tells a reader nothing
about the pipeline. The workflow's own header states its length and how many times it stops for a
person — the two numbers somebody actually wants before reading the diagram.

Both are derived from the same edges as membership. Nothing counts anything twice, and nothing is
stored.

## D5 — Lock the vocabulary, because it has already been re-merged twice

"Catalogue" and "workflow" become two named things in this product's vocabulary. If that is not
written down as a locked decision and in the glossary, the next change collapses them again by
accident — which is exactly what happened in #122 and, in the first draft of this item's own issue,
in a sentence describing the palette as "what can be placed" while creation lived elsewhere.

A rule that exists only in one implementation's shape is a rule that survives until the next
refactor.
