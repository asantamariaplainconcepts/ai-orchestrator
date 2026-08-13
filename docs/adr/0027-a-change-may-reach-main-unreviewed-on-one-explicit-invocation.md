# ADR-0027: A change may reach `main` unreviewed, on one explicit invocation

- **Status:** Accepted
- **Date:** 2026-08-12
- **Deciders:** repository maintainer (solo, DEC-003)
- **Tags:** process, workflow, risk

## Context

This repository's loop puts a person in front of every change three times: the grill, the spec review
after `/aio:propose`, and the code review after `/aio:implement`. The two reviews are marked by the
**hold** — a reserved label that every mutating command refuses while it is on, and that
[`workflow-commands`](../../openspec/specs/workflow-commands/spec.md) forbids any command from
removing (*the hold is a refusal, and no command ever clears it*).

What the record says about those three sittings:

- **The human's total recorded time per change is 15–30 minutes.** The five most recent entries in
  `docs/process/retro-log.md` report human ~0.25h (#323), ~0.25h (#331), ~0.3h (#332), ~0.5h (#335),
  ~0.5h (#340) — against agent time of hours on the same changes. The cost of the two later gates is
  therefore not hours of reading; it is three interruptions, each of which has to reach a person who
  is elsewhere.
- **A review stage has already failed to catch what a gate caught.** #323's retro records that AC 13
  required a label the change itself forbade a command from creating: *"The bootstrap ordering was
  invisible at spec review: the proposal named provisioning as a task, and nobody noticed that a
  later step of the same run would need it."* Implementation stopped mid-flight. The human gate read
  the spec and missed an ordering constraint that only running the thing surfaced.
- **And a review stage would have been the only thing that could catch one specific defect.** The
  same retro records `/aio:sync` refusing on a red rollup while asserting the failing test was
  *"deterministic, not flaky"* on three consecutive failures — a claim that was false within the
  hour. A machine gate produced the refusal; nothing but a reader would have challenged the sentence
  attached to it.

The product this repository builds intends to offer an unattended tier. Running it here first is how
that tier earns the right to be offered — and the honest version of the question is not "is review
valuable" (it demonstrably is, unevenly) but "may the owner spend it once, at grill, on work whose
shape they already accepted".

## Decision

We will allow a change to reach `main` with no human having read its spec or its diff, when a person
explicitly asks for that in a single named act: `/aio:ship <issue>`.

The invocation is the authorisation. It replaces DEC-016's in-session go-ahead, which an unattended
run has nobody to ask, and it is the *only* thing that authorises the merge — so choosing this route
is always deliberate and always attributable.

Three properties make the route additive rather than a hole in the existing one:

1. **No hold is created, so none is cleared.** `/aio:ship` runs `/aio:propose`, `/aio:implement` and
   `/aio:sync` in *unattended mode*, whose whole content is: advance the status without applying the
   hold, and treat the invocation as sync's recorded go-ahead. The hold exists to pause *between*
   commands; an unattended run has no between. The invariant that nothing removes a hold is preserved
   **literally**, not by exception — which is what keeps a hold worth trusting on the reviewed path.
2. **Every refusal becomes a halt that applies the hold.** A halt is permitted to write the label; it
   comments the reason and leaves the status alone. The halted issue is then indistinguishable from
   any issue waiting on a person, and the ordinary command for its label resumes it.
3. **The staged path is unchanged and remains the default.** A contributor who never types
   `/aio:ship` observes the loop exactly as before.

The route deliberately declines the guardrails that were considered and would have softened it: no
lane label, no eligibility restriction by item kind, no cost or size ceiling, no batching, no
auto-retry, and no change to the WIP cap or the overlap check.

## Consequences

- **Positive:** the owner spends their attention once, at grill, where the record shows it is most
  productive — #323's grill produced the corrected RULE-004 reasoning, while its spec review missed
  the ordering defect.
- **Positive:** the loop gains the unattended tier the product intends to ship, exercised on the
  repository that builds it before it is offered to anyone else.
- **Positive:** the hold's meaning gets *sharper*, not vaguer. It was ambiguous between "a person
  must look" and "the next stage is gated"; after this it means only the first, and the `status:*`
  label carries the second alone.
- **Negative:** a defective change can reach `main` with nobody having read it. The determinism
  claim in #323's refusal is the concrete shape of this risk: an agent writing a confident, false
  sentence that a reader would have challenged. On this route that sentence merges.
- **Negative:** "a question the issue and its spec do not answer" is a judgement, not a gate. A run
  that under-detects its own ambiguity ships a guess, and no check catches that class.
- **Negative:** unattended mode is conditional behaviour in three command files. Each clause is
  additive and adjacent to the rule it modifies, but the conditional is real and a future edit to any
  of the three must consider both modes.
- **Neutral — the checks that carry this decision** (per *an ADR names its evidence and its check*):
  - **CI green stays a hard gate**, unchanged in content and ordering, and is the only automated
    reviewer between a generated diff and `main`. It gets no new exceptions on this route.
  - **`rg -n 'remove-label' .claude`** must show no command or skill removing the hold — the
    mechanical form of property 1, and a check that fails loudly if a future edit reaches for it.
  - **The PR body and the retro entry must both state that no human read the spec or the diff, and
    name `/aio:ship`** — which makes unreviewed changes *countable*. Without that marker, any future
    claim about this route's safety would be unmeasurable (ADR-0018 — a measurement licenses only
    what it measured), and the log could not distinguish the two populations it now must compare.
  - **The retro entry marks its reflections unconfirmed**, since nobody confirmed them.
- **Neutral:** this change itself lands through the **staged** path. A human reads this ADR before
  the route it authorises exists.
- **Neutral:** to revisit, compare the retro entries marked unattended against the reviewed ones. If
  unattended changes produce more post-merge `/aio:refine` findings or more reverts, that is the
  evidence to narrow the route — by eligibility, by a size ceiling, or by restoring a gate.

## Alternatives considered

- **Chain the three commands as they stand and clear the hold between them** — rejected because it
  requires the one thing the loop forbids. A hold that an automation can undo returns the reviewer to
  choosing among labels, which is the exact stall #323 removed.
- **A durable `lane:autonomous` label on the issue** — rejected at grill. It would survive a dead
  session and show up in `/aio:status`, but it puts a standing permission on an issue where a
  per-invocation decision belongs.
- **Stop before the merge** (propose → implement → retro → archive → CI green, then hold for one
  review) — rejected at grill. It is the conservative version of this ADR and remains the obvious
  fallback if the evidence turns.
- **`/aio:ship` orchestrates the skills directly** instead of invoking the three commands — rejected
  because it would restate sync's gate ordering in a second file, giving that ordering two owners
  (ADR-0003). #202 is what a drifted ordering costs: five consecutive merges landed a failing deploy.
- **Extract a shared `close-out-and-merge` skill** — rejected because a skill that gates CI, writes a
  retro, archives, lints, merges and watches a deploy has six responsibilities, against
  [`skill-catalog`](../../openspec/specs/skill-catalog/spec.md)'s *one responsibility per skill*.
- **A `--no-hold` flag on each staged command** — rejected because it invites a human to skip a
  review stage without deciding to. Unattended mode is reachable only through `/aio:ship`.
- **Restrict the route to `lane:spec-less` items** — rejected at grill: the owner's approval at grill
  is the gate, and an item's kind is a poor proxy for whether that approval was informed.

## References

- Issue [#343](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/343); change
  `openspec/changes/ship-a-change-unattended/`
- Evidence: `docs/process/retro-log.md` — `aio-commands-honour-the-hold` (#323) for both the missed
  ordering constraint and the false determinism claim; the human-time lines of #323, #331, #332,
  #335, #340
- Related: [ADR-0003](0003-a-derived-artifact-has-exactly-one-owner.md) — why reuse is by invocation
  rather than restatement
- Related: [ADR-0018](0018-a-measurement-licenses-only-what-it-measured.md) — why the unattended
  marker is part of the decision, not decoration
- Related: [ADR-0021](0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md) — the
  precedent for permitting in one habitat what is refused in another
- Decisions: DEC-003 (product authority, solo), DEC-016 (solo review path), DEC-017 (WIP limit),
  DEC-025 (spec-less lane), DEC-068 (this route)
- Commands: `.claude/commands/aio/ship.md`, `propose.md`, `implement.md`, `sync.md`
