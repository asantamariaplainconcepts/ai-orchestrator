# Proposal: starter-prompts

## Why

[#190](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/190). After
[#162](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/162) a new project has one
action and no prompts, so its first Run fails on a missing file. #150's refusal names the resolved
path, which is the right message — but the right message about an empty repository is still an empty
repository.

A starter set closes it: prompts an Admin can take, whose names and locations already match what
#150 resolves, so taking one and creating an Automation is two steps and no translation.

## What changes

- **A versioned starter catalogue in this repository**, as real markdown files with frontmatter —
  the kind #150 already strips — so a file taken from here behaves identically whether this product
  runs it or a local agent CLI does.
- **Two tiers, labelled by what they require** (design **D2**):
  - **Portable** — five prompts that assume a cloned repository and a Story, and nothing else. This
    tier is what answers the issue's value statement.
  - **Workflow** — the spec-first workflow this product was built with, offered as a bundle that
    states its prerequisites out loud: OpenSpec, and the documents the prompts read.
- **The portal offers them; the product writes nothing** (design **D1**). Each entry shows its
  purpose in one sentence, its prerequisites, its filename, and its content to copy. The Admin puts
  the file in their repository.
- **Collisions are reported by reading, not assumed by not writing** (design **D6**). Where the
  project has a Connector, the portal reads each starter's target path and marks the ones that
  already exist. "Never overwritten" is trivially true when nobody writes; "the collision is
  reported" is a promise worth actually keeping.
- **A test per file**: it loads, and it has a body once frontmatter is stripped, using the product's
  own `StripFrontmatter`. A starter prompt that fails to load is worse than none.

## The decision the issue asked for, and its argument

**The portal offers; an agent does not write.** Three checkable facts, all pointing the same way:

1. **The content is deterministic and the product already holds it.** An agent pass is a
   nondeterministic process; using one to emit bytes this repository versions and tests can only
   introduce error, and it charges for the privilege.
2. **The no-overwrite criterion is enforceable in one shape and merely requested in the other.** With
   the portal offering, nothing writes — the guarantee is structural. With an agent writing, it is a
   sentence in a prompt, enforced by nothing, testable only as "the instruction is present".
3. **Agent-writes does not remove the human step; it substitutes one.** A pull request somebody must
   review and merge is not less work than committing files — it is the same work with a spend, a
   write-scoped credential, and a failure path in front of it.

## What this revises

Nothing. It is the first change since #162 that *could* have reintroduced an orchestrator repository
write, and it declines to — for the same reason #162 removed the others.

## What does not change

- How a prompt is resolved or read (#150, #162). The catalogue produces files; the Run path reads
  the repository exactly as before.
- Where a prompt lives. The repository, still (#189 settled that the product is not a second home).
- BR-008: nothing here reaches the vendor at all, in either direction beyond the existing read.
- BR-010: no credential is involved beyond the Connector's existing named secret, used for the
  collision read.

## Out of scope

- Keeping a project's copies in step with later changes to either tier. A copied file is the
  project's; a sync mechanism is its own decision, and one that would need an answer to "what if
  they edited it".
- Writing, committing or opening a pull request for the Admin — that is the shape this change
  rejects, with the argument above.
- Shipping the workflow tier's *supporting documents* (a definition of ready, a retro log, an
  OpenSpec bootstrap). The bundle states that it needs them; producing them is a larger item and
  naming it as a prerequisite is the honest half that fits here.

## Impact

- **Modules:** Projects — one query slice, one embedded catalogue, one frontend panel. No schema, no
  migration, no new permission (`project.automations.read`, which both bundles hold).
- **Reads at the vendor:** one document read per starter when a project has a Connector, to report
  collisions. Bounded by the catalogue's size, not by the repository's.
- **A maintenance cost, stated:** the workflow tier is a *copy* of this repository's own
  `.claude/commands/aio/` prompts rather than a reference to them (design **D4**), so editing a
  command here does not silently change what every project is offered. The cost is that the two can
  drift, and a test asserts the copies load rather than that they match.
