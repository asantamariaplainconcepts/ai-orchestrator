# Tasks — run-execution-resilience

- [x] 1.1 A 60-minute ceiling on the phase timeout, refused at save naming it (design D1), with the
      constant, the Terraform value and BR-005 each referencing the other two.
- [x] 2.1 Terraform's replica timeout raised to the ceiling plus a drain margin, comment included.
- [x] 3.1 The worker stops claiming when its remaining budget is under one phase timeout, and exits
      cleanly (design D2).
- [x] 4.1 The reap grace default becomes 300s (design D3), still configurable.
- [x] 5.1 The notifier evicts a terminal Run's cursor and serialises per Run (design D4).
- [x] 6.1 The portal subscribes before it reads, so the handshake window closes (design D5).
- [x] 7.1 DEC-050's flush figure corrected to 500ms; DEC-054 recorded; BR-005 bounded;
      ARCHITECTURE.md's crash story names the sweeper instead of *Run now* (design D6).
- [x] 8.1 Tests: the ceiling refuses and accepts at the boundary; a terminal Run leaves no cursor;
      concurrent deliveries produce no duplicate; a worker short of budget claims nothing.
- [ ] 9.1 CI green; evidence on #144.
