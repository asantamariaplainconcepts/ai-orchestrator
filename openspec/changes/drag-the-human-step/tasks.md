# Tasks — drag-the-human-step

- [ ] 1.1 The human block is draggable from the catalogue (design D1) and the gaps between steps are
      drop targets. Dropping sets `requiresApproval` on the following step.
- [ ] 2.1 Removing the block clears it, and moving it does both sides in one gesture — new gate set
      before the old one is cleared, so an interrupted move fails safe (design D2).
- [ ] 3.1 Refusals are the ordinary Automation update's, shown with the API's reason, and the canvas
      returns to what is stored (design D3).
- [ ] 4.1 Valid gaps are marked during a drag; a gap that would be refused is visibly not a target
      (design D4).
- [ ] 5.1 The approval button stays as the explicit control at every width, and dragging is not
      offered below the wide breakpoint (design D5).
- [ ] 6.1 Four states, both themes, copy through the i18n catalogue, focus visible, and the drag
      controls carry accessible names.
- [ ] 7.1 CI green; evidence on #137.
