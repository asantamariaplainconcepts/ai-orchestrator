# Tasks — reap-from-the-phase

- [x] 1.1 The deadline is measured from the current phase's start (design D1): `ApprovedAt` where a
      Run is executing after approval, `StartedAt` otherwise.
- [x] 2.1 Tests: an approved Run that waited longer than its timeout is untouched; the same Run with
      an overdue *executing* phase is still reaped. Both were confirmed red before the fix.
- [x] 3.1 The requirement says "the start of its current phase" and gains the missing scenario
      (design D2).
- [ ] 4.1 CI green; evidence on #146.
