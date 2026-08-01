## Context

The starter set is shipped content (versioned, tested); installing one is today a copy-paste
ceremony the human performs. The product already owns a bounded repository-write pipeline —
`ICodeWorkspace` clone/commit/push plus the PR-opening ceremony the Run executor uses for
implement/propose — exercised, stage-named refusals included. The spec's "SHALL NOT write"
guarded against two things: spending an agent pass to produce content the product holds, and
growing a write capability for a convenience. #214 reuses the existing capability and spends no
pass; the owner reversed the clause with that shape on the table (grill record on the issue).

## Goals / Non-Goals

**Goals:**
- One click takes a starter from "offered" to "reviewable PR" with zero manual file handling.
- The default branch is never written; a human merges the draft PR.
- Refusals name their stage (clone / push / PR) and the already-present case names the path.

**Non-Goals:**
- Committing to the default branch directly.
- Auto-creating an Automation after the merge.
- Editing starter content in the portal before install.
- Installing several starters in one PR (one starter, one branch, one PR — keep the review small).

## Decisions

**D1 — reuse the workspace publish pipeline; no agent involved.** The install use case prepares a
workspace exactly as implement does (clone with the PAT resolved by name at use, run-scoped
branch), writes the starter's bytes at `<prompts directory>/<filename>`, commits, pushes, opens a
draft PR. *Alternative rejected:* a vendor contents-API write (create file via REST) — it would be
a second, different write path with its own failure modes; the workspace pipeline is already
exercised and already names its failures.

**D2 — the branch is starter-scoped and deterministic** (`starter/<filename-slug>` or the
pipeline's run-scoped convention adapted): re-installing after a failed PR reuses or replaces the
branch rather than accumulating orphans. The PR body names the starter and its target path.

**D3 — already-present wins, checked before any workspace exists.** The offer already reports
presence (existing requirement); install re-checks the default branch at click time and refuses
naming the path — cheaper than a clone and honest under staleness.

**D4 — the PR URL is data on the response, rendered on the card.** No polling, no state stored:
the card shows the URL the install returned; the offer's presence reporting picks the file up
after the merge, which is also what flips the picker (#215) to offering it.

## Risks / Trade-offs

- [A pending install PR makes "already present" read false until merged] → acceptable and honest:
  presence reports the default branch, and the card holds the PR link the human should finish.
- [Branch litter from abandoned installs] → deterministic branch name (D2) means one branch per
  starter at most; a re-install force-updates it.
- [The PAT lacks PR-create scope on some repos] → the PR stage refusal names the vendor's reason
  (same voice as implement's).

## Migration Plan

Additive plus one spec-clause reversal recorded in the delta spec. No schema change, no data
migration. Rollback is reverting the change.

## Open Questions

(none — the write shape was settled at grill time on #214)
