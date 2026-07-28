# Proposal: kanban-board

## Why

Issue #110 (ACT-002, Member), last of the frontend chain and a view over everything the others
built. Dragging a card already has a meaning in this product: dropping into a column is applying
a trigger label (UC-008, DEC-027), and ordinary matching fires the Run. The board is the
grill→ready→propose→implement pipeline made spatial, with **zero new dispatch machinery** — and
because columns derive from the project's enabled Automations, the defaults button (#76) hands
every new project a working board.

## What changes

- **A board view on the Operate tab**, toggled with the list: one column per enabled Automation
  trigger label, plus "Untouched" for stories carrying none. Columns are derived, never
  configured — that derivation is the feature.
- **Moving a card is UC-008's existing label write**: entering a trigger column applies the
  label, leaving one removes it. The move is optimistic and reconciles to vendor truth (BR-008):
  a refusal returns the card to its column carrying the refusal sentence, with the mirror
  unchanged.
- **Cards wear their Run state**: an unanswered question with its age, a plan awaiting approval,
  executing with the live-log link, failed. For stories, the board absorbs the attention strip.
- **BR-001 as physics**: a story with an active Run refuses the drop before any write, naming the
  rule — better than letting matching refuse silently after the label already landed.
- **Approval-gated columns say so**, and a card dropped there reports that its plan awaits
  (DEC-040).
- **Every move has a menu**, on every width (design D1): the menu is the semantics, the drag is
  sugar.

## Impact

- Specs: `backlog-mirror` (one ADDED requirement).
- Code: frontend only. No endpoint, no rule, no schema change — the board writes through the
  label endpoint that already exists and reads the queries the page already polls.
- No new runtime dependency (design D1).

## Out of scope

Reordering within a column (labels are sets; order means nothing), columns detached from
Automations, and per-column WIP limits.
