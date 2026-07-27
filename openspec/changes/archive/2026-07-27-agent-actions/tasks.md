# Tasks — agent-actions

## 1. The seam

- [x] 1.1 `AddComment` and `SetState` on `IBacklogConnector`; GitHub implementations (comment
      via Octokit; state mapped to open/closed, anything else refused — design D4); stubs in
      both fixtures.
- [x] 1.2 A Contracts write surface so the Runs module reaches them without the Backlog
      implementation, as it does for reads.

## 2. Dispatch

- [x] 2.1 `RunExecutor` switches on the action (design D1): PR action unchanged; the other
      three build their own instruction, skip the workspace, and consume the answer — comment,
      state, or estimate label + comment (D2/D3).
- [x] 2.2 Unusable answers fail with their reason: no number in an estimate, a rejected state.

## 3. Tests

- [x] 3.1 Each action reaches its own seam write and succeeds; the workspace is untouched for
      the three non-PR actions.
- [x] 3.2 An estimate replaces a prior `estimate:*` label rather than adding a second (D3/D5).
      **Found by writing it:** the first implementation read the existing labels from the
      *Mirror*, which is a poll behind — so a second estimate before the next refresh left the
      Story carrying both. The seam gained `FetchStory` and the replace now reads vendor truth
      (BR-008).
- [x] 3.3 A non-numeric estimate answer and a rejected state each fail the Run with that reason.

## 4. Close-out

- [x] 4.1 ARCHITECTURE.md: the catalogue is fully executable; the "not executable yet" copy and
      its frontend hint go. CI's own filtered command locally; CI green.
