# Proposal: live-run-following

## Why

Issue #96 (UC-027). A Run is an opaque box until it ends: DEC-031 chose fetched-logs-only for
the MVP, before the conversational actions made watching part of the contract and before
minutes-long implement Runs whose only feedback is a spinner. Watching an agent work is the
strongest trust surface an agent product has. **This revises DEC-031**, recorded as DEC-050 at
sync.

## What Changes

- **Runtimes report output as it happens**: `AgentInstruction` gains an optional per-line
  callback; the process wrappers already read stdout line-by-line and now forward each line.
- **The record is Postgres** (design D1): a chunk table in the Runs schema, appended in batches
  by a writer the executor owns. Zero new infrastructure — it works identically under
  `aspire run`, the self-host compose (#99) and ACA, because the worker has the database (#90).
- **The window is a short poll**: `GET .../runs/{id}/log` returns the log so far plus whether
  the Run is done; the Run page polls every 3 seconds while it executes. Stated lag target:
  **≤5 seconds** (flush ≤2s + poll 3s). The SignalR hub remains the recorded latency upgrade
  when someone needs sub-second — the issue carries that design ready to lift.

## Impact

- Affected specs: `agent-execution` (output is observable while it happens).
- Touched: BuildingBlocks (one optional field), both runtime wrappers, Runs module (table,
  migration, writer, read slice), Run page, mock, tests. DEC-050 at sync; UC-027 into the corpus.
- Out of scope: SignalR transport (recorded upgrade); worker/poller logs; retention.
