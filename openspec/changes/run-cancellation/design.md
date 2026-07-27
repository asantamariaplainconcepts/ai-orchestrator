# Design — run-cancellation

## D1 — Cancel writes the terminal state immediately, and that is the honest part

The Run is `Cancelled` the moment the human asks, not when some worker acknowledges. The
alternative — a "Cancelling" state pending confirmation — would leave BR-001 holding the Story
on a promise, and would need a timeout policy for workers that never answer. Immediate
termination means the Story frees at once and the UI never implies work that has been called off.

## D2 — The worker cooperates at boundaries it already has

`RunExecutor` re-reads the Run before invoking the runtime and again before publishing. Those
are the two moments where stopping is both possible and meaningful: before, nothing has been
spent; after, the spend is sunk but the *consequence* — a branch and a pull request — is not.
No new machinery, no polling loop, no cancellation token threaded through a vendor CLI.

## D3 — A cancelled Run's outcome must not overwrite the cancellation

The executor's terminal-state writes are guarded: if the Run is already `Cancelled`, the
success or failure it computed is discarded. Without that, a Run cancelled mid-invocation would
flip back to `Succeeded` when the agent returned, and the human's decision would silently lose
a race it should always win.

## D4 — Refusals name the state

Cancelling a `Succeeded`, `Failed` or already-`Cancelled` Run is a question with no answer;
saying which state it is in is the difference between a five-second and a five-minute
diagnosis. Same shape as the approval gate's refusal (#22).
