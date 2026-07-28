# Design: workflow-canvas

## D1 — The graph is derived, and the layout with it

An edge exists exactly when one Automation's output label equals another's trigger label. There
is no workflow table, no stored edge and no saved node position: the picture is a rendering of
the same rows the list shows, so it cannot claim a chain that would not fire.

Layout follows from the graph rather than from a mouse. Nodes are placed in dependency order —
roots (nothing points at them) first, then what they feed — with anything unreachable in a
trailing row. Two Admins looking at one project therefore see the same picture, and a node that
moves has moved because the *pipeline* changed. Saved positions would be a second source of
truth about a graph that already has one.

## D2 — One balloon, two mechanisms, chosen by where it lands

The Admin sees a single object called "human". Where it sits decides what it means:

| Position | Means | Removing it |
|---|---|---|
| On an edge (dotted) | the chain stops: no output label upstream | writes the downstream trigger as the upstream output label |
| On a node | `requiresApproval` — the Run pauses on its plan (BR-007) | clears the flag |

These are genuinely different mechanisms and the UI deliberately does not say so. They share a
meaning that matters more than their implementation: *a person is required here*. The dotted edge
and the node badge are drawn in the same colour for that reason.

The asymmetry worth stating: a balloon on an edge between A and B can only be removed if B has a
trigger label to copy — which it always does, since a trigger label is mandatory. So the gesture
never fails for structural reasons, only for the self-trigger rule (#115) or an overlap (BR-003).

**Where the balloon actually hangs, discovered while building it.** An absence has no two ends:
"no output label" does not name a destination, so there is no A→B gap to put a balloon *in*. The
balloon therefore hangs off a node's output, and removing it is the act of choosing the
destination — which is exactly the "hands work to…" control. The picture the issue described
survives; what changed is that the gesture and the choice are one thing rather than two.

Chains are drawn **left to right**, like the board's columns, and the connector between two
steps is a **vertical rule** — the separator a reader already knows from a kanban. Solid where
work flows on its own, dotted where a person carries it, broken in the middle by the control
that changes which of the two it is. A long chain scrolls sideways exactly as the board does.

## D3 — Drag is sugar; the control is the feature

Same conclusion as the board, for the same reasons and now with evidence: HTML5 drag cannot be
performed by Playwright and cannot be driven by a keyboard, so a canvas whose only affordance is
dragging is a canvas that is neither testable nor accessible. Every node therefore carries
explicit controls — "hands work to…", "requires approval" — and dragging is a shortcut for people
with a mouse. The acceptance criteria are written against the controls.

## D4 — The canvas edits Automations, one at a time, through the existing use case

A reconnection is an update to one Automation's output label; a balloon on a node is an update to
one Automation's `requiresApproval`. Both go through `UpdateAutomation`, so BR-003's overlap
check and #115's self-trigger refusal apply unchanged and the canvas cannot invent a validation
path of its own. Nothing here is transactional across two Automations, because no gesture in
scope needs to change two.
