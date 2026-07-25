# ADR-0003: A derived artifact has exactly one owner

- **Status:** Accepted
- **Date:** 2026-07-25
- **Deciders:** repo owner (solo path, DEC-016)
- **Tags:** tooling, ci, architecture

## Context

Twice, an artifact that is produced by one mechanism was also claimed by another, and the two
disagreed:

1. **`wwwroot` — git and the build.** The frontend build writes the SPA into the host's `wwwroot`.
   It was committed to git before an ignore rule existed, so git tracked a directory the build
   rewrites on every run. The symptom was a permanently dirty working tree and a build output in
   review diffs; untracking it was the fix, and the ignore rule had to be re-added after a
   `git reset --hard` silently dropped it.
2. **`tokens.ts` — the generator and Prettier.** The design-token generator emits the runtime
   adapter; Prettier's frontend glob also matched it. Whichever ran last "won", and the other
   immediately reported a violation: run Prettier and the drift stage failed, regenerate and
   `format:check` failed. Two formatters on one file is not a race to be tuned — it is a
   guaranteed, permanent disagreement.

A third instance sat inside the same change and is worth recording because it shows the failure
does not need two *tools*: Prettier reformatted the **canonical** CSS by wrapping long font-stack
declarations across lines, and the generator's line-based parser silently dropped them. Prettier
legitimately owns formatting there — the parser was simply not robust to its owner's output.

The general shape: when two mechanisms can write the same artifact, or one mechanism's output is
another's input without an agreed contract, the result is drift that presents as a flapping
check rather than as an obvious bug.

## Decision

We will give every derived artifact **exactly one owner**, and make that ownership explicit:

- A generated file is owned by its generator. Every other tool that could touch it — formatters,
  linters, git — is configured to leave it alone: `.prettierignore`, `.gitignore`, or an explicit
  scope exclusion.
- A generated file declares its owner in its own header, naming the regeneration command.
- Where one tool's output is another's input, the consumer is written to tolerate anything the
  owner may legitimately produce, rather than assuming a shape.

## Consequences

- **Positive:** the drift and format gates can both be trusted, because they can no longer
  contradict each other. A failing gate now means a real problem rather than a turf war.
- **Negative:** exclusions must be maintained. A new generated artifact needs its ignore entry
  added at the same time, or the collision reappears — and the symptom will again look like a
  flapping check rather than a missing config line.
- **Neutral — the checks that would have caught each:**
  - `wwwroot` → the ignore rule is now in `.gitignore`; `git status` on a clean tree is the check
  - `tokens.ts` → `.prettierignore`; the drift stage and `format:check` now agree on every run
  - the parser → it splits on declarations rather than lines, so any legal formatting parses; the
    drift stage is what surfaced the breakage and would surface a regression
- Reviewers should ask of any new generated file: *who owns this, and is everything else told to
  leave it alone?*

## Alternatives considered

- **Make the generator emit Prettier-formatted output** — rejected: it couples the generator to a
  formatter's version and configuration, and the coupling breaks silently on upgrade. The
  generator's output does not need to be pretty; it needs to be stable.
- **Run the formatter, then regenerate, in a fixed order** — rejected: it encodes the conflict
  into the build order instead of removing it, and the order is invisible to anyone running one
  tool by hand.
- **Commit generated files and stop generating in CI** — rejected: it trades a detectable drift
  failure for an undetectable stale artifact.

## References

- Retro entries: `docs/process/retro-log.md` — 2026-07-25 `project-scaffolding` (post-merge
  finding) and 2026-07-25 `design-system`
- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — the parser breakage was also an
  assumption never exercised
