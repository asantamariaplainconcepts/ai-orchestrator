# Design: kanban-board

## D1 — The menu is the feature; the drag is sugar (and needs no library)

Acceptance criterion 6 requires that every move possible by drag is possible without it. Once
that menu exists — and it must, for phones and for keyboards — a drag library buys exactly one
thing this change wants: touch dragging, which #110 explicitly puts out of scope. So the drag
uses the browser's own HTML5 drag events for pointer devices, and **every** card carries its
"Move to…" menu at every width, not only on mobile.

This inverts the issue's framing (it assumed dnd-kit or similar) and is worth stating plainly:
Foundations' "prefer established open source over custom" is about not hand-rolling solved
problems, not about adding a dependency for a capability we already have twice over. Native
drag also cannot be made keyboard-accessible — the menu is what makes the board operable at
all, which is the argument for treating it as primary rather than as a mobile fallback.

If touch dragging is ever actually wanted, dnd-kit slots in behind the same move function with
no change to the write path. Recorded as the upgrade, not taken now.

## D2 — One move function, three ways to call it

Drag-drop, menu selection and (already existing) label pills all funnel into a single
`moveStory(vendorStoryId, fromColumn, toColumn)` that performs the same UC-008 writes in the
same order. Three gestures cannot drift in behaviour if they cannot take different paths. The
optimistic update, the BR-001 pre-check and the refusal rollback live in that one function.

## D3 — Refusals are pre-checked where the rule already is, and reconciled where truth is

Two different refusals, two different mechanisms:

- **BR-001 (active Run)** is checked client-side *before* the write, from the runs query the page
  already polls. This is a deliberate duplicate of a server rule, justified because the point is
  to refuse the *gesture* — letting the label land and the match silently decline would leave the
  vendor labelled and the board lying. The server remains the authority; the client is being
  polite, not authoritative.
- **A vendor refusal** cannot be predicted, so the card moves optimistically and returns on
  failure with the refusal sentence attached (BR-008). The mirror is never written by the board.

## D4 — Columns derive from Automations, and "Untouched" is not a column

Enabled Automations' trigger labels, deduplicated, in their configured order. Stories with none
of those labels gather in an "Untouched" pile that is a *source* only: dropping there means
"remove the trigger label you came from", which is exactly leaving a trigger column. Modelling
it as a real column with its own label would invent a vocabulary the vendor does not have.

## D5 — The board is a view, not a second data source

It reads the same backlog, runs and automations queries the list view already uses, and writes
through the same label mutation. No new endpoint, no new query key, no board-specific state
beyond the view toggle — so a story cannot appear in one view and not the other, and the poll
that updates the list updates the board (acceptance criterion 4 comes free from
reconciliation).
