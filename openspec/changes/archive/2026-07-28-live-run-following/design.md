# Design: live-run-following

## D1 — Postgres chunks as the record, a poll as the window

Four transports were mapped on the issue (#96). Chosen: the durable store IS the stream —
appended chunks in the Runs schema, followed by a 3-second poll. What decided it:

- **BR-014 for free.** Every line committed is a line that survives a crash; there is no
  window/record split to reconcile, and a partial log up to a crash is just the rows so far.
- **Self-host alignment (DEC-049).** Blob stores need a blob endpoint, roles and Terraform in
  every habitat; SignalR needs an ingest auth story while OPN-002 is open. Postgres is already
  everywhere the system runs.
- **The lag budget is honest.** Flush ≤2s plus poll 3s gives ≤5s observed lag, which is the
  difference between a spinner and a window — sub-second is not what the trust problem needs.

The SignalR hub (pod as outbound client, per-run groups) stays the recorded upgrade path: it
layers on top of this exact writer without schema changes, purely as latency.

## D2 — The writer owns a channel; the process thread never touches a DbContext

`OutputDataReceived` fires on a worker thread. Lines go into a bounded channel; one task drains
it in batches (50 lines or 2 seconds) into its own scope. The executor completes the channel
after the runtime returns, flushing the tail. Backpressure drops nothing: the channel is
bounded but the producer awaits.

## D3 — The read reports "done" so the client knows when to stop

The log endpoint returns the content and whether the Run is terminal. The page polls while it
is not. No push, no negotiation — the client stops itself, and a finished Run read later is the
same endpoint with `complete: true` on the first response.
