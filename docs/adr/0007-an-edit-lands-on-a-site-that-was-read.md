# ADR-0007: An edit lands on a site that was read, never on a pattern

- **Status:** Accepted
- **Date:** 2026-07-29
- **Deciders:** repository owner; agent working the change
- **Tags:** workflow, testing, agent-behaviour

## Context

Two failures in consecutive changes share one cause: an edit was expressed as a pattern and
applied wherever the pattern matched, instead of being applied to a site somebody had read.

In `automation-output-label`, the payload field `readyLabel` was renamed to `outputLabel`.
The rename was driven by searching for the C# identifier, which the compiler would have caught
anyway. The same string also appears as a JSON literal inside test request bodies, where nothing
type-checks it. Those literals were missed, the field silently stopped being sent, the test fell
back to the default value and still passed — a green suite asserting the old behaviour.

In `sync-action`, adding a seventh entry to the seeded Automation catalogue broke four suites
that assert the catalogue's size. Repairing them, `Skipped.Count.ShouldBe(4)` was replaced with
`5` across a file. One of those matches belonged to
`AProjectSeededBeforeTheSetGrew_Should_ReceiveOnlyTheAdditions`, a test that pre-seeds exactly
four Automations and whose `4` had nothing to do with the catalogue's size. The suite went from
one honest failure to a different, more confusing one.

Both edits were correct as descriptions of intent and wrong as instructions. Neither would have
happened if the rule had been "open each match and decide", because in both cases a human reading
the match would have seen immediately that it did not belong.

The compiler is not a backstop here. The first case involved a string literal; the second, a bare
integer. Both type-check perfectly while meaning something else.

## Decision

We will apply edits site by site, having read each site, and we will not use a
replace-everywhere operation on a bare literal.

Concretely:

- A rename or a value change is applied by opening each match and confirming that this occurrence
  means what the change intends. A match that is the same characters for an unrelated reason is
  left alone, deliberately and visibly.
- Searching for the identifier is not searching for the concept. When a name also travels as a
  string — a JSON payload field, a route, a label, a config key, a test fixture — the search
  covers the string form too, because that form has no compiler behind it.
- A numeric literal in a test is never replaced globally. Numbers in tests are coincidences far
  more often than they are the same fact; the anchor is the test's name, not its digits.

## Consequences

- **Positive:** the class of failure where a suite stays green while asserting the old behaviour
  is closed at its source. Editing gets slower in proportion to the number of matches, which is
  the correct price — the matches are exactly where the judgement is needed.
- **Negative:** large mechanical renames cost more attention than a single command. When a rename
  genuinely spans dozens of identical sites, this rule taxes the honest case to prevent the
  dishonest one.
- **Neutral:** this constrains how a change is made, not what it contains, so no compiler or CI
  stage can enforce it directly. Two checks stand in for one:
  - **A test that configures a non-default value asserts an outcome the default could not
    produce.** This is the gate that would have caught the `readyLabel` case on its own: the test
    set the field and then expected the value the default already supplies, so dropping the field
    changed nothing observable. Written the other way, the silent fallback is a failure.
  - **The retro log is the tripwire.** A third occurrence means this ADR did not work as a working
    rule and something mechanical has to replace it.

## Alternatives considered

- **Rely on the compiler and the test suite** — rejected because both observed failures were
  invisible to both. A string literal and an integer are the two shapes that type-check while
  meaning something else, and they are precisely the shapes that broke.
- **Ban the replace-everywhere operation outright** — rejected as unenforceable and too broad.
  There are legitimate uses (a unique, distinctive token) and no mechanism to police the
  difference; a rule that cannot be followed literally gets ignored entirely.
- **Require a diff review after every bulk edit** — rejected because it is what already happens
  and it is what failed. Both defects were visible in the diff and were read past; the fix has to
  be before the edit, not after it.

## References

- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — the same instinct one layer up:
  a claim is checked against the artifact, not against the description of the artifact.
- Retro: `docs/process/retro-log.md`, entries for `automation-output-label` and `sync-action`.
