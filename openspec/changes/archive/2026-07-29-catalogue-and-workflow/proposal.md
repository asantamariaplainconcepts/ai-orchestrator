# Proposal: catalogue-and-workflow

## Why

Issue #136 (ACT-001 configures; UC-008). The Automations tab does two jobs under one name: it is
the **catalogue** — which Automations exist, with what trigger label, action and runtime — and it
is the **workflow**, the path they form when one hands work to the next. Every awkwardness in this
area comes from the two being the same list.

Two are already on record. #122 existed to give the loose Automations — `estimate`, `transition`,
`refine` — "their place after the ordered ones": a special case *inside* the workflow for things
that are not in it. It was closed as superseded rather than solved. And the canvas draws every
Automation, chained or not, so the flow wraps into a grid whose rows mean nothing.

Separating them dissolves both. An Automation that is not in the flow is not a special case of the
flow — it is simply not in it, a trigger that acts on its own when somebody applies its label. The
workflow stops being a list with exceptions and becomes what it is: a path derived from which
Automation hands work to which.

## What changes

- **Two named sections** (design D1): the catalogue in its own column, the workflow in the rest.
- **The workflow holds only the chain** (design D2). Membership is derived, never stored: an
  Automation with no edge in either direction is a catalogue entry and does not appear.
- **One continuous flow** (design D3): a single left-to-right row that scrolls inside its own
  container, never wrapping.
- **A header that says how big the flow is** (design D4) — steps, and how many times it stops for a
  person. Both derived.
- **The separation is locked as a decision** (design D5), because it becomes vocabulary.

## Impact

- Specs: `automation-configuration` — one MODIFIED requirement (the canvas becomes two sections
  with defined membership).
- Docs: DEC-053 in `10-locked-mvp-decisions.md`; the glossary gains **catalogue** and **workflow**.
- Code: `workflowGraph.ts` gains the membership question; `AutomationsSection` splits; the canvas
  stops wrapping. No API change and no schema change.

## Out of scope

- **Dragging anything.** The hand-off select and the approval button keep doing the work; placing
  the human step by dragging is #137.
- Reordering steps by dragging — the order is the chain.
- The board's columns and its human column — #128, which follows this definition of the workflow.
- Creating an Automation, which stays exactly where it is.
