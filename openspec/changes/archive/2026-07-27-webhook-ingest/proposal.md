# Proposal: webhook-ingest

## Why

Issue #31 (UC-010). Polling shipped first per DEC-028 and gives a worst case of one interval
between a label and a Run. Webhooks close that gap — and BR-015 makes *how* the interesting
part: webhook and polling events must normalise identically, or detection starts depending on
which path a change arrived by.

## What Changes

- **`POST /api/webhooks/{vendor}`** — verifies the vendor's signature, identifies the Connector
  by the repository the payload names, and runs the **same** `BacklogSynchroniser` a poll runs.
- **The payload is a hint, not data.** Nothing is read from it except the repository and enough
  to decide the event is interesting; the resulting `StoryChanged` comes from the reconciler,
  so BR-015 holds by construction rather than by two implementations agreeing.
- **A webhook secret name on the Connector**, resolved through `ISecretResolver` like every
  other credential (BR-010) — the value is never stored.
- **Polling keeps running.** A webhook that never arrives, or is refused, costs latency only.

## Impact

- Affected specs: `backlog-mirror` (ingest).
- Touched: Backlog module (Connector gains a webhook secret name + migration, one endpoint, a
  signature verifier), functional tests, README (how to configure it at the vendor).
- Out of scope: Azure DevOps webhooks (#29, OPN-003), registering the webhook from the portal,
  event-type filtering beyond "is this interesting".
