# Architecture Decision Records

One file per decision: `NNNN-<slug>.md`, from [`template.md`](template.md).

**Immutable once accepted.** An accepted ADR is never edited to change its decision — write a new
one that supersedes it, and mark the old one `Superseded by ADR-NNNN`. The old text stays exactly
as written; the road not taken is the point of keeping these.

**Numbers are allocated against `origin/main`**, not against your branch, and re-verified at sync.
Two changes in flight would otherwise claim the same number — which happened twice in the project
this framework came from.

**Graduation is at the second occurrence.** A one-off lesson stays in
[`../process/retro-log.md`](../process/retro-log.md). A lesson that recurs, or that changes how
the workflow behaves, becomes an ADR *in the change that noticed the recurrence* — not later. The
rule is second, not third, because "we'll write it next time" is how patterns recur ten times.

**An ADR earns its place by citing evidence and naming a check.** State the specific incidents
that produced it, and in Consequences name the gate, test, or check that would have caught them.
A decision that names no check stays advice; the loop exists to turn advice into gates.

## Index

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-verify-claims-by-exercising-them.md) | Verify claims by exercising them, never by reading configuration | Accepted |
| [0002](0002-test-tiers-must-not-provision-their-own-preconditions.md) | A test tier must not hide a precondition the application lacks | Accepted |
| [0003](0003-a-derived-artifact-has-exactly-one-owner.md) | A derived artifact has exactly one owner | Accepted |
| [0004](0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) | A verification asserts the observable artifact, not a proxy signal | Accepted |
| [0005](0005-a-claim-that-depends-on-verification-is-written-as-a-hypothesis.md) | A claim that depends on verification is written as a hypothesis until verified | Accepted |
| [0006](0006-a-capability-is-not-added-until-a-user-can-reach-it.md) | A capability is not added until a user can reach it | Accepted |
| [0007](0007-an-edit-lands-on-a-site-that-was-read.md) | An edit lands on a site that was read, never on a pattern | Accepted |
| [0008](0008-a-live-conversation-costs-a-pass-per-message.md) | A live conversation costs a pass per message, because an untimed wait cannot idle | Accepted |
