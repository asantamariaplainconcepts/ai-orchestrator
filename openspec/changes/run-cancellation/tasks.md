# Tasks — run-cancellation

## 1. The slice

- [ ] 1.1 `POST .../runs/{runId}/cancel`: terminal Runs refused with their state (design D4);
      otherwise `Cancelled` with an end timestamp and no failure reason.

## 2. The worker cooperates

- [ ] 2.1 `RunExecutor` re-reads before invoking and before publishing (design D2), and its
      terminal writes are guarded so an outcome cannot overwrite a cancellation (D3).

## 3. Tests

- [ ] 3.1 Queued/AwaitingApproval cancelled → terminal, nothing enqueued, Story freed.
- [ ] 3.2 Cancelled before invocation → the runtime is never called.
- [ ] 3.3 Cancelled during invocation → nothing published and the Run stays `Cancelled` — the
      race the human must always win.
- [ ] 3.4 Terminal Runs refuse, naming the state.

## 4. Portal + close-out

- [ ] 4.1 Cancel on the Run detail page; refusals visible; catalog copy; lint + build.
- [ ] 4.2 ARCHITECTURE.md records the stated gap (an Agent mid-invocation is not killed);
      CI's own filtered command locally; CI green.
