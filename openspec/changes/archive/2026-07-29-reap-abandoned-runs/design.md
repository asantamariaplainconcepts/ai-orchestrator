# Design: reap-abandoned-runs

## D1 — The deadline is already written; this makes it true

BR-005 says each phase has a timeout. The product enforces it in exactly one place — around the
agent process — which means it enforces it only while that process exists. The rule was never
wrong; its enforcement was conditional on the thing most likely to fail.

So the sweep asserts the rule that is already there rather than inventing a new one: a Run in
`Planning` or `Executing` whose `StartedAt` plus its Automation's timeout plus a grace period is in
the past has, by the contract, already ended. All that remains is to write it down.

## D2 — Two timeouts, two reasons, because the next step differs

An agent that ran out of time and a worker that disappeared produce the same state and want
different responses. The first says the work was too big or the model too slow: raise the timeout,
or narrow the task. The second says the infrastructure dropped it: look at the container, and
re-trigger unchanged.

Collapsing them into one message would send the reader to tune a timeout that was never the
problem — the same failure `Translate` makes today when it reports a permission refusal as an
unreachable vendor.

## D3 — Deadline, not heartbeat

The tempting design is a `lastHeartbeatAt` the executor refreshes while it works, with the sweep
failing whatever is stale. It is more precise about liveness and it is the wrong trade.

A heartbeat introduces a second definition of "still running" alongside the timeout, a write
cadence to choose, and a new way to be wrong: a worker that is alive but briefly starved reports
late and gets killed for it. The deadline has none of those. A Run past its timeout is over
*whatever* the process is doing — if that process were alive it would have cancelled itself, so
exceeding the deadline is itself the evidence that it is not.

It also needs no schema: `Run.StartedAt` exists and `MarkExecuting` sets it.

## D4 — The watcher cannot be the thing being watched

The dispatch worker scales to zero, which is the whole point of KEDA and also the reason it cannot
host this. A sweep that only runs while workers run cannot notice that no worker is running.

The long-lived host carries it, beside the poller that already lives there for the same reason.

## D5 — Never overwrite an outcome

The one way this feature could be worse than the bug is a sweep that marks `Failed` a Run the
executor is at that moment marking `Succeeded`.

Two things prevent it. The grace period past the deadline means a worker still finishing is out of
scope by construction. And the update is conditional on the Run still being in the state the sweep
observed, so a Run that reached a terminal state between the read and the write is left alone
rather than reopened. The second matters more than the first: a grace period is a guess, and a
conditional write is a guarantee.

## D6 — Visible, because a silent recovery teaches nobody

A reaped Run is a failure like any other and belongs in the waiting inbox's failure lane (#94).
That is not decoration: this condition was invisible until somebody happened to open a Run page,
and a recovery that leaves no trace would make the next occurrence just as invisible. The point is
that the Story is freed *and* somebody can tell it happened.
