# Tasks — sync-action

- [x] 1.1 `SyncChange` in the catalogue and in the executor's dispatch, on the implement
      pipeline's shape.
- [x] 2.1 The procedure read from the repository (design D1), path configurable and defaulted in
      code; the prompt tells the agent to follow it exactly and to refuse rather than improvise,
      and to leave the pull request untouched on failure (design D5).
- [x] 3.1 Both refusals before the workspace (design D4): no open change, and no readable
      procedure — each naming what was missing.
- [x] 4.1 The seeded defaults gain the close-out step, approval-gated (design D3), chained from
      whatever the implement step hands on.
- [x] 5.1 Tests: the close succeeds and records; each refusal fires before any workspace is
      prepared; a custom path is used exactly; the defaults include it gated.
- [x] 6.1 The portal offers the action; CI green; evidence on #123.
