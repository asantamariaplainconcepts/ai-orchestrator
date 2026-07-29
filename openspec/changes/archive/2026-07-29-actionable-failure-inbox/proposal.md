# Proposal: actionable-failure-inbox

## Why

Issue #145 (ACT-002; UC-026, UC-012, UC-021). The inbox exists to feed the human bottleneck, and it
asks for a decision it does not let anyone take.

Observed on dev: a Failed Run sits under "Decide about a failure" for hours. Its row links to the Run
page, where every control is hidden — Cancel is refused on a terminal Run, Approve and Reject need
`AwaitingApproval` — and *Run now* lives only in the project's backlog rows. So the real path is
inbox → Run → back out to the project → find the Story → pick the Automation → *Run now*. On a phone
that is a dead end.

And the only decision UC-026 can express is "a newer Run exists". The other legitimate decision —
*this failure needs no re-run* — has no expression at all, so a failure on a Story nobody intends to
re-run waits in "Waiting on you" forever. The one on dev is blocked by OPN-002 and will never be
re-run, and the product has no way for a person to say so.

## What changes

- **Run again, from where the failure is** (design D1): one control on a Failed Run's page that
  creates a Run through the existing Run-now path with the same Automation. No new dispatch, no new
  rule — BR-001, BR-002 and the approval gate apply exactly as they do for *Run now*.
- **Dismiss, because "no re-run" is a decision** (design D2): an acknowledgement stored on the Run.
  The entry leaves the inbox and the pulse's failure count; the Run stays `Failed`, terminal, and
  nothing re-runs.
- **The dismissal is auditable** (design D3): who is out of scope until OPN-002, but *when* is
  recorded and shown, because BR-014 makes a Run's history readable.
- **The inbox still acts on nothing** (design D4): its rows link to the Run page where both controls
  live. UC-026's v1 shape stands.

## Impact

- Specs: `run-orchestration` — one MODIFIED requirement (the inbox's failure lane gains the second
  decision) and one ADDED (re-running from a failure).
- Code: `Run.DismissedAt` and one migration; a dismiss slice; the inbox and pulse queries excluding
  dismissed failures; two controls on the Run page.
- No new endpoint for re-running: the Run-now path already takes a Story and an Automation, and
  reusing it is what makes BR-013 apply for free.

## The decision this revises

#94's design D2 says a failure's departure from the inbox is **derived by query, never stored**,
because BR-013's two re-trigger paths would each forget to update a flag. That reasoning is correct
and survives: the *newer-Run* condition stays derived.

The dismissal is a different kind of fact. It is a human decision that no query can derive — nothing
in the data distinguishes "nobody has decided yet" from "somebody decided not to re-run". So it is
stored, and the change records that this is an addition to D2 rather than a contradiction of it.

## Out of scope

- Actions in the inbox row itself; v1's shape stands.
- Choosing a *different* Automation, which stays with the backlog's Run-now control.
- Automatic clearing or expiry of failures. A decision is human, always.
- Who may dismiss — permissions land with OPN-002.
