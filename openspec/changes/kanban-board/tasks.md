# Tasks — kanban-board

- [ ] 1.1 The view toggle on Operate (list ⇄ board, preference persisted) and the derived
      columns (design D4): enabled trigger labels plus the Untouched pile.
- [ ] 2.1 `moveStory` — the one write path all three gestures share (design D2), with the
      BR-001 pre-check and the optimistic-then-reconcile rollback (design D3).
- [ ] 3.1 Cards: Run state, question age, approval-awaiting, executing with the live-log link;
      approval-gated columns marked (DEC-040).
- [ ] 4.1 The "Move to…" control on every card at every width, and HTML5 drag for pointers
      (design D1) — no new dependency.
- [ ] 5.1 An E2E that drives the chain from the board: move onto a trigger column → label at the
      vendor stub → Run exists (acceptance criterion 2); plus the drag-free path asserted.
- [ ] 6.1 Design validator, mock routes, 375px pass, CI green; evidence on #110.
