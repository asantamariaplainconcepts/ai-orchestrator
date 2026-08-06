# ADR-0013: An assertion must be able to fail

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** repo owner (DEC-003)
- **Tags:** testing, quality

## Context

Two changes, two days apart, hit the same defect through different APIs.

**2026-08-05 (#231's shell chip).** A shell-level accessible name — `"This machine — owner · no
sign-in"` — broke four E2E tests. Playwright's `GetByLabel` matches on **substring** by default, so a
label containing "owner" collided with the connector form's `Owner` input on every screen. The retro
recorded it and prescribed a fix: *"grep the e2e suite for loose GetByLabel/GetByRole substrings its
name can newly match — the collision is deterministic, so it can be found before CI does."*

**2026-08-06 (#269, this change).** An E2E assertion existed to prove that two tiers' `implement.md`
prompts were distinguishable — the exact collision that change had discovered:

```csharp
text.ShouldContain("implement.md");
text.ShouldContain("aio-implement.md");
```

The first line cannot fail while the second passes. `"aio-implement.md"` **contains**
`"implement.md"`, so the assertion was satisfied by the very value it was written to be distinguished
from. It had never tested anything. It was noticed only because the surrounding test failed for an
unrelated reason — the tier heading it also waited on had been removed.

The two share a root cause that the earlier prescription did not cover, because that prescription was
scoped to Playwright **locators** and this was a Shouldly assertion on page text. The class is wider
than the instance: **a substring predicate whose needle is a substring of the value it must exclude
carries no information.** Both cases were deterministic and findable by reading.

The graduation rule in this repository's retro process is that the **second** occurrence of a pattern
becomes a rule. This is the second.

## Decision

**We will require that every assertion be able to fail, and we will treat substring matching against a
family of related names as a defect rather than a style preference.**

Concretely:

- An assertion that distinguishes two values SHALL NOT use a containment predicate when one value is a
  substring of the other. Assert the distinguishing fact instead — exact equality, a count, a
  collection comparison, or the absence of the shorter needle in a context where only the longer may
  appear.
- Where a test's purpose is that two similar names are *told apart*, the test SHALL fail if they are
  conflated. Writing both as `ShouldContain` guarantees the opposite.
- Playwright locators SHALL be exact-matched (`new() { Exact = true }`, or a role plus an exact name)
  wherever the accessible name shares a token with another element on the page.
- When a test is edited and its subject has changed, the assertion SHALL be re-derived from what is
  now being proven, not adapted by find-and-replace. A renamed needle in a substring assertion is how
  a passing test survives the deletion of the behaviour it covered.

The check is a question, asked while writing: **what edit to the production code would make this line
fail?** If the answer is "none", the line is decoration. This is the same discipline
[ADR-0004](0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) applies to proxy signals,
one level down: there it was asserting the wrong artifact; here it is asserting an artifact in a way
that cannot discriminate.

## Consequences

- **Positive:** a test that cannot fail is now a named defect with a name to call it by, so it can be
  raised in review instead of read past. Both known instances were visible on the page.
- **Positive:** cheap to apply. Exactness costs one option on a locator and one predicate choice on an
  assertion.
- **Negative:** exact matching is more brittle against benign copy changes — a reworded label breaks a
  test that a substring match would have absorbed. Accepted deliberately: a test that breaks when the
  copy changes is doing its job badly at worst, while one that never breaks is doing no job at all.
- **Negative:** this cannot be enforced mechanically. A lint rule could flag `ShouldContain` with a
  literal, but not whether the literal is a substring of something else that matters. It stays a review
  rule, which is weaker than a gate.
- **Neutral:** the existing suites are not swept as part of this decision. #269 fixed the instance it
  found; a deliberate audit of loose substring assertions across the E2E tier is worth its own change,
  and this ADR is what would justify it.

## Alternatives considered

- **Leave it in the retro as a "what didn't", ungraduated** — rejected because that is exactly what
  happened after the first occurrence. The 2026-08-05 prescription was correct and too narrow, and the
  same defect returned two days later through an API it did not mention. A third occurrence costs more
  than this file.
- **A lint rule banning `ShouldContain` with a string literal** — rejected as both too blunt and too
  weak: containment is often the right predicate (a report listing several paths), and the rule cannot
  see the substring relationship that makes a particular use wrong.
- **Require exact matching everywhere, without exception** — rejected because containment is genuinely
  correct when asserting that a large rendered text includes a value whose name collides with nothing.
  A rule that overreaches gets suppressed rather than followed.
- **Trust CI to catch it** — rejected on evidence. CI passed both times. An assertion that cannot fail
  is invisible to every gate by construction, which is what makes it worth a rule.

## References

- Second occurrence: [#269](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/269),
  `src/tests/AiOrchestrator.EndToEndTests/StarterPrompts_Should_Constraint.cs`
- First occurrence: the 2026-08-05 entry in [`docs/process/retro-log.md`](../process/retro-log.md)
  (#231's shell chip, four E2E tests, `GetByLabel` substring collision)
- Related: [ADR-0004](0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) — a verification
  asserts the observable artifact, not a proxy signal.
- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — verify claims by exercising them.
