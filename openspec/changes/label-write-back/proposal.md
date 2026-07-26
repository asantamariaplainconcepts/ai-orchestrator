# Proposal: label-write-back

## Why

Issue #24 (UC-008). The loop is closed (#17) and observable (#20) but only drivable by
labelling at the vendor. DEC-027 locked the trigger UX: the vendor-side label is the single
trigger semantics and the website applies/removes it **through the Connector** — one mechanism,
two entry points. This is the first write through a seam that has been read-only since #7.

## What Changes

- **The Connector seam gains its first writes**: `ApplyLabel` / `RemoveLabel` on
  `IBacklogConnector`, with the GitHub implementation via Octokit and the same error
  translation discipline as reads (unavailable vs permission vs not-found stay distinct).
- **Two endpoints on the Backlog module**:
  `PUT`/`DELETE /api/projects/{projectId}/backlog/stories/{vendorStoryId}/labels/{label}`.
  Order is load-bearing (BR-008): write to the vendor first, then re-synchronise the mirror
  through the existing `BacklogSynchroniser` — the same path polling takes, so the mirror
  update and the `StoryChanged` event that matching consumes come from the ordinary machinery,
  never from a local patch.
- **Portal**: backlog rows offer apply/remove for enabled Automations' trigger labels; other
  labels remain read-only pills.

## Impact

- Affected specs: `backlog-mirror` (adds the write-back requirement).
- Touched: Backlog module (seam + GitHub impl + two use cases + synchroniser reuse), frontend
  (backlog table + catalog), Backlog + Runs functional tests (stub gains label mutation; the
  full portal-probe scenario becomes testable end-to-end).
- Out of scope: webhooks (#31), arbitrary label management UI, optimistic UI.
