# Proposal: product-corpus-v1

Issue: [#318](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/318) · Foundation · ACT-001

## Why

The product corpus's identity contradicts its own decisions: DEC-001 still says "internal web
app" while DEC-049 made self-hostability a product goal ("anyone can run this"), and everything
built since — habitats (DEC-052, DEC-065), the self-host persona (BR-016, UC-029), conversations
(DEC-055, DEC-061) — exists in decisions the identity documents never absorbed. The old corpus
also assigns UC-024 twice, omits load-bearing terms (Habitat, Sandbox, Conversation, Inbox), and
frames shipped capabilities as "post-MVP". Every grill traces against this corpus (RULE-003), so
shaping new work on it propagates the incoherence into every future issue — including the three
intended capabilities (UC-030..032) waiting behind this change.

## What Changes

- **Adopt `docs/product/v1/` as the living corpus** (drafted 2026-08-11, in this change):
  README naming the authority split (`openspec/specs/` = behaviour; the decision log stays
  append-only in `../mvp/`), brief (open-source, dual-habitat identity, anti-references),
  actors, glossary (+ Habitat, Sandbox, Code source, Execution locus, Conversation, Pass,
  Inbox, Transcript), bounded contexts, capabilities (UC-001..029 carried; UC-030..032 added as
  intended, ungrilled), business rules (BR-001..016 unchanged in force; BR-014 gains the
  habitat sentence from DEC-065, named as wording clarification), journeys (+ J5 self-host),
  roadmap, shaping rules.
- **BREAKING (product identity): a new DEC (next free number against origin/main) revises
  DEC-001** — open-source, dual-habitat identity; v1 adopted as living corpus; the
  UC-024→UC-028 correction — backed by an ADR per the decision-records spec. DEC-001's own
  text is not edited in place. No integration contract (Aspire, host csproj, outbox message
  schema, CI) is affected — this change touches documentation and two doc-facing specs only.
- **Repoint live docs to v1**: `README.md`, `AGENTS.md`, `ARCHITECTURE.md`, `ONBOARDING.md`,
  `CONTRIBUTING.md`, `docs/process/*`, and the project-context/rules blocks in
  `openspec/config.yaml` (which still describe "internal web application… KEDA-scaled ACA
  Jobs" — text retired by DEC-013's supersession). ADRs, `BOOTSTRAP*`, the retro log and
  archived changes stay byte-identical.
- **`docs/product/mvp/` becomes the explicit historical record**: unchanged except one
  supersession note atop `00-product-brief.md`. The `run-orchestration` spec's historical
  reference to `mvp/05-business-rules.md` stays valid and untouched.
- Ships the **Orca study** (`docs/product/studies/2026-08-11-orca.md`) that sources UC-030..032.

## Capabilities

### New Capabilities

- `product-corpus`: the corpus itself as a specified capability — where the living corpus
  lives, the authority split it declares, stable-ID continuity across corpus versions (one
  named correction: UC-028), and the supersession contract with the historical `mvp/` record.

### Modified Capabilities

- `definition-of-ready`: the DoR SHALL cite the shaping rules at their v1 path
  (`docs/product/v1/08-backlog-shaping-rules.md`) instead of the mvp path.
- `agent-instructions`: canonical-docs linking requirement adds `docs/product/v1/` as the
  product-truth target (mvp/ remains a valid historical link, no longer the default).

### Unchanged on purpose

- `decision-records`: the new DEC + ADR **follow** the existing spec (numbers allocated
  against origin/main, ADR names its evidence); no requirement changes.

## Impact

- Documentation only: `docs/product/v1/` (new), one-line note in `docs/product/mvp/00`,
  live-doc links, `openspec/config.yaml` context text, one new DEC entry + one ADR.
- Specs: one new (`product-corpus`), two deltas (`definition-of-ready`, `agent-instructions`).
- No code, no APIs, no dependencies, no infra. CI is affected only insofar as markdown link
  checks (if any) must pass on the repointed docs.
- Out of scope (from #318): grilling/filing UC-030/031/032; moving the decision log; editing
  ADRs, bootstrap records or archived changes; renaming or deleting `docs/product/mvp/`.
