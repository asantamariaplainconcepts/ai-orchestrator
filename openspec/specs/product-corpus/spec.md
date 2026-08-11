# product-corpus Specification

## Purpose
TBD - created by archiving change product-corpus-v1. Update Purpose after archive.
## Requirements
### Requirement: the living corpus is docs/product/v1

`docs/product/v1/` SHALL be the product's living corpus — the identity, glossary, capabilities,
business rules, journeys, roadmap and shaping rules every ceremony and live document reads. Its
`README.md` SHALL name the authority split: `openspec/specs/` is the authority on current
behaviour; the corpus is the authority on what the product is and must become; the decision log
remains append-only at `docs/product/mvp/10-locked-mvp-decisions.md`.

#### Scenario: a grill reads the living corpus

- **WHEN** `/aio:grill` grounds an idea in product context
- **THEN** the glossary, business rules and capabilities it reads resolve under
  `docs/product/v1/`, and the decisions it cites resolve in the decision log the v1 README
  names

#### Scenario: a corpus/spec disagreement

- **WHEN** `docs/product/v1/` and a spec in `openspec/specs/` disagree about current behaviour
- **THEN** the spec wins and the corpus carries the defect, per the authority split in
  `docs/product/v1/README.md`

### Requirement: the identity revision is a numbered decision

The identity change (open-source, dual-habitat, superseding DEC-001's "internal web app") SHALL
be recorded as a new `DEC-*` entry appended to `docs/product/mvp/10-locked-mvp-decisions.md`,
numbered against `origin/main` at implementation time, backed by an ADR in `docs/adr/`. The DEC
SHALL record, in one entry: the identity revision, the adoption of `docs/product/v1/` as living
corpus, stable-ID continuity (carrying #316's UC-024→UC-028 correction), and BR-014's habitat
sentence as a wording clarification sourced from DEC-065. DEC-001's own text SHALL NOT be
edited.

#### Scenario: the decision is discoverable from either corpus

- **WHEN** a reader follows DEC-001 in the decision log
- **THEN** the log's newest entries contain the revising DEC, and DEC-001's original text is
  intact above it

### Requirement: stable IDs survive the corpus version

Every `ACT-*`, `BC-*`, `UC-*`, `BR-*` and `RULE-*` id present in `docs/product/mvp/` SHALL
resolve to the same concept in `docs/product/v1/`, with exactly one named correction, carried
from #316: the file-changes review (numbered UC-024 until 2026-08-11) SHALL be UC-028 in both
corpora, the grill SHALL remain UC-024, and v1 SHALL name the correction where UC-028 is
defined.

#### Scenario: an old issue's trace still resolves

- **WHEN** an issue filed before v1 cites a UC/BR id
- **THEN** the id resolves to the same concept in `docs/product/v1/04-capabilities.md` /
  `05-business-rules.md`, and a pre-#316 citation of UC-024 is read against its day's corpus,
  as the note on UC-028 states

### Requirement: the superseded corpus is legible history

`docs/product/mvp/` SHALL remain in place, byte-identical except for two named writes: one
supersession note at the top of `00-product-brief.md` pointing at `docs/product/v1/`, and
appends to `10-locked-mvp-decisions.md` — the decision log remains the live, append-only DEC
record there. ADRs (existing entries), `BOOTSTRAP*`, `docs/process/retro-log.md` and
`openspec/changes/archive/` SHALL NOT be edited by the cutover.

#### Scenario: a reader lands in the old corpus

- **WHEN** someone opens `docs/product/mvp/00-product-brief.md`
- **THEN** the first thing they read says the living corpus is `docs/product/v1/`, and the rest
  of the document reads as it was

#### Scenario: history is untouched

- **WHEN** the cutover change is diffed
- **THEN** no existing file under `docs/adr/` is modified (a new ADR may be added), nothing
  under `openspec/changes/archive/` appears, and no `BOOTSTRAP*` file and not
  `docs/process/retro-log.md` appears in the diff; under `docs/product/mvp/` only
  `00-product-brief.md` (the note) and `10-locked-mvp-decisions.md` (the append) appear

### Requirement: live documents point at the living corpus

`README.md`, `AGENTS.md`, `ARCHITECTURE.md`, `ONBOARDING.md`, `CONTRIBUTING.md`,
`docs/process/*` and the project-context/rules text in `openspec/config.yaml` SHALL reference
the product corpus at `docs/product/v1/`, and the `openspec/config.yaml` context SHALL describe
the current substrate (per-Run sandboxes, Postgres-outbox dispatch, dual-habitat identity) —
not the retired queue/KEDA one.

#### Scenario: the cutover sweep is clean

- **WHEN** `grep -rn "product/mvp"` runs over the repository after the change, excluding
  `docs/product/mvp/` itself, `docs/adr/`, `BOOTSTRAP*`, `docs/process/retro-log.md` and
  `openspec/changes/archive/`
- **THEN** every remaining match is a deliberate historical or decision-log reference named in
  the change's design, and none is a live doc's product-truth pointer

#### Scenario: a future proposal reads a truthful context

- **WHEN** `openspec instructions` serves the project context to a proposal
- **THEN** that context names the sandbox substrate and the dual-habitat identity, and does not
  describe Agents as KEDA-scaled ACA Jobs or the product as internal-only
