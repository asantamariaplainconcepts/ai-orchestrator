# ADR-0012: A seeded document is the project's own

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** repo owner (DEC-003)
- **Tags:** backend, product, docs

## Context

[DEC-048](../product/mvp/10-locked-mvp-decisions.md) brought the owner's grill into the product and
settled where its rubric comes from:

> the rubric is always the project's own document, read live, because a product-wide readiness bar
> would impose one team's standards on every repository it touches.

The same reasoning is restated one level up, in the adoption requirement of
[`automation-configuration`](../../openspec/specs/automation-configuration/spec.md): a repository that
already carries its own pipeline is wired rather than given a second copy, because *"a product-wide
version of a team's own document imposes one team's standards on every repository it touches, and the
copy is the weaker of the two."*

Both statements were written about a product that **read** documents and never wrote them. The starter
catalogue offered prompts to copy; nothing in the product wrote a process document anywhere.

[#269](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/269) changes that. The
spec-first workflow's prompts read a definition of ready, a retro log, and an OpenSpec layout. Before
this change an Admin could reach that workflow only by assembling it by hand, and the product's honest
best offer was a sentence on a card naming documents their repository did not have. The prompts
installed; the first Run failed on a missing rubric. A prerequisite that is printed and then abandoned
is the failure the tiering was introduced to prevent, moved one step later.

So the question is forced: may the product write a readiness rubric into a repository that has none?

## Decision

**We will let the product seed a document it does not already find, and we will treat what it writes
as the project's own from the moment it lands.**

DEC-048's clause is revised by [DEC-064](../product/mvp/10-locked-mvp-decisions.md) on one narrow
ground: **"the weaker of the two" presumes two.**

Where a team has its own readiness document, it still wins. That rule is untouched and is now asserted
by scenarios in [`workflow-prerequisites`](../../openspec/specs/workflow-prerequisites/spec.md): a
prerequisite whose path exists is not written, not modified, and absent from the pull request, decided
against the clone the branch is cut from. The comparison DEC-048 makes — product copy versus team's
own — only ever arises when both exist, and in that case the product still loses. Where a team has
none, there is no second term: the choice is a seed or nothing, and nothing is what makes the workflow
fail on first use.

DEC-048's **read-time** invariant survives intact. The `GrillToReady` action still reads the project's
document live, at a configured path, never a bundled copy. What changes is only whether the product may
put a first version of that document there — and once it has, the document is at the project's own
path, in the project's own repository, editable and deletable by the team, read live thereafter. The
distinction is between *installing* and *depending on*: a seeded file becomes theirs; a bundled rubric
read at Run time would stay ours.

Two properties make this safe enough to accept, and both are spec'd rather than left to care:

1. **An existing file always wins**, prerequisites included.
2. **Consent is explicit**, off by default, and states every path it will write before it is given.

The seed also refuses to overreach. It carries the rubric and the shaping rules it must cite — the
`definition-of-ready` spec forbids the rubric from restating them, so the two are one artifact — and it
ships `openspec/config.yaml` with its project-context section as an explicit `TODO`, because context is
the one part that cannot be inherited and a plausible-looking wrong context is worse than a blank one.
This repository's own product corpus (`ACT-*`, `UC-*`, `BR-*`, `DEC-*`) is **not** shipped: that is
AI Orchestrator's identity, not a template.

## Consequences

- **Positive:** a team can adopt the spec-first workflow in one press and have it actually run. The
  prompts and the documents they read arrive in one reviewable draft pull request, so the workflow
  cannot be merged half-installed.
- **Positive:** the prerequisite is no longer a warning an Admin has to act on themselves. The sentence
  that used to say *"you need these"* now says *"this writes these"*, which is a stronger and more
  useful claim.
- **Negative, and stated plainly:** on day one, every repository that presses the button holds the same
  readiness bar. That is exactly what DEC-048 objected to, and the objection is not dismissed — it is
  bounded. The mitigation is that the file is theirs from the moment it lands, and that no team with
  its own document ever receives ours.
- **Negative:** the product's blast radius grows. It writes outside the prompt directory for the first
  time — process documents, an `openspec/` layout. Bounded by the draft pull request (the default branch
  is never written), by the existing-file rule, and by the paths being catalogue content that is
  enumerable and tested.
- **Neutral:** every seeded document ships as a starter whose first line says it is now the team's to
  edit. If those files drift from this repository's own equivalents, that is acceptable — the
  manifest-enumeration test guarantees they load and have a body, not that they stay in step with ours.
- **Neutral:** a fork that disagrees edits the manifest. Prerequisites are catalogue content, so this
  decision is configuration rather than code.

## Alternatives considered

- **State the prerequisites and let the human accept them, writing nothing** — rejected because it is
  what the product already did, and it is what produces prompts whose first Run fails on a missing
  document. It respects DEC-048 by leaving the user with the problem.
- **Check the repository for the prerequisites and refuse consent until they exist** — rejected because
  it makes the product decide an Admin may not install prompts before writing the documents they were
  about to write next, and it costs a vendor read per path to enforce.
- **Ship the rubric but read it from the product at Run time** — rejected outright: this is precisely
  what DEC-048 forbids, and correctly. A rubric read from the product is one the team cannot change.
- **Ship this repository's full product corpus as the seed** — rejected because `docs/product/mvp/`
  describes AI Orchestrator. Pasting one product's actors, use cases and locked decisions into another
  team's repository is not a template, it is noise they must delete.
- **Two pull requests, one for prompts and one for documents** — rejected because a workflow merged
  half-way is the broken state the prerequisites exist to prevent. One press is one decision, and one
  decision should cost one review.

## References

- Issue: [#269](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/269)
- Revises: DEC-048 (via DEC-064) in [`10-locked-mvp-decisions.md`](../product/mvp/10-locked-mvp-decisions.md)
- Specs: [`workflow-prerequisites`](../../openspec/specs/workflow-prerequisites/spec.md),
  [`automation-configuration`](../../openspec/specs/automation-configuration/spec.md),
  [`default-automations`](../../openspec/specs/default-automations/spec.md)
- Related: [ADR-0009](0009-a-claim-about-existing-behaviour-cites-where-it-lives.md) — the discipline
  this ADR follows in quoting the clause it revises rather than paraphrasing it.
