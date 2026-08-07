# ADR-0014: a Run needs a rehearsal target before its proof depends on one

- **Status:** Accepted
- **Date:** 2026-08-07
- **Deciders:** the repository owner (solo path, DEC-016)
- **Tags:** process, verification, backend

## Context

Two consecutive changes have ended with the same verification unexercised, for the same reason.

The sbx spike (archived 2026-08-07) recorded: "the owner declined the real Run (it posts to
their GitHub repository)." Its successor, `split-run-pod-into-executor-and-sandbox`, reached
the identical wall five hours later — the dev loop was running in sandbox mode, the substrate
was ready, and the Run was still not dispatched, because the only configured project targets
`asantamariaplainconcepts/ai-orchestrator` with four live backlog stories, and DEC-062 has the
agent publish its own work. Launching one would push a branch and open a pull request on a real
repository.

The structural fact behind both: **this product has no target a Run can be rehearsed against.**
Every path to "just try it" runs through somebody's real backlog. The seeded demo project
carries no Connector at all, so it cannot run anything; the only project that can run is the
real one. The obstacle is not caution — it is that nothing safe exists to point at.

Both times the response was to downgrade the verification rather than remove the obstacle. Each
change shipped with its end-to-end proof marked unverified and honestly bounded, which is
correct as far as it goes (ADR-0005) — but a bound that recurs is a gap, not a caveat. ADR-0001
exists because claims must be exercised; a claim that *cannot* be exercised by construction
defeats it quietly, and the defeat is invisible because each individual change looks rigorous.

## Decision

We will treat "a real Run can be launched against something disposable" as a **precondition of
the plan**, not a hope at proof time.

Concretely: a change whose acceptance depends on a Run executing end to end SHALL name its
rehearsal target in `tasks.md` before implementation begins — a project whose Connector points
at a repository nobody minds being written to. Creating or pointing that target is a task in the
change, ordered before the task that needs it. Where no such target exists yet, creating one is
the change's first task.

A change that reaches its proof step without a rehearsal target SHALL record the verification as
**not verified** and cite this ADR, so the recurrence is counted rather than re-discovered.

## Consequences

- **Positive:** the end-to-end claim becomes exercisable, so the substrate work of the last two
  changes stops accumulating unverified. The cost lands at planning time, when it is a line in
  `tasks.md`, instead of at proof time, when it is a decision nobody can make safely.
- **Negative:** a throwaway repository is one more thing to exist and stay configured, and a
  rehearsal against an empty repository proves less than one against a real backlog — it
  exercises the pipeline, not the prompts' judgement.
- **Neutral:** the two changes already shipped with this gap keep their honest bounds; this ADR
  does not retroactively verify them. The read-only ad-hoc-prompt Run (#275) remains the safe
  way to close them against the real repository when someone can supervise it.

## Alternatives considered

- **Keep declining and keep bounding honestly** — rejected: it is what happened twice, and the
  bound is now load-bearing for a whole substrate rather than for one detail.
- **Make the agent's publishing opt-in per Run** — rejected here: it changes DEC-062, a locked
  product decision, to solve a testing problem; the grants model is where that belongs.
- **Rely on the read-only ad-hoc prompt alone** — rejected as the general answer: it still acts
  on a real repository (a clone with a real credential) and still needs a human watching, so it
  does not remove the obstacle, only shrinks it.

## References

- Related: ADR-0001 (verify claims by exercising them), ADR-0005 (a claim that depends on
  verification is written as a hypothesis)
- `openspec/changes/archive/2026-08-07-spike-sbx-sandbox/findings.md` — first occurrence
- `openspec/changes/.../split-run-pod-into-executor-and-sandbox/evidence.md` — second occurrence
- DEC-062 (the agent publishes its own work), #275 (a Run on an open change with an ad-hoc prompt)
