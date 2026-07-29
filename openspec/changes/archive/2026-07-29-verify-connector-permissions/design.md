# Design: verify-connector-permissions

## D1 — Probe what the pipeline does, not what is cheapest to call

`Repository.Get` was chosen because it is one call that proves the coordinates resolve. It proves
nothing else, and metadata is the one permission a fine-grained token cannot be created without.

The probe therefore performs the reads the product actually performs: list the repository's Stories,
and read a document from the repository. Those are the two vendor reads every action depends on —
matching and the mirror need the first, every conversational action needs the second. A credential
that can do both can run the pipeline; one that cannot, cannot, and that is exactly the question
UC-004 asks.

It is not a guess about which permissions exist. It is the same calls, made early.

## D2 — A verdict per capability, because "no" is not an answer

A boolean cannot say which read failed, so the refusal cannot name what to fix — and naming what to
fix is the whole value, as #124's store refusal demonstrated within hours of shipping.

`VerifyAccess` therefore returns a result carrying, per capability, whether it succeeded and the
vendor's own reason when it did not. The caller gets a verdict rather than a test plan: it does not
orchestrate probes, it asks once. That also keeps each vendor's permission model inside its own
implementation, which matters because Azure DevOps' is different and the caller must not learn
either.

## D3 — Four failures, four fixes, four messages

`Translate` maps `NotFoundException` and `RateLimitExceededException` and sends everything else to
`VendorUnavailable("the API returned {status}")`. So a `403` reads as *"the vendor could not be
reached"* — false whenever the vendor answered, and it discards the reason the vendor gave. That one
sentence is what made today's failure take twenty minutes to diagnose.

A permission refusal, a rejected credential, an exhausted rate limit and an unreachable vendor have
four different fixes and become four different errors. The vendor's own text travels with the
permission one, because GitHub says *"Resource not accessible by personal access token"* and no
paraphrase of ours improves on it.

Fixing `Translate` is unavoidable here and it also improves Run-time messages. Those are not two
changes; they are one function with two callers.

## D4 — Testing on demand, because a credential rots without anything changing here

A permission granted in the morning can be revoked by lunchtime, and nothing in this product
changes when it happens. So the probe is reachable on demand, against the stored credential, with
no token re-entered — and it renders per capability, which is the shape D2 already produces.

It writes nothing. A failed test leaves the Connector exactly as it was: the Admin asked a question,
not for a change.

## D5 — One probe, two entry points

The save path and the test button call the same code. If they diverge, the button starts reassuring
people about a check that no longer matches the one gating saves — the failure mode that makes a
test worse than no test.

This is the discipline `RunCreator` and `HandOn` already apply: one path, two callers, so the two
cannot disagree.

## D6 — Absent is not forbidden

The natural implementation of the document probe — read the configured rubric path and see if it
works — conflates a missing file with a forbidden one, and would refuse every repository that has
not adopted this framework's document layout.

So the document capability succeeds when the vendor answers *anything other than* a permission
refusal. `404` is a pass: "this path is empty" and "you may not look" are different answers, and
only the second is a refusal. This is the distinction D3 makes available, and it is the reason D3
has to land first rather than alongside.
