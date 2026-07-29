# Tasks — edit-automation-form

- [ ] 1.1 The catalogue's form takes a mode: absent creates, an Automation edits (design D1).
- [ ] 1.2 The row gains an edit control that opens the form on that Automation.
- [ ] 2.1 Edit seeds every field from the Automation and submits the whole shape to
      `useUpdateAutomation` (design D2).
- [ ] 2.2 The timeout becomes a visible field in both modes, bounded by BR-005 and
      `PhaseBudget.MaximumMinutes`; blank means the default (design D2).
- [ ] 3.1 Changing to an action that reads no document clears the document name (design D3).
- [ ] 4.1 The API's refusal renders on the form, `detail` first, in create's voice (design D4).
- [ ] 5.1 i18n keys for the edit control, the timeout field and its hint; the mock still serves the
      catalogue.
- [ ] 6.1 E2E: edit changes a trigger label and the row shows it; an Automation with a non-default
      timeout keeps that timeout after an unrelated edit; a disabled Automation stays disabled; an
      overlapping edit renders the API's reason and changes nothing (design D5).
- [ ] 7.1 CI green; evidence on #151.
