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
| [0009](0009-a-claim-about-existing-behaviour-cites-where-it-lives.md) | A claim about existing behaviour cites where that behaviour lives | Accepted |
| [0010](0010-a-habitat-contract-is-asked-never-inferred.md) | A habitat contract is asked, never inferred | Accepted |
| [0011](0011-a-worktree-session-carries-its-own-telemetry.md) | A worktree session carries its own telemetry, or the retro says which check failed | Accepted |
| [0012](0012-a-seeded-document-is-the-projects-own.md) | A seeded document is the project's own | Accepted |
| [0013](0013-an-assertion-must-be-able-to-fail.md) | An assertion must be able to fail | Accepted |
| [0022](0022-an-order-a-person-can-rearrange-is-stored.md) | An order a person can rearrange is stored, never derived | Accepted |
| [0023](0023-a-hand-rolled-spawn-inherits-nothing-unless-it-says-so.md) | A hand-rolled spawn inherits nothing unless it says so | Accepted |
| [0025](0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) | Human time is recorded by a person, never derived from telemetry | Accepted |
| [0027](0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md) | A change may reach `main` unreviewed, on one explicit invocation | Accepted |

<!-- The table stops at 0013 and resumes at 0022: rows for 0014-0021 were never added, although those
     files exist in this directory. #310 added its own row rather than silently backfilling eight
     entries it did not write, so the gap stays visible until somebody decides to close it. #323 did
     the same for 0025, and #343 for 0027 — the missing set is now 0014-0021, 0024 and 0026, so the
     gap is still growing. #343 did not close it either: eleven rows nobody reviewed do not belong in
     a change about the merge route. Per ADR-0026 this stops being a comment and becomes a tracked
     issue at that change's sync — which is the last time it is allowed to be a note. -->

