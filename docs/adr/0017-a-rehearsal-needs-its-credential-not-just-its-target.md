# ADR-0017: a rehearsal needs its credential, not just its target

- **Status:** Accepted
- **Date:** 2026-08-08
- **Deciders:** the repository owner (solo path, DEC-016)
- **Tags:** process, verification

## Context

[ADR-0014](0014-a-run-needs-a-rehearsal-target.md) was written because two consecutive changes had
ended with the same end-to-end verification unexercised: there was nothing safe to point a Run at.
It made naming a **rehearsal target** a precondition of the plan, and a target was duly created —
`asantamariaplainconcepts/ai-orchestrator-rehearsal` exists and nobody minds it being written to.

`sandbox-carries-the-owners-session` (#288) then became the **third** consecutive change to reach
its proof step and stop. Not for ADR-0014's reason: the target existed, the dev loop was running in
sandbox mode with carriage on, and the sandbox host already held the `github` credential the agent
would publish with. What was missing was the **Connector's own token**. The server reads Stories
and clones with a GitHub PAT stored under a name derived from the project and the vendor, so a
fresh project has no secret to name, and the only way to create one is to paste a token into the
Connector form. That is an action an agent may not take, and no amount of planning inside the
change makes it takeable.

So ADR-0014 removed the obstacle it could see and left the one behind it. The pattern is the same
one it was written against — the verification is downgraded at proof time, when the cost is a
decision nobody can make, rather than at planning time, when the cost is a line in `tasks.md`.

The general shape: **a rehearsal is a target plus everything needed to reach it.** A repository
nobody minds is only the half of the precondition that happens to be a noun.

## Decision

A change whose acceptance depends on a Run executing end to end SHALL name, before implementation
begins, both:

1. its **rehearsal target** — the repository the Run may write to (ADR-0014, unchanged); and
2. the **credentials that rehearsal consumes**, each with a statement of whether it already exists
   and resolves.

Where a required credential does not exist, obtaining it is the change's first task. Where it can
only be created by a human — anything that must be pasted, authorised in a browser, or approved in
a vendor's console — the change SHALL say so explicitly, so the human step is scheduled while
somebody is at the keyboard rather than discovered by an agent that cannot perform it.

A change reaching its proof step without them records the verification as **not verified**, cites
this ADR, and names which of the two was missing.

## Consequences

- **Positive:** the remaining half of ADR-0014's obstacle is removed, and the specific failure mode
  that produced three consecutive unverified Runs — a precondition nobody enumerated — becomes a
  planning artifact instead of a proof-time surprise.
- **Positive:** it makes agent-executable and human-only work visible before implementation, which
  is useful well beyond rehearsals.
- **Negative:** more to write at planning time, some of it for changes that would have been fine.
- **Neutral:** #288 keeps its honest bound; this ADR does not retroactively verify it. Its AC7
  remains open, needing a PAT on a Connector pointed at the rehearsal repository.

## Alternatives considered

- **Amend ADR-0014 in place** — rejected: the ADRs are a record of what was decided and when, and
  the fact that the first attempt addressed only the visible half is itself the useful information.
- **Pre-provision a standing rehearsal credential for every deployment** — rejected for now: it
  means a long-lived token existing for no other reason, which is a worse default than a human
  pasting one when a change needs it. Worth revisiting if the human step becomes frequent.
- **Accept that end-to-end Runs are simply not agent-verifiable and drop the criterion** — rejected:
  the criterion is the one that catches integration faults, and three changes' worth of substrate
  now rests on it.
