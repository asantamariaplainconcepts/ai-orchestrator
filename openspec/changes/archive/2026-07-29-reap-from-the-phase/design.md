# Design: reap-from-the-phase

## D1 — A phase's clock starts when the phase does

BR-005 gives each phase a timeout. `Run.StartedAt` is set by whichever phase ran first, so for a
two-phase Run it is the plan's start and it stays there — across `AwaitingApproval`, which BR-006
says may last indefinitely.

So the deadline is computed from the start of the phase the Run is actually in. `ApprovedAt` already
records the moment the wait ended, which is the executing phase's start for every gated Run; an
ungated Run has no wait and `StartedAt` is still right for it.

No new column, no heartbeat, and no change to what the sweep does once a Run really is overdue.

## D2 — The requirement's wording is part of the defect

The spec said the deadline is "its start, plus its Automation's timeout, plus a grace period". That
sentence is readable, plausible, and wrong: `StartedAt` does not mean "the start of what is currently
running", and an implementation that followed the words faithfully produced a Run killed for the time
a human took to approve it.

So the wording is corrected in the same change, not left for a reader to reconcile with the code. A
requirement that can be implemented correctly and still be wrong is a requirement that will be
implemented wrongly again.

The scenario that makes it visible — an approved Run surviving a long wait — is the one that was
missing. Its absence is why #140's suite was green while the behaviour was broken.
