## 1. Make the backlog surfaces re-read

- [ ] 1.1 Add `refetchInterval: 30_000` and `refetchOnWindowFocus: "always"` to `useBacklog` in
      `src/frontend/features/backlog/useBacklog.ts`, with a comment in the file's existing house
      style recording why `"always"` is needed (the global `staleTime: 30_000` would otherwise
      suppress the focus re-read — design D3) and naming the ≤90s stated lag.
- [ ] 1.2 Add the same two options to `useStory` in the same file, so the Story detail view and the
      Kanban board inherit the behaviour with no per-screen code (design D1).
- [ ] 1.3 Confirm by reading `KanbanBoard.tsx`, `ProjectScreen.tsx` and `StoryScreen.tsx` that each
      consumes `useBacklog`/`useStory` and needs no change; if any holds its own copy of the data
      or its own timer, note it here before proceeding.
- [ ] 1.4 Verify `useStoryDocuments` and `useStoryDocumentContent` remain untouched — no interval,
      no `"always"` (design D6, acceptance criterion 6).
- [ ] 1.5 Verify `refetchIntervalInBackground` is set nowhere in the file, so the hidden-tab timer
      stays gated on `focusManager.isFocused()` (design D4).

## 2. Check the interval against the board's drag

- [ ] 2.1 With the interval live, drag a card on the Kanban board and hold it across an interval
      boundary; confirm the re-read does not reorder or snap the card under the pointer.
- [ ] 2.2 If it does, reconcile with the optimistic path already in `useMoveStory.ts` rather than
      by disabling the interval, and record what was needed in the change's design notes.

## 3. Pin the behaviour with tests

- [ ] 3.1 Add an end-to-end test in `src/tests/AiOrchestrator.EndToEndTests/` named per the
      `Subject_Should_Constraint` convention (e.g. `BacklogFreshness_Should_Constraint.cs`) that
      opens a project backlog, mutates a `StubIssue`'s title in `GitHubStub.Issues`, lets the
      server poll reconcile, and asserts the open page shows the new title with no click
      (acceptance criterion 1).
- [ ] 3.2 Extend that test — or add a sibling case — asserting the same for a label change reaching
      the Kanban board's new column and the Story detail's label list (acceptance criterion 3).
- [ ] 3.3 Assert no vendor amplification: capture `GitHubStub.Requests.Count`, let one or more
      client intervals elapse with the page open, and assert the count is unchanged — every
      automatic re-read was served from Postgres (acceptance criterion 4).
- [ ] 3.4 Assert stale-not-empty survives: with the stub failing, let an automatic re-read happen
      and assert the previously mirrored Stories are still rendered and the failure is surfaced —
      never an empty backlog (acceptance criterion 5, design D7).
- [ ] 3.5 Assert hidden means idle: hide the page via the Playwright page's visibility state, let an
      interval elapse, and assert no re-read was issued (acceptance criterion 7).
- [ ] 3.6 Assert the focus re-read is not suppressed by the global stale time: re-show the page
      inside 30 s of the last fetch and assert a re-read occurs (acceptance criterion 2).

## 4. Record the lag

- [ ] 4.1 Confirm `design.md` states the end-to-end lag as **≤ 90 s = 60 s server poll + 30 s client
      interval**, naming both components in the DEC-050 form (acceptance criterion 7, second half).

## 5. Verify with the CI-equivalent gates

- [ ] 5.1 `pnpm --dir src/frontend format:check` (Prettier).
- [ ] 5.2 `pnpm --dir src/frontend lint` (ESLint `--max-warnings=0`).
- [ ] 5.3 `pnpm --dir src/frontend typecheck` (`tsc --noEmit`).
- [ ] 5.4 `pnpm --dir src/frontend build` — the full production pipeline
      (`tsc --noEmit && vite build && assert-no-mock`). **Run this before the E2E suite**: E2E boots
      the real AppHost and serves the built bundle from `wwwroot`, so a `.ts`/`.tsx` edit is
      invisible to it until the build runs. Check the build's real exit code rather than trusting a
      filtered wrapper's summary.
- [ ] 5.5 `dotnet build` for the solution, then run the end-to-end tests and confirm the new cases
      pass and no existing backlog, board or projects-tree test regressed.
- [ ] 5.6 Run `openspec validate backlog-auto-refresh --strict` and confirm it passes.
