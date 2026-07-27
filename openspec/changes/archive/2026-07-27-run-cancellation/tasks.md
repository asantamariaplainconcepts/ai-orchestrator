# Tasks — run-cancellation

## 1. The slice

- [x] 1.1 `POST .../runs/{runId}/cancel`: terminal Runs refused with their state (design D4);
      otherwise `Cancelled` with an end timestamp and no failure reason.

## 2. The worker cooperates

- [x] 2.1 `RunExecutor` re-reads before invoking and before publishing (design D2), and its
      terminal writes are guarded so an outcome cannot overwrite a cancellation (D3).

## 3. Tests

- [x] 3.1 Queued/AwaitingApproval cancelled → terminal, nothing enqueued, Story freed.
- [x] 3.2 Cancelled before invocation → the runtime is never called.
- [x] 3.3 Cancelled during invocation → nothing published and the Run stays `Cancelled` — the
      race the human must always win. **Caught a placement bug:** the first cut checked *after*
      `Invoke` returned, but publishing happens inside it, so the pull request already existed.
      The check now sits immediately before `Publish`, which is what design D2 actually said.
- [x] 3.4 Terminal Runs refuse, naming the state.

## 4. Portal + close-out

- [x] 4.1 Cancel on the Run detail page; refusals visible; catalog copy; lint + build.
- [x] 4.2 ARCHITECTURE.md records the stated gap (an Agent mid-invocation is not killed);
      CI's own filtered command locally; CI green.
