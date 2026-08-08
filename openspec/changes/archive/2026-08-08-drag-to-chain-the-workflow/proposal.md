## Why

[#293](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/293). The workflow is
wired by typing labels — an Automation hands work to another because somebody wrote the second
one's trigger into the first one's output. That is the model and it stays the model: the graph is
derived from labels precisely so the picture cannot claim a chain that would not fire (#116, design
D1).

What it costs an Admin is bookkeeping. Chaining a step means opening a panel and editing a field,
so the shape of the flow gets changed somewhere other than where the shape is drawn. And the effect
of that change on the Backlog board — the columns Stories actually move through — lives in another
tab, so the person wiring a pipeline is never looking at what they are building.

Actors: **ACT-001 Admin**. Use cases: UC-005, UC-006, UC-007. Business rules: BR-003, BR-001,
BR-014. Source: design review turn 8 (options 8a/8b/8c).

## What Changes

A standalone Automation drags out of the catalogue and into the chain. Every gap between steps
becomes a **drop slot that says what it would wire, in words, before the drop happens** — both
hand-offs named — because dropping rewrites output labels and nothing else. A slot at the end
chains onto the tail. Dragging a step onto the catalogue takes it back out.

A drop that would break a rule refuses **at the slot**, quoting the rule: a trigger shared with
another enabled Automation (BR-003), a loop, a step handed to itself, an edge that already exists.
The reason appears where the pointer is rather than in a toast after the gesture.

Below the chain, a read-only **board preview** shows the Backlog columns this workflow produces —
gated columns marked, the stop where the flow ends, and the column a drop just added highlighted
where it landed.

## Capabilities

### New Capabilities

None. This is a new way to perform edits the product already supports, and it adds no rule.

### Modified Capabilities

- `automation-configuration`: the workflow's shape is editable from the picture that draws it, and
  the picture shows the board that shape produces.

## Impact

**Frontend only.** No endpoint, no schema, no migration: every gesture is an ordinary Automation
update, which is what keeps BR-003's overlap check and #115's self-trigger refusal applying
unchanged (design D4, inherited from #137).

- `features/automations/chainDrag.ts` (new) — the rules, as pure functions of the Automations.
- `features/automations/BoardPreview.tsx` (new) — the same derivation painted as columns.
- `WorkflowCanvas.tsx` — drop slots, refusals, the grab handle, one shared request builder.
- `AutomationsSection.tsx` — draggable catalogue rows, the rail as a drop target.
- `shared/http/mock.ts` — an update handler, which did not exist.

**Not touched.** The derived graph, the human-block gesture, the endpoint, and the width rule that
keeps drags off touch.
