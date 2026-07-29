# Tasks — human-step-column

- [x] 1.1 `BoardAutomation` gains `outputLabel`; the board derives its column order from the chain,
      with unchained Automations after the ordered ones (design D1).
- [x] 2.1 A human column after a step that hands work to nobody, holding the Stories that step
      finished — own header, count, empty state (design D2).
- [x] 3.1 An approval-gated step keeps its badge and gets no human column; a Run awaiting an approval
      or an answer stays in its step's column with its state on the card (design D2/D4).
- [x] 4.1 Placing the column clears the preceding step's output label through the ordinary update, and
      a refusal is shown with its reason (design D3).
- [x] 5.1 Removing the cause removes the column and returns its Stories to the columns matching their
      labels.
- [x] 6.1 Every gesture has an explicit control at every width; a project with no chain renders as it
      does today.
- [x] 7.1 Four states, both themes, i18n catalogue, focus visible.
- [ ] 8.1 CI green; evidence on #128.
