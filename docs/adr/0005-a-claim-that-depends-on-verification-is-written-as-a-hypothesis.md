# ADR-0005: A claim that depends on verification is written as a hypothesis until verified

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** repo owner (solo path, DEC-016)
- **Tags:** process, documentation, planning

## Context

Twice now, a plausible claim was written into a durable artifact in the indicative mood — as a
settled fact — and was then disproved by the first thing that exercised it:

1. **atlas-shell-adoption.** The proposal stated "**no new token is expected** — the README
   records the palette as sufficient". Measurement disproved it twice in one change: `--ls-caps`
   was needed because no uppercase label had existed before the sidebar, and `--brand-text`
   because the dark `--brand` — tuned for white text *on* it — reaches only 3.55:1 *as* text on
   the dark soft fill. That retro recorded the shape as a first occurrence.
2. **azure-dev-infrastructure.** A Dockerfile carried `# The runtime image, not aspnet: this
   process serves nothing`. The reasoning was sound and the conclusion wrong: `BuildingBlocks`
   framework-references `Microsoft.AspNetCore.App` for its endpoint and ProblemDetails helpers,
   so the shared framework is required transitively however little of it the migrator uses. The
   deployed job failed at startup with "must install or update .NET".

The shared shape is not being wrong — being wrong is normal and cheap. It is **recording a
prediction in the grammar of an established fact**, in a place that outlives the moment. The next
reader cannot tell which sentences were verified and which were merely reasonable, so a wrong one
is inherited as knowledge instead of being re-checked. Both examples above were *good* reasoning;
neither had been run.

This is distinct from [ADR-0001](0001-verify-claims-by-exercising-them.md) (exercise the path
rather than reading its configuration) and [ADR-0004](0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md)
(assert the artifact, not a proxy). Those govern *how to verify*. This one governs *how to write
a claim you have not verified yet*.

Per the graduation rule — an ADR on the second occurrence, not the first — this is the second.

## Decision

We will write any claim that depends on future verification as an explicit hypothesis with the
check attached, and only restate it as fact once the check has run:

- **In proposals and designs:** "verify X and record what is missing", not "X needs nothing".
  A task that asserts its own conclusion cannot fail.
- **In code comments:** state what was observed, not what was reasoned. "aspnet, because
  BuildingBlocks framework-references Microsoft.AspNetCore.App — the runtime image fails at
  startup" is durable; "runtime image, this serves nothing" was a guess in a fact's clothing.
- **When a hypothesis is disproved**, the artifact is updated to say what actually happened,
  including that the original expectation was wrong. Both occurrences above are recorded that
  way in their tasks and design files.

Reasoning is still welcome — the rule is about mood, not about omitting analysis. "This should
only need the runtime image (verify: run it)" would have been fine.

## Consequences

- **Positive:** a reader can tell verified statements from predictions, and a disproved
  prediction leaves a trail instead of a confident falsehood. Task lists stop containing
  self-confirming items.
- **Negative:** slightly wordier proposals; some claims must be marked provisional and revisited,
  which is friction at exactly the moment one wants to move on.
- **Neutral:** existing documents are not swept for this; it applies to new writing and to any
  claim touched while working nearby.

## Alternatives considered

- **Leave it as a retro lesson.** Rejected: it recurred one change after being written down, and
  the second instance was in a code comment — a different artifact from the first, which is
  evidence the shape is general rather than a proposal-writing quirk.
- **Ban unverified claims outright.** Rejected as unworkable: planning *is* prediction. The
  problem is unmarked prediction, not prediction.

## References

- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md), [ADR-0004](0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md).
- Occurrence 1: retro entry *2026-07-26 — atlas-shell-adoption*; PR #33.
- Occurrence 2: retro entry *2026-07-26 — azure-dev-infrastructure*; PR #35.
