# Proposal: run-execution-resilience

## Why

Issue #144 (ACT-002 watches; ACT-001 configures the ceiling). Watching an agent work is this
product's strongest trust surface (DEC-050), and today that surface tells three untruths.

**The infrastructure contradicts the rule.** `infra/dev/dispatch.tf` sets
`replica_timeout_in_seconds = 600` while BR-005 promises an Admin-configurable phase timeout whose
default is 30 minutes. Every implement Run over ten minutes is killed by the platform, not by its
own budget — and that is almost certainly what killed the worker behind Run
`019fac98-0924-7b58-8f81-3b0c57bbb40c`, which I diagnosed this morning as "stuck" when it was a
container the platform had already terminated.

**A decision states a number the code contradicts.** DEC-050 records "≤5s (2s flush + 3s poll)";
`RunLogWriter.FlushInterval` is 500ms.

**The live window leaks and can miss lines.** `RunLogNotifier`'s per-Run cursor is never evicted, so
it grows for the process's lifetime, and it is unsynchronised, so two notifications for one Run can
push the same frame twice. And a watcher who opens a page mid-Run can miss the lines committed
between its first fetch and its hub subscription — those wait for the reconciliation poll rather
than arriving inside the stated lag.

**The crash story in the docs is untrue.** `ARCHITECTURE.md:274` says a lost message is "recovered
by *Run now*". It is not: BR-001 holds the Story, so *Run now* answers `AlreadyActive`.

## What is already done, and not proposed again

**#144's G2 is delivered.** #140 added the sweeper and #146 corrected it to measure from the current
phase rather than from `StartedAt`, which had timed the approval wait BR-006 declares untimed. What
remains of G2 is its documentation bullet, which this change carries.

## What changes

- **One budget, bound at both ends** (design D1): a 60-minute ceiling on the configurable phase
  timeout, refused at save; Terraform's replica timeout ≥ ceiling plus a drain margin, with a
  comment binding the three numbers so drift is visible. Recorded as DEC-054, because it amends
  BR-005's "Admin-configurable" to bounded.
- **A worker that cannot finish a phase claims nothing** (design D2): when its remaining replica
  budget is under one full phase timeout it stops claiming and exits, leaving the queue for the next
  KEDA-started job.
- **The grace becomes five minutes** (design D3), matching what #144 asked for.
- **The notifier is bounded and serialised** (design D4): a terminal Run's cursor is evicted, and one
  Run's pushes cannot interleave.
- **A watcher joining mid-Run misses nothing** (design D5): subscribe first, then read, so the
  handshake window closes rather than being covered by a later poll.
- **DEC-050 and the docs say what the code does** (design D6): 500ms, and a crash story that names
  the sweeper.

## Impact

- Specs: `automation-configuration` (the timeout ceiling) and `run-orchestration` (the live window's
  guarantees).
- Docs: BR-005 bounded; DEC-054 recorded; DEC-050's flush figure corrected; ARCHITECTURE.md's crash
  story rewritten.
- Code: the Automation validator, `RunLogNotifier`, the dispatch worker's claim loop, one Terraform
  value and its comment.

## Out of scope

- Control-plane kill of a running KEDA job (absent by design in cancellation).
- The SignalR hub as the primary channel, and ingest auth — blocked on OPN-002.
- Managed identity for the KEDA scaler (upstream limitation).
- Any automatic retry. BR-004 is untouched: the sweeper closes state, it never re-runs.
- The transcript rendering of the live window — #130.
