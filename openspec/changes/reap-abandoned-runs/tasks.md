# Tasks — reap-abandoned-runs

- [ ] 1.1 A hosted service in the Runs module, composed in the long-lived host and not in the
      dispatch worker (design D4), sweeping on a configurable interval that is not hardcoded.
- [ ] 2.1 The overdue query: `Planning`/`Executing` Runs whose `StartedAt` plus the Automation's
      timeout plus a grace period has passed (design D1). No new column — `StartedAt` exists.
- [ ] 3.1 The write is conditional on the observed state (design D5), so a Run that finished in
      between keeps its outcome. A grace period is a guess; this is the guarantee.
- [ ] 4.1 A failure reason that names a worker that never reported, distinct from the executor's
      own timeout message (design D2).
- [ ] 5.1 Tests: an overdue Run is failed and its Story freed; a Run inside its deadline is
      untouched; a Run that reaches a terminal state between read and write keeps its outcome;
      nothing is re-dispatched; a Run overdue across a sweeper restart is still ended; the reaped
      Run appears in the inbox's failure lane.
- [ ] 6.1 CI green; evidence on #140, including the concurrency slot released.
