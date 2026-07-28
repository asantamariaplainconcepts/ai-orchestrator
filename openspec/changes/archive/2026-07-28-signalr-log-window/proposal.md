# Proposal: signalr-log-window

## Why

Issue #106 (ACT-002, Member), the latency upgrade DEC-050 recorded when it chose the poll. #96
shipped following at ≤5s — the difference between a spinner and a window. This is the difference
between a window and presence: sub-second lines, and fanout that costs one push per viewer
instead of one query per viewer every three seconds.

## What changes

- **A SignalR hub in the portal**, one group per Run. No new Azure resource, so it behaves the
  same under `aspire run`, the self-host compose and ACA (DEC-049).
- **The portal learns of new lines from Postgres, not from the pod** (design D1). A `NOTIFY` on
  commit wakes a listener in the portal, which reads the new chunks and pushes them to the
  group. The dispatch worker is not modified at all.
- **The flush interval drops** from 2s to 500ms (design D2), because with the pod out of the
  path the flush *is* the latency. Batching by size is unchanged, so a noisy Run still commits
  in batches of fifty.
- **The poll stays** as the fallback and the guarantee (design D3): the hub is best-effort on
  top, and the page states nothing is lost when it is unavailable, because Postgres is the
  record either way (BR-014).

## Impact

- Specs: `run-orchestration` (one ADDED requirement).
- Code: portal — hub, listener, and the Run page preferring it; `RunLogWriter`'s interval.
- **No worker change, and no ingest authentication** — see D1. The issue anticipated a per-Run
  token resolved from the vault; this design removes the need for one.
- No schema change.

## Out of scope

Azure SignalR Service (managed scale-out), and streaming anything but Run output.
