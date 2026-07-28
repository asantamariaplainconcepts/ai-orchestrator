# Tasks — automation-output-label

- [x] 1.1 Rename `ReadyLabel` to `OutputLabel` across the domain, the Contracts surface, the
      use cases and the form, with a data-preserving migration (design D1). Check every call
      site of `Create`/`UpdateTo` — optional parameters have silently dropped a field here
      before.
- [x] 2.1 The executor writes it once, on success, for every action (design D2); the grill's
      code default applies only to the grill action.
- [x] 3.1 The self-trigger refusal at save (design D3), with its sentence.
- [x] 4.1 Tests: the chain for a non-grill action, silence when unset, nothing on
      failure/cancellation, the refusal, and a pre-existing grill unchanged after migration.
- [ ] 5.1 CI green; evidence on #115.
