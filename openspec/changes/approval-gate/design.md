# Design — approval-gate

## D1 — Resuming is a stamp, not a fifth state

`AwaitingApproval` already exists in the enum and in BR-001's active list. Approval sets
`ApprovedAt`, puts the Run back to `Queued` and re-enqueues; the worker routes on what the Run
already records — an approval-gated Run with no `ApprovedAt` gets phase 1, everything else gets
phase 2. A new "ApprovedQueued" state would duplicate `Queued`'s meaning and force every
existing filter, index and cap query to learn about it.

## D2 — The approved Plan is an input, or approval is theatre

Phase 2's instruction embeds the Plan the human approved. The alternative — re-deriving intent
from the Story alone — means the human blessed a document the Agent never sees again, and the
implementation can diverge from the thing that was approved without anything noticing.

## D3 — Two invocations, two timeouts (BR-005/BR-006)

The Automation's timeout bounds *each runtime invocation*, never their sum and never the human's
thinking time. That falls out of the phases being separate jobs, but it is asserted rather than
assumed: treating the phases as one long Run is the natural bug here, and BR-006 says the wait
is untimed.

## D4 — The cap counts work, not waiting

BR-002 counts `Planning`/`Executing`. `AwaitingApproval` is deliberately absent: a Run parked on
a human holds a Story (BR-001) but not a concurrency slot, or one slow reviewer would throttle
the whole project. Already true in the query; now it has a test that says why.

## D5 — Reject is terminal and frees the Story

`Cancelled` joins `Succeeded`/`Failed` outside BR-001's index filter. A rejected Run is done —
BR-004 leaves re-triggering to a human, which *Run now* (#21) already does.

## D6 — The Plan is untrusted text

It is model output rendered in a browser, so it goes through the same sanitiser as a Story's
description and its documents (#37/#38). Three renderers of untrusted markdown, one pipeline.
