## Context

`useBacklog` and `useStory` (`src/frontend/features/backlog/useBacklog.ts:15-30`) are plain
`useQuery` calls with no `refetchInterval` and no per-hook `staleTime`. They fetch on mount and on
explicit invalidation — `useConfigureConnector`, `useWriteStoryLabel` and `useRefreshBacklog` all
invalidate `["backlog", projectId]` on settle. Nothing else re-reads. Meanwhile
`BacklogSynchroniser` reconciles the Mirror every 60 seconds, so the server is current and the
screen is not.

Six other feature hooks already declare an interval — `useRuns` 10s, `useSandboxes` 10s,
`useInbox` 30s, `useInFlight` 30s, `useConnectorHealth` 30s, `useRuntimes` 30s. The backlog is the
outlier, not the pioneer. The global client sets `retry: 1, staleTime: 30_000`
(`src/frontend/shared/query/queryClient.ts:6`).

Both read endpoints are Postgres-only (`GetBacklog.cs:65-84`); only `POST /backlog/refresh` reaches
the vendor. So the whole problem, and the whole fix, is client-side.

**Library behaviour, verified rather than assumed** (`@tanstack/query-core@5.101.4`, the installed
version):

- `queryObserver.js:215` — the interval timer fires only when
  `this.options.refetchIntervalInBackground || focusManager.isFocused()`.
- `focusManager.js:55-60` — `isFocused()` returns `document.visibilityState !== "hidden"`, and the
  manager subscribes to `visibilitychange` (`focusManager.js:12`).
- `queryObserver.js:450-456` — `shouldFetchOn` returns
  `value === "always" || (value !== false && isStale(query, options))`. A plain
  `refetchOnWindowFocus: true` is therefore gated by `staleTime`; only the literal `"always"`
  bypasses it.

These three facts decide most of what follows.

## Goals / Non-Goals

**Goals:**

- An open backlog list, Kanban board and Story detail converge on the Mirror with no user action.
- Focusing a background tab shows current content without waiting out the interval.
- Zero additional vendor calls, at any tab count.
- A hidden tab costs nothing.
- One stated end-to-end lag number, in the DEC-050 form.

**Non-Goals:**

- Removing or shrinking the Mirror — DEC-029 is locked and its reasoning (trigger diffing,
  run-history joins, fast UI, rate-limit safety) is exactly what this change relies on.
- Changing the 60s server poll (BR-015, DEC-028).
- SSE, SignalR or websockets.
- Freshness of the runs, inbox or pulse surfaces — they already declare their own intervals and are
  not touched.
- Putting document content on an interval.

## Decisions

### D1 — The interval is declared on the hook, not on the screen

`refetchInterval` goes on `useBacklog` and `useStory` in
`src/frontend/features/backlog/useBacklog.ts`. The board (`KanbanBoard.tsx`), the project screen
(`ProjectScreen.tsx`) and the story screen (`StoryScreen.tsx`) then inherit it by consuming those
hooks and need no code of their own — which is what makes acceptance criterion 3 fall out rather
than be built.

*Rejected:* a `useEffect` + `setInterval` per screen, or an options bag threaded from each screen
(the shape `useSandboxes`/`useRuntimes` use). Both would put the freshness policy in three places
and let the three surfaces drift apart. The options-bag shape exists in those two hooks because
their callers genuinely want different rates (`RuntimesScreen` 5s vs `EnvironmentChip` 60s); the
backlog has no such caller, so adding the knob would be speculative.

### D2 — 30 seconds, and the stated lag is ≤ 90 s

`refetchInterval: 30_000`. It matches the tier the other non-Run surfaces already sit in
(`useInbox`, `useInFlight`, `useConnectorHealth`, `useRuntimes` are all 30s), it stays at or under
the 60s server poll as the spec requires, and it means a change at the vendor is on screen within:

> **Stated lag ≤ 90 s** — 60 s server poll (BR-015) + 30 s client interval.

That is the DEC-050 form: DEC-050 states the Run log window as "≤5s (500ms flush + 3s poll)". The
number is a budget, not a guarantee of promptness — the average is roughly half of it.

*Rejected:* 60s (equal to the server poll, doubling worst-case lag to 120s for no saving, since the
read is a local Postgres query); 10s (the `useRuns` tier — justified there because a Run is
actively moving, whereas a backlog changes at human speed, and it would triple the request rate
against a read nobody is waiting on).

### D3 — `refetchOnWindowFocus: "always"`, not a lowered `staleTime`

