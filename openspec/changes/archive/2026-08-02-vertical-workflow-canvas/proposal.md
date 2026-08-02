# Proposal: vertical-workflow-canvas

## Why

[#232](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/232). The canvas was two
products in one file. Below `xl` it stacked and **the drag was hidden entirely** — a phone could not
reorder a pipeline at all. At `xl` it flipped horizontal and scrolled sideways, so a chain ran off
the edge of the screen. Two layouts, two interaction models, one codepath forked on `xl:` throughout.

## What changes

- **One vertical layout at every width.** All three `xl:` forks are gone; the chain reads top-down
  from 320px up, capped at `520px`, and branches indent under the step they leave.
- **Reordering works on a phone**, because the block that was `hidden … xl:flex` is now simply
  visible. That is the capability, not the styling.
- **The node is a header plus its content**: trigger, gate chip and actions on one row, rather than
  two full-width buttons stacked under the card making every node taller than what it carries.
- **The open gap offers a control, not a permanent select.** A select at every gap is a form offered
  to somebody who is not connecting anything; it is one click away instead of zero, which is what
  ADR-0006 asks.
- **A dangling output label is announced on the node that owns it**, not at the gap below — the
  label belongs to the step, and that is where it gets fixed.
- **`GateChip` is shared** with the board's column header rather than duplicated.

## What does not change

The graph, matching, branching, and every stored value. This is layout and placement.

## Impact

`WorkflowCanvas.tsx`, `KanbanBoard.tsx` (loses its private chip), one new shared component. No API.
