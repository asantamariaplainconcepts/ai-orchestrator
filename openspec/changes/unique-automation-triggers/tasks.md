# Tasks — unique-automation-triggers

- [ ] 1.1 `Automation.Overlaps` compares labels and states case-insensitively (design D4), and the
      comment that already claimed this stops being false.
- [ ] 2.1 An exact-duplicate rule independent of `Enabled` (design D3), used by the guard alongside
      subsumption.
- [ ] 3.1 A unique index over project, normalised label and normalised state, written as raw SQL
      because it is an expression index, with the NULL trap handled explicitly (design D1).
- [ ] 4.1 A unique violation maps to the same `TriggerOverlaps` refusal, never a 500 (design D2).
- [ ] 5.1 Matching compares labels and states the same way (design D4) — one comparison, two callers.
- [ ] 6.1 BR-003 reworded and DEC-056 recorded (design D5).
- [ ] 7.1 Tests: a differently-cased duplicate is refused; a disabled exact duplicate is refused; a
      disabled subsuming sibling still allows a narrower enabled one; a wrong-cased Automation fires
      for a Story labelled in the other case; two concurrent identical saves produce one row and one
      refusal.
- [ ] 8.1 CI green; evidence on #147.
