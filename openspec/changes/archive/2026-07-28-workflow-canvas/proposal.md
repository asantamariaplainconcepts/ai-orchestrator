# Proposal: workflow-canvas

## Why

Issue #116 (ACT-001, Admin). The Automations tab is a list of rows, and a pipeline is not a list
— it is a shape. Reading six rows, an Admin cannot see that grill hands to propose, that propose
deliberately hands to nobody, or where a human is required; the same three facts are obvious on
a canvas. And since #115 the shape is *editable*: an output label is what an edge is made of.

What this makes newly possible, rather than merely visible: **closing the propose→implement gap
by removing a HITL balloon** — turning a supervised pipeline into an autonomous one without
editing two forms and knowing that two label strings must match.

## What changes

- **A canvas view on the Automations tab** (list ⇄ canvas toggle, preference remembered, the same
  shape the board's toggle established): one node per Automation, one edge wherever an output
  label equals another Automation's trigger label.
- **The graph is derived, never stored** (design D1) — the edges *are* the label agreements, so
  the canvas cannot disagree with what actually fires.
- **Reconnecting writes an output label**: pointing an Automation's outgoing edge at another node
  sets the upstream output label to that node's trigger. This is the canvas's only mutation of
  the chain.
- **The HITL balloon, one concept in two positions** (design D2):
  - **on an edge** — the chain is broken there because the upstream Automation has no output
    label; removing the balloon gives it one, closing the chain; adding one to a solid edge
    clears the label, opening it.
  - **on a node** — the Automation's `requiresApproval` (BR-007).
- **Every gesture has a non-drag equivalent at every width** (design D3), as the board
  established and for the same reason: it is the only path a keyboard, a phone, or a test has.

## Impact

- Specs: `automation-configuration` (one ADDED requirement).
- Code: frontend only. No endpoint — the canvas reads the automations query and writes through
  the update use case that already exists.
- No new dependency: the layout is computed, and dragging reuses the board's HTML5 approach.

## Out of scope

Branching (#115 allows one output label), creating Automations from the canvas, saved node
positions, and conditional edges.
