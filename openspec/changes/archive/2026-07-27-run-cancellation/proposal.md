# Proposal: run-cancellation

## Why

Issue #23 (UC-014). `Cancelled` has existed since #22 with only plan-rejection able to reach it;
a wrong or runaway Run cannot be stopped. BR-012 wants `Queued`/`AwaitingApproval` discarded and
`Planning`/`Executing` terminated — and the second half is where the honesty is.

## What Changes

- **`POST /api/projects/{projectId}/runs/{runId}/cancel`** — ends the Run `Cancelled`
  immediately. Terminal at once, so BR-001 frees the Story and the UI stops implying work.
- **Cooperative cancellation in the worker**: the executor checks the Run's state at its own
  boundaries — before invoking the runtime, and after it returns but before publishing — so a
  cancelled Run **publishes nothing**: no branch, no pull request, no overwritten outcome.
- **Terminal Runs refuse**, naming their state; a deliberate cancellation records no invented
  failure reason.
- **Portal**: Cancel on the Run detail page while the Run is cancellable.

## Impact

- Affected specs: `run-orchestration`, `agent-execution` (the executor's cancellation checks).
- Touched: Runs module (one slice + executor boundaries), frontend, tests, ARCHITECTURE.md.
- Out of scope: a control-plane kill of the container; bulk cancellation; re-running a
  cancelled Run (*Run now*, #21).

## The gap this leaves, stated

An Agent already mid-invocation is **not** killed. It finishes (bounded by BR-005's timeout) and
its work is discarded at the publish boundary. Killing the container needs management-plane
credentials in the portal's identity and an Azure-only path with no local equivalent — the exact
shape #50 argued against, and a second way for a Run to end that the worker would not know
about. It becomes its own issue if a wasted invocation ever costs more than the machinery.
