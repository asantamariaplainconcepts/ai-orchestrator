# Proposal: reap-abandoned-runs

## Why

Issue #140. A Run whose executor process disappears stays in `Executing` for ever, and under
BR-001 its Story can then never start another Run. Measured on dev rather than inferred: Run
`019fac98-0924-7b58-8f81-3b0c57bbb40c` was dispatched at `06:37:53Z` with the default 30-minute
timeout and, read back at `07:18:34Z`, was still `Executing` after **40.7 minutes** with no
failure reason and no output.

BR-005's timeout could not have fired. It is `CancellationTokenSource.CancelAfter` inside
`HeadlessProcess`, so it lives in the process that vanished. The executor's `catch` has the right
instinct and says so — *"an eternal Executing would hold the Story hostage"* — but a `catch` only
runs while the process is alive to run it, and a recycled container, an out-of-memory kill or an
ACA job eviction raises no exception anywhere.

Nothing else watches. Outside the executor, the only code reading `Executing` is BR-002's
concurrency count, so an abandoned Run also consumes a slot of the project's cap permanently.

One gap therefore breaks three rules: BR-001 blocks the Story, BR-002 loses a slot, and BR-005's
promise that every phase ends is false whenever the process does not survive to keep it.

## What changes

- **A sweep ends Runs past their deadline** (design D1): a Run in `Planning` or `Executing` whose
  `StartedAt` plus its Automation's timeout plus a grace period is in the past becomes `Failed`.
- **Its reason names the cause** (design D2), distinguishable from a timeout the executor enforced
  itself — an agent that was too slow and a worker that vanished have different next steps.
- **The deadline is the evidence, not a heartbeat** (design D3): no new liveness concept, no write
  cadence to tune, nothing to be late.
- **It runs in the long-lived host** (design D4), because the process that must notice an absence
  cannot be the one that scales to zero.
- **A living Run is never touched** (design D5): grace period plus a state-conditional update, so
  a sweep can never overwrite an outcome the executor is in the middle of writing.

## Impact

- Specs: `run-orchestration` — one ADDED requirement (every Run reaches a terminal state, even
  when its worker does not report).
- Code: one hosted service in the Runs module, one query over overdue Runs, one failure reason.
  The Automation's timeout is already readable through the Contracts surface the executor uses.
- No schema change: `Run.StartedAt` already exists and is set by `MarkExecuting`.

## Out of scope

- **`Queued` Runs that were never dispatched.** A different cause — the message never arrived
  rather than the worker died — belonging with the dispatch substrate, and with a different
  deadline to reason about.
- Retrying, resuming, or reconstructing what the lost worker was doing. Reaping ends a Run; BR-004
  still says humans re-trigger.
- Diagnosing *why* a worker vanished. That is the platform's to answer; this guarantees only that
  the Run ends.
- Changing the default timeout.
