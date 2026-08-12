## 1. Make the backlog surfaces re-read

- [x] 1.1 Add `refetchInterval: 30_000` and `refetchOnWindowFocus: "always"` to `useBacklog` in
      `src/frontend/features/backlog/useBacklog.ts`, with a comment in the file's existing house
      style recording why `"always"` is needed (the global `staleTime: 30_000` would otherwise
      suppress the focus re-read — design D3) and naming the ≤90s stated lag.
- [x] 1.2 Add the same two options to `useStory` in the same file, so the Story detail view and the
      Kanban board inherit the behaviour with no per-screen code (design D1).
      Both hooks spread one `mirrorFreshness` const so the two cannot drift apart.
- [x] 1.3 Confirm by reading `KanbanBoard.tsx`, `ProjectScreen.tsx` and `StoryScreen.tsx` that each
      consumes `useBacklog`/`useStory` and needs no change; if any holds its own copy of the data
      or its own timer, note it here before proceeding.
      **Confirmed, no change needed.** `ProjectScreen.tsx:87` derives `stories` straight from
      `backlog.data?.stories ?? []` and passes it to `<KanbanBoard stories={…}>` (`:357-362`);
      `StoryScreen.tsx:15` calls `useStory` directly. `KanbanBoard` takes `stories` as a prop and
      re-derives its columns by `.filter()` on every render — its `useState` calls are all
      interaction state (`dragging`, `over`, `carried`, `refused`, `moving`, `activeColumn`), never
      a copy of the server data, and no screen runs a timer of its own.
- [x] 1.4 Verify `useStoryDocuments` and `useStoryDocumentContent` remain untouched — no interval,
      no `"always"` (design D6, acceptance criterion 6).
- [x] 1.5 Verify `refetchIntervalInBackground` is set nowhere in the file, so the hidden-tab timer
      stays gated on `focusManager.isFocused()` (design D4).

## 2. Check the interval against the board's move

> **Revised during implementation.** 2.1 as written assumed a Playwright drag was available. It is
> not: `KanbanBoard_Should_Constraint.cs:74-75` records that "Playwright cannot perform an HTML5
> drag anyway, which is precisely why the menu is the semantics and the drag is sugar." The
> hazard is therefore checked by reading the code and asserted through the move menu — the path
> the repository already treats as the semantics.

- [x] 2.1 Establish by reading `KanbanBoard.tsx` and `useMoveStory.ts` whether an interval re-read
      can disturb an in-progress move.
      **It cannot, and the guard predates this change.** `useMoveStory.onMutate:52` already calls
      `queryClient.cancelQueries({ queryKey: backlogKey })` with the comment "In-flight refetches
      would overwrite the optimistic move with pre-move truth" — the optimistic path was built
      against exactly this hazard, so adding a scheduled refetch introduces no new one. During the
      drag itself the board holds no copy of the data: `dragging` is `{ story: vendorId, from }`
      (an id, not an object), the dragged card is resolved fresh at drop time via
      `stories.find(c => c.vendorId === dragging?.story)` (`:322`), and cards are keyed
      `key={story.vendorId}` (`:375`), so a re-read returning identical content reconciles to the
      same DOM nodes and cannot reorder or remount the card under the pointer.
- [x] 2.2 No reconciliation work was needed, and the interval was not disabled. Covered by test
      3.7, which asserts the optimistic move survives an interval re-read through the move menu.

## 3. Pin the behaviour with tests

> **Revised during implementation.** The E2E harness sets `Backlog__PollingEnabled = "false"`
> (`AppHostFixture.cs:95`), so the server's 60s background poll does not run under test. Tests
> reconcile the Mirror out-of-band with `POST /backlog/refresh` through `page.APIRequest` — a
> request context separate from the browser, so it never invalidates the page's TanStack cache.
> The page must therefore catch up purely through the behaviour under test, which makes this a
> *sharper* assertion of the client claim than a background poll would have been.
>
> Hiding a tab is done by dispatching the real `visibilitychange` event with
> `document.visibilityState` overridden — the one signal `focusManager` reads
> (`isFocused()` is `document.visibilityState !== "hidden"`). `BringToFrontAsync()` was the first
> choice and was rejected: the harness launches headless Chromium
> (`AppHostFixture.cs:173`), where tab activation does not reliably move `visibilityState`.

- [x] 3.1 Add an end-to-end test in `src/tests/AiOrchestrator.EndToEndTests/` named per the
      `Subject_Should_Constraint` convention (`BacklogFreshness_Should_Constraint.cs`) that opens a
      project backlog, mutates a `StubIssue`'s title in `GitHubStub.Issues`, reconciles the Mirror
      out-of-band, and asserts the open page shows the new title with no click (AC 1).
- [x] 3.2 Assert the same for a label change reaching the Kanban board's new column and the Story
      detail's label list (AC 3).
- [x] 3.3 Assert no vendor amplification: capture `GitHubStub.Requests.Count`, let a client
      interval elapse with the page open, confirm from the browser's own request log that a
      re-read did happen, and assert the vendor count is unchanged (AC 4).
- [x] 3.4 Assert stale-not-empty survives: remove the repository from `GitHubStub.Repositories` so
      the next reconciliation 404s, then let an automatic re-read happen and assert the previously
      mirrored Stories are still rendered — never an empty backlog (AC 5, design D7).
- [x] 3.5 Assert hidden means idle: hide the page, let an interval elapse, and assert no `/backlog`
      re-read was issued (AC 7).
- [x] 3.6 Assert the focus re-read is not suppressed by the global stale time: re-show the page
      inside 30 s of the last fetch and assert a re-read occurs (AC 2).
- [x] 3.7 Assert an interval re-read does not clobber an in-progress optimistic move, exercised
      through the move menu (task 2.1's hazard, via the path the repository treats as the
      semantics).

## 4. Record the lag

- [x] 4.1 Confirm `design.md` states the end-to-end lag as **≤ 90 s = 60 s server poll + 30 s client
      interval**, naming both components in the DEC-050 form (acceptance criterion 7, second half).
      Stated in design.md D2, and repeated in the `mirrorFreshness` doc comment in
      `useBacklog.ts` so the number is readable where the interval is set, not only in the change.

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
