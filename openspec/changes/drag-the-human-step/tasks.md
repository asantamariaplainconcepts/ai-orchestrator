# Tasks — drag-the-human-step

- [ ] 1.1 The human block is draggable from the catalogue and the gaps between steps accept it
      (design D1). Dropping clears the preceding step's output label — never `requiresApproval`.
- [ ] 2.1 Removal restores the label where the following step makes the destination unambiguous, and
      otherwise leaves the existing select as the control (design D2).
- [ ] 3.1 A move breaks the new gap before reconnecting the old, so an interruption leaves an extra
      review and never none (design D3).
- [ ] 4.1 Refusals are the ordinary update's, shown with the API's reason, and the workflow returns
      to what is stored (design D4).
- [ ] 5.1 Valid gaps are marked during a drag; a gap that would be refused is not a target.
- [ ] 6.1 The existing controls stay at every width and dragging is not offered where the flow reads
      vertically (design D5).
- [ ] 7.1 A test asserts the two human moments stay distinct: placing the block changes the output
      label and leaves `requiresApproval` alone.
- [ ] 8.1 Four states, both themes, i18n catalogue, focus visible, accessible names on drag controls.
- [ ] 9.1 CI green; evidence on #137.
