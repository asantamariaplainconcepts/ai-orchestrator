# Product corpus — v1

The living definition of AI Orchestrator: what is built, in product terms, and what we intend
to build next. Rewritten from zero on 2026-08-11 because the product outgrew its MVP framing —
it began as an internal tool dispatching "AI pods" and is now an open-source, self-hostable
orchestrator running Agents in per-Run microVM sandboxes across two habitats — and the old
corpus was amended patch by patch until its identity contradicted its decisions
([DEC-001](../mvp/10-locked-mvp-decisions.md) vs [DEC-049](../mvp/10-locked-mvp-decisions.md)).

## What lives where

| Document | Holds |
|---|---|
| [00-product-brief.md](00-product-brief.md) | What the product is, its habitats, goals, personas, anti-references |
| [01-actors-and-responsibilities.md](01-actors-and-responsibilities.md) | ACT-001..004 |
| [02-domain-glossary.md](02-domain-glossary.md) | The ubiquitous language, including the terms the old glossary never learned (Habitat, Sandbox, Conversation, Inbox) |
| [03-bounded-contexts.md](03-bounded-contexts.md) | BC-001..005 |
| [04-capabilities.md](04-capabilities.md) | Every UC — the loop as built, and the intended capabilities (UC-030+) awaiting their grill |
| [05-business-rules.md](05-business-rules.md) | BR-001..016 |
| [06-user-journeys.md](06-user-journeys.md) | J1..J5, including the self-host developer the old journeys never met |
| [07-roadmap.md](07-roadmap.md) | What we want to do, reconciled with every open issue |
| [08-backlog-shaping-rules.md](08-backlog-shaping-rules.md) | RULE-001..007 — the grill's rubric |

## Stable IDs

`ACT / BC / UC / BR / RULE` IDs are carried over unchanged from the old corpus — issues and
specs cite them, so renumbering is BREAKING. One correction is made here and named where it
happens: the old corpus assigned **UC-024 twice**; the grill capability becomes **UC-028**
(which was never used) and the file-changes review keeps UC-024. New intended capabilities
start at UC-030.

## What stays in `../mvp/`

The historical record, untouched: the decision log
([10-locked-mvp-decisions.md](../mvp/10-locked-mvp-decisions.md), append-only — new DECs keep
landing there), open decisions ([07-open-decisions.md](../mvp/07-open-decisions.md), OPN-006
remains open in #223), and the bootstrap-era foundation split
([09-foundation-vs-product-split.md](../mvp/09-foundation-vs-product-split.md)). Studies live
beside both in [../studies/](../studies/).

## Authority

Current *behaviour* is specified in `openspec/specs/` — where this corpus and a spec disagree
about what the code does, the spec wins and this corpus has a bug. What the product *must
become* is decided here and in the decision log. Product authority: the repo owner, solo
([DEC-003](../mvp/10-locked-mvp-decisions.md)).