Acceptance criterion 2 says returning to the tab is *immediate*. With the global
`staleTime: 30_000` and the default `refetchOnWindowFocus: true`, a focus event inside 30s of the
last fetch is suppressed (`queryObserver.js:450-456`, verified above) — the Member would focus the
tab and see stale content, which is the exact complaint this change exists to fix. Setting
`refetchOnWindowFocus: "always"` on these two hooks bypasses the stale gate for the focus path
only.

*Rejected:* setting `staleTime: 0` on the hooks. It would fix focus, but it also makes every
mount and every observer subscription refetch, and it changes cache semantics for the many
components that read the same key. `"always"` scopes the override to the one event the criterion
is about.

*Note:* focus here means `visibilitychange` — `focusManager.isFocused()` is
`document.visibilityState !== "hidden"`. Switching tabs fires it; clicking between windows on the
same visible tab may not. That is the right granularity for the criterion as written ("in a
background tab … focuses the tab").

### D4 — Hidden-tab idleness is asserted, not implemented

`refetchIntervalInBackground` defaults to `false`, and `queryObserver.js:215` gates the timer on
`focusManager.isFocused()`. So acceptance criterion 7's first half already holds and requires no
code — we must simply *not* set `refetchIntervalInBackground: true`. The spec scenario exists so a
future change cannot flip it silently; the test asserts the behaviour, not our implementation of
it.

### D5 — No new freshness mechanism

Everything above is TanStack Query options on two existing hooks. No new dependency, no new
transport, no server change. DEC-050 chose a poll with a stated lag over the SignalR hub for the
Run log — the harder case, where the data moves every few hundred milliseconds. A backlog that
changes at human speed does not warrant more.

### D6 — Documents stay off the interval

`useStoryDocuments` and `useStoryDocumentContent` are untouched. Document content is a live read at
the head ref (design D3 of the documents change; the hook already carries the comment "there is no
cache to invalidate because there is no cache" at `useBacklog.ts:49`). Those *are* vendor-facing,
so putting them on an interval would breach acceptance criterion 4 as well as 6.

### D7 — Stale-not-empty is inherited, not re-implemented

Acceptance criterion 5 asks that an automatic re-read still degrade to stale rather than empty.
That guarantee lives server-side (`BacklogSynchroniser.cs:44-49`, and the `backlog-mirror`
requirement "a failed poll degrades to stale, never to empty"): a failed vendor poll leaves the
mirrored Stories in Postgres and records the failure on the Connector. Since an automatic re-read
is the same Postgres-only GET the mount already issues, it returns the same stale-plus-failure
payload. Nothing new is built here — a test pins it so the claim is exercised rather than asserted.

### Design system

No visual change: this change adds no markup, no copy and no token usage. `docs/design-system/`
and the derived `DESIGN.md` govern nothing new here, and the i18n catalog gains no key. The three
absences (no Connector, no Stories, poll failed) keep the treatment
`frontend-architecture`/"backlog data surfaces show only facts from the live response" already
requires.

## Risks / Trade-offs

- **A background re-read replaces good data with an error state** → TanStack Query keeps the last
  successful data on a failed background refetch (the query stays `success` with `isFetching`),
  so a transient blip does not blank the screen. The stale-not-empty test (D7) covers the
  server-side half.

- **The interval fights a drag on the Kanban board** — a re-read landing mid-drag could reorder
  under the pointer → `useMoveStory` already owns the optimistic path for moves; the re-read
  returns the same Mirror content the board is showing. Worth an explicit check during
  implementation, and a task covers it.

- **Request volume: one extra GET per 30 s per open tab per project** → both endpoints are indexed
  Postgres reads with no vendor hop; this is the same order the six existing interval hooks already
  impose. Acceptance criterion 4 constrains the thing that actually matters (vendor calls), and it
  is exactly zero.

- **`"always"` on focus could surprise a future reader** as a deviation from the app-wide
  `staleTime` → the rationale is recorded in D3 and a code comment points at the criterion, in the
  house style already used throughout `useBacklog.ts`.

- **The stated lag is a budget, not a promise** → it is written as a worst case with both
  components named, so it cannot be misread as "updates arrive in 90 seconds".

## Migration Plan

None. No schema change, no API change, no config. The change is additive query options; rolling
back is deleting them. Nothing persists.

## Open Questions

None. No `OPN-*` decision is implicated: DEC-029 (Mirror stays), DEC-030 (rate-limit safety),
DEC-050 (poll with a stated lag) and BR-015 (60s poll) are all locked and all point the same way.
