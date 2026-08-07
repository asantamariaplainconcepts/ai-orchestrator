# ADR-0015: always-on output has one owner, and tests assert its shape

- **Status:** Accepted
- **Date:** 2026-08-07
- **Deciders:** the repository owner (solo path, DEC-016)
- **Tags:** backend, testing, process

## Context

Twice in two changes, adding a line to every Run's transcript broke exact-content assertions in
suites the author did not run.

`project-runtimes` (#244) added a header naming the credential's source; it broke two
`RunLog_Should_Constraint` assertions. Its retro called the ripple "foreseeable, since the header
is prepended to every Run's transcript, but not foreseen", and closed with a remedy: *grep the
test suites for exact-content assertions on that stream at plan time and list the repairs in
tasks.md*.

`split-run-pod-into-executor-and-sandbox` then added a second always-on line — where the
credential lives — and broke three such assertions. The remedy from the entry directly above was
not applied. It was correct advice, recorded in the right place, one change old, and it changed
nothing.

Two forces are visible behind the recurrence. First, **the transcript had no owner**: any code
on the executor's path could prepend to it, so "how many always-on lines are there" had no
answer short of reading the whole method — and the second line turned out to be redundant with
the first, which nobody noticed until the tests failed. Second, **the assertions were
exact-content**, so every addition to a shared stream is a breaking change to tests that were
not about headers at all.

Advice in a retro is not a mechanism. The graduation rule (decision-records spec) exists for
exactly this: a lesson that recurs becomes an ADR on its **second** occurrence.

## Decision

We will give each always-on output stream **one composition point**, and assert its **shape**
rather than its exact bytes.

Concretely:

- A Run's transcript preamble is built in **one place**. Code that wants to state a fact about
  the Run adds a clause to that sentence; it does not emit a second always-on line. Two headers
  that must be kept in sync are a defect, not a feature.
- Tests that care about the preamble assert **that it names a fact** (contains the runtime, names
  the credential source), not the full literal line. `ShouldBe` on a whole transcript is reserved
  for tests whose subject genuinely is the exact bytes — and where used, they are listed in the
  change's `tasks.md` as known ripples before implementation starts.
- A change that adds to any always-on stream states, in `tasks.md`, which assertions it expects
  to touch. Discovering them from a red CI run means the plan was incomplete.

## Consequences

- **Positive:** adding a fact to the preamble stops being a cross-suite breaking change; the
  redundancy that produced two credential sentences becomes visible at the one place they meet.
- **Negative:** shape assertions catch less than exact ones — a garbled preamble with the right
  substrings would pass. The exact-bytes tests that remain are the mitigation, and they are
  deliberately few.
- **Neutral:** the existing `RunLog_Should_Constraint` exact assertions stay for now; they are
  the ones whose subject *is* the transcript's exact content. This ADR governs what gets added
  next, and does not require a sweep.

## Alternatives considered

- **Repeat the retro advice more emphatically** — rejected: it is what already failed, and the
  failure mode of advice is silence, which is indistinguishable from compliance.
- **Forbid always-on preamble lines entirely** — rejected: the preamble is genuinely useful
  (BR-016's companion promise — a reader must not have to infer where a Run's authority came
  from), and forbidding it would push the same facts into less discoverable places.
- **Make every transcript test use `ShouldContain`** — rejected as a blanket rule: some tests
  exist precisely to pin exact output, and weakening all of them to fix a planning gap trades a
  real guarantee for convenience (ADR-0013: an assertion must be able to fail).

## References

- Related: ADR-0013 (an assertion must be able to fail)
- `docs/process/retro-log.md` — 2026-08-07 project-runtimes (#244), first occurrence;
  2026-08-07 split-run-pod-into-executor-and-sandbox, second occurrence
- Pull request #284 — where the second occurrence surfaced, after the PR was open
