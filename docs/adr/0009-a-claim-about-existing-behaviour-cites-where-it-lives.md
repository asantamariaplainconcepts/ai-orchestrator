# ADR-0009: A claim about existing behaviour cites where that behaviour lives

- **Status:** Accepted
- **Date:** 2026-07-29
- **Deciders:** repository owner; agent working #147
- **Tags:** workflow, testing, agent-behaviour

## Context

In one working session, seven claims about how existing code or an external vendor behaves were
written down without being checked, and every one was wrong. They cost a cycle each and they were all
in files that could have been read in seconds.

- `TreatWarningsAsErrors` was asserted absent from the e2e lane. It is in
  `src/Directory.Build.props` and applies to every project, so the two lanes could never have
  disagreed. An acceptance criterion was written to fix a problem that did not exist.
- `tCount` was assumed to interpolate a `{count}` placeholder. It prefixes the number itself, so the
  keys rendered as "2 {count} steps" — and it type-checked perfectly.
- "The client already handles a redelivered push by sequence" was written into a spec delta. The
  handler concatenated unconditionally, so the design it justified would have traded a missing line
  for duplicated text.
- The inbox's field was called `Reason`. It is `WaitingFor`, so the test read a null.
- `TriggerOverlaps` was assumed to produce `400`. It is `Error.Conflict`, so `409`.
- The enable route was assumed to be `/enabled` with a body. The routes are `/enable` and `/disable`.
- A "full test sweep" was a hand-written list of test projects. It missed
  `AiOrchestrator.Modules.Projects.UnitTests`, and CI found the failure the sweep had reported green.

The repository had already made the same mistake about an external system. A unit test asserted that
GitHub label names are case-sensitive, on the stated grounds that "folding case here would invent a
rule the vendor does not have". Three API calls disprove it — `/labels/bug`, `/labels/BUG` and
`/labels/Bug` all return `bug` — so the comment invented the *absence* of a rule the vendor has, and
matching silently never fired for differently-cased triggers as a result.

The pattern is one shape: a statement about behaviour that already exists, believed rather than
checked. It is most confident in code the writer edited earlier the same day, which is exactly when
memory is stalest.

## Decision

We will not write a claim about existing behaviour — in this codebase or in a vendor's — without
citing where that behaviour lives, and we will read the citation before writing the claim.

Concretely:

- A design, spec sentence, acceptance criterion or comment that depends on how something already
  behaves names the file and the symbol, or the API call that demonstrates it. "The client dedupes by
  sequence" is not admissible; "the handler at `useRuns.ts:151` concatenates unconditionally" is.
- A claim about an external system is settled by exercising it, not by reasoning about it. Three `gh
  api` calls settled the label question that a comment had asserted for months.
- "I know what that returns" is the signal to open the file, not permission to skip it. Especially
  for a file edited earlier in the same session.
- A sweep over a set — test projects, screens, call sites — is enumerated by a command, never by a
  remembered list. `find src/tests -name "*.csproj"` cannot forget one.

## Consequences

- **Positive:** the failure mode this closes is the expensive kind, because a wrong claim about
  existing behaviour type-checks, reads plausibly, and survives review. Two of the seven produced
  green suites over broken behaviour, which is worse than a red one.
- **Negative:** every claim costs a lookup, and most lookups confirm what the writer thought. The
  price is paid on the many to catch the few — the same trade ADR-0007 accepted for edits.
- **Neutral:** no automated check enforces this. What it names instead is a habit with a visible
  artefact: the citation is in the text, so a reader can check whether the writer did. A claim with no
  citation is the thing to question.

## Alternatives considered

- **Rely on tests to catch it.** Rejected because they did not, twice: `tCount`'s wrong placeholder
  type-checked and rendered wrongly in a browser, and the "sequence" claim would have shipped
  duplicated text. A test written from the same wrong belief asserts the wrong thing.
- **Require a citation only in specs, not in comments or tests.** Rejected because the GitHub label
  claim lived in a test comment and misled for months, and the `Reason` field claim lived in a test.
  The venue does not change the cost.
- **A stricter rule: never state existing behaviour, always link.** Rejected as unusable prose. The
  claim is often the clearest way to say a thing; what it needs is a checked citation, not deletion.

## References

- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — the same instinct for claims about
  *infrastructure*; this extends it to claims about code and vendors.
- Related: [ADR-0007](0007-an-edit-lands-on-a-site-that-was-read.md) — its sibling: an edit lands on
  a site that was read. This is about the sentence, that one is about the change.
- Retro log entries for `parallel-ci-lanes`, `catalogue-and-workflow`, `run-execution-resilience`,
  `actionable-failure-inbox` and `unique-automation-triggers`, 2026-07-29.
- DEC-056 and #147, where the vendor claim was finally exercised.
