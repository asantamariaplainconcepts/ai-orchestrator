# Tasks — archive-project

- [ ] 1.1 `ArchivedAt` on the Project (design D3) with its migration, and the archive/restore
      slices — the archive requiring the name as confirmation (design D4).
- [ ] 2.1 The state on the Projects Contracts surface, read per decision and never copied
      (design D1).
- [ ] 3.1 Backlog's poller skips archived projects; Runs' matching creates nothing for them;
      manual dispatch refuses with the reason.
- [ ] 4.1 Reading stays open (design D2): no read path gains an archived condition — asserted,
      not assumed.
- [ ] 5.1 The projects list excludes archived projects, states how many, and can show them; the
      Settings tab carries archive and restore.
- [ ] 6.1 Tests: each of the spec's scenarios, including the Run in flight that must finish.
- [ ] 7.1 Mock routes, design validator, CI green; evidence on #121.
