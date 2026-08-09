# ADR-0020: A launcher is unverified until it has met its real CLI

- **Status:** Accepted
- **Date:** 2026-08-09
- **Deciders:** repository owner
- **Tags:** testing, infra, agents

## Context

This product drives external CLIs across a process boundary — `sbx`, `aca`, the agent runtimes
themselves. Each is tested against a **stand-in script**: a shell script at the configured command
path that records what it was asked and answers what the test expects. That idiom is good and it
stays. It makes a process contract exercisable with no daemon, no cloud account and no cost, and
it lets "the launcher did not disable auto-suspend" be an assertion rather than an assumption.

It has one failure mode, and this programme has now paid for it twice.

**The stand-in is written from the same beliefs as the code it stands in for.** Where those
beliefs are wrong, the fixture encodes the error and the test turns green on it. Nothing in the
suite can detect this, because the fixture *is* the suite's model of the outside world.

The evidence, both from launcher work:

- `spike-sbx-sandbox` (2026-08-07): `sbx cp` preserves the **host's** uid and mode, so a 0600
  credential owned by uid 501 landed inside the sandbox unreadable. The CLI reported "0
  credentials" for a file demonstrably present. No fixture had any reason to model file ownership.
- `runs-execute-in-azure-sandboxes` (2026-08-09): the first contact between the shipped
  `AcaAgentProcessHost` and the real `aca` CLI produced **seven defects in one session**, every
  one of them invisible to a fully green suite:
  1. `fs cp` takes no `--id`; the remote side is `<sandbox-id>:<path>`.
  2. **No verb copies a directory tree at all**, which invalidated the workspace design and forced
     tar → copy → untar.
  3. The poll loop dropped the last lines of every Run.
  4. The egress decision log is JSON; the reader filtered lines for a word the real output never
     contains, so a Run that reached a blocked host reported nothing.
  5. `sandbox create` was never passed `--credential`, so no agent could ever have authenticated.
  6. `--disk` names only *public* disks; a deployment's own image needs `--disk-id`, which the
     host never sent — leaving this product's other runtime with nowhere to run.
  7. `sandbox delete` prompts, and a piped invocation answers *Aborted*. (This one the product had
     right, by luck rather than by test — the stand-in never prompts.)

Two of those rewrote a design decision. None of them was a coding slip; all of them were beliefs.

## Decision

**We will treat a launcher as unverified until a gated test has driven it against the real CLI,
and we will write that test before trusting the stand-in — not after.**

Concretely, for every `IAgentProcessHost` implementation and any comparable process-boundary
driver:

1. A `Real<Thing>_Should_Constraint` exists beside the stand-in suite, gated on an environment
   variable so CI can never run it by accident and a human can run it on purpose. The precedent
   is `RealSbxSandbox_Should_Constraint`; `RealAcaSandbox_Should_Constraint` follows it.
2. It runs **before** the change is called done, and its observations are recorded verbatim in the
   change's `evidence.md` — including what did not work (ADR-0001).
3. **When the real CLI contradicts the stand-in, the stand-in is corrected to the real answer,
   kept verbatim.** A fixture that invents its subject's answers can only confirm the invention
   (ADR-0016); this is that rule applied to a boundary rather than to a server.
4. Where the real exercise is impossible — no account, no credential, no hardware — the leg is
   recorded as a **hypothesis** (ADR-0005), loudly, in the design and in the habitat's own
   documentation. It is never left implied by a green suite.

## Consequences

- **Positive:** the class of defect that only appears on contact is found while the change is
  still being made, when correcting a design costs a rewrite rather than a rollback. Two of this
  change's seven would have shipped as data loss (dropped output) and one as a silent security
  hole (denials never reported).
- **Negative:** a real account, real credentials and real money. The exercise here cost five
  short-lived microVMs and about ten AI credits — trivial — but it needed a subscription with
  write authority, a token only a human can mint, and roughly an hour.
- **Negative:** the gated test cannot run in CI, so it protects against nothing on the next
  change unless someone runs it. It is a ritual, and rituals decay.
- **Neutral:** every measurement carries its date, its CLI version and its region, because a
  preview surface moves (ADR-0018). `evidence.md` states them at the head.

## Alternatives considered

- **Trust the stand-in and fix on first deployment** — rejected: two of these defects are only
  visible under load a deployment reaches immediately, and one of them (the unreported denials)
  fails silently in exactly the direction that matters.
- **Mock at the SDK level instead of the CLI** — rejected, and for the same reason: a mock derived
  from our reading of the API has the same blind spot as a script derived from our reading of the
  CLI. The .NET SDK for this platform did not exist at the time either.
- **Run the real exercise in CI** — rejected: it needs credentials CI must not hold and creates
  billable cloud resources on every pull request. Gated-and-manual is the honest trade.
- **Write only the real test and skip the stand-in** — rejected: the stand-in is what makes the
  contract exercisable at all on a laptop with no account, and it catches regressions the real
  test is too slow and too expensive to catch.

## References

- `openspec/changes/runs-execute-in-azure-sandboxes/evidence.md` — the seven defects, verbatim
- `openspec/changes/archive/2026-08-07-spike-sbx-sandbox/findings.md` — first occurrence
- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md),
  [ADR-0005](0005-a-claim-that-depends-on-verification-is-written-as-a-hypothesis.md),
  [ADR-0016](0016-a-fixture-derives-what-the-server-derives.md),
  [ADR-0018](0018-a-measurement-licenses-only-what-it-measured.md)
