## Why

A Member watching a project's backlog sees whatever was true when the page mounted. The server
keeps the Mirror current every 60 seconds (BR-015), but the browser never re-reads it: `useBacklog`
is a plain `useQuery` with no `refetchInterval`, so it fetches on mount and on explicit
invalidation only. The screen silently drifts and the Member must press refresh to trust it — and
when the board is the surface you drive the pipeline from (UC-007, UC-022, ACT-002 Member), a stale
board makes the whole loop feel unreliable. This closes the gap between "the Mirror is fresh" and
"the screen shows it".

The staleness is entirely client-side. `GET /api/projects/{id}/backlog` and
`GET .../stories/{id}` read Postgres only — neither makes a vendor call — so automatic re-reads
cost no vendor quota and DEC-030's rate-limit concern does not apply. Only `POST /backlog/refresh`
reaches the vendor, and it is untouched here.

The Mirror stays. This change deliberately does **not** revisit DEC-029 (locked): reconciliation
diffs incoming vendor state against the *persisted* Stories, and that diff is the only thing
distinguishing "the trigger label was just applied" from "the label is still there" — without it,
BR-001's Queued/AwaitingInput/Executing guard would let every poll create a new Run once the first
went terminal.

## What Changes

- `useBacklog` gains a declared refetch interval, so an open backlog list catches up with the
  Mirror unattended.
- `useStory` gains the same, so the Story detail view and the Kanban board — both fed from these
  two hooks — inherit the behaviour without their own mechanism.
- Both gain a re-read on window focus that is not suppressed by the global `staleTime: 30_000`
  default, so returning to a background tab shows current content without waiting out the interval.
- Hidden tabs stay idle: no interval re-read is issued while the document is hidden.
- The end-to-end lag budget is stated in the DEC-050 style, as the sum of the server poll interval
  and the client interval.
- `useStoryDocuments` / `useStoryDocumentContent` are explicitly left on demand (design D3) — an
  automatic re-read never pulls document content.

No **BREAKING** changes. No API, outbox message schema, Aspire, host csproj or CI contract is
touched — this change adds query options to two frontend hooks.

## Capabilities

### New Capabilities
- `backlog-auto-refresh`: the browser keeps the backlog surfaces level with the Mirror on its own —
  an interval re-read, a focus re-read, idleness while hidden, a stated end-to-end lag, and the
  guarantee that none of it reaches the vendor.

### Modified Capabilities
<!-- None. The behaviour this change adds is new; it modifies no existing requirement.
     backlog-mirror's "a failed poll degrades to stale, never to empty" and
     frontend-architecture's "backlog data surfaces show only facts from the live response"
     both continue to hold unchanged, and the new capability's scenarios assert that they do. -->

## Impact

**Code** — `src/frontend/features/backlog/useBacklog.ts` (`useBacklog`, `useStory`). The board
(`KanbanBoard.tsx`), the project screen (`ProjectScreen.tsx`) and the story screen
(`StoryScreen.tsx`) consume those hooks and change only by inheriting them.

**Conventions followed, not invented** — six feature hooks already declare `refetchInterval`
(`useRuns` 10s, `useInbox` 30s, `useInFlight` 30s, `useConnectorHealth` 30s, `useRuntimes` 30s,
`useSandboxes` 10s). This change puts the backlog on the same footing; it does not introduce a new
freshness mechanism, and it does not touch those hooks.

**APIs** — none. Both endpoints already exist and are Postgres-only (`GetBacklog.cs:65-84`).

**Server** — none. `BacklogSynchroniser`'s 60s schedule, its reconciliation diff and its
degrade-to-stale behaviour are unchanged (BR-008, BR-015).

**Sequencing** — behind `shell-projects-tree` (#335, merged as `9f543e1`) per RULE-004, which
restructured the shell around `ProjectScreen`. No Foundation work required. No open `OPN-*`
decision is involved.

**Out of scope** — removing or shrinking the Mirror (DEC-029 stands); changing the 60s server poll
(BR-015, DEC-028); SSE/SignalR/websockets (DEC-050's poll-with-a-stated-lag precedent stands); a
client re-read triggering a vendor poll (DEC-030); freshness of the runs, inbox or pulse surfaces;
putting document content on an interval.
