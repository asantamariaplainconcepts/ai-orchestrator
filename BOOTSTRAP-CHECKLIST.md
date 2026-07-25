# Bootstrap checklist

Copy this file to the new repo's root as `BOOTSTRAP-CHECKLIST.md` and check items off as the bootstrap advances — it is the progress tracker for the [bootstrap prompt](08-bootstrap-prompt.md) (the prompt does the work; this file records where you are). Each phase ends with **your review** — don't tick the phase until its verify items are true. If a session dies, re-anchor with: `Read docs/framework/README.md, BOOTSTRAP.md and BOOTSTRAP-CHECKLIST.md — continue from the first unchecked item.`

## Stage A — Grill to the bootstrap charter

**You type:** the full contents of `docs/framework/08-bootstrap-prompt.md`

Grill topics resolved (each is a DEC, or an OPN naming what it blocks):

- [x] A1 — Product seed: scope, reference material, product authority (DEC-001..003)
- [x] A2 — Vocabulary locks: project/module/experience/actor names (DEC-004..007)
- [x] A3 — Stack & deviations (every deviation names its equivalent per invariant) (DEC-008..014)
- [x] A4 — House conventions: guardrails-companion rules adopted/deviated (DEC-015)
- [x] A5 — Team & review reality: HITL reviewers, solo path if needed, WIP limit, agent runtimes (DEC-016..018)
- [x] A6 — Ceremonies calibration: retro cadence, ADR-on-second-occurrence, retro owner (DEC-019)
- [x] A7 — Hosting & CI: platform, plan limits verified (branch protection!), cloud owner, existing infra state confirmed with a human (DEC-018, DEC-020 — ruleset behavior re-verified at Phase 1)
- [x] A8 — Design source: reference material, number of experiences, copy language — or a deliberate OPN deferring Phase 4 (DEC-021)
- [x] A9 — Telemetry: opt-in, privacy constraints, where human time is recorded (DEC-022..023)
- [x] A10 — Spec-less lane: the path for hotfixes/infra work defined (DEC-025)

Charter gate:

- [x] `BOOTSTRAP.md` written (DEC/OPN log + accepted defaults + deviations + phase plan — 25 DEC, 2 OPN)
- [ ] **Human reviewed and approved the charter** → type: `Charter approved. Proceed with Phase 0.`

## Phase 0 — Product corpus *(doc 00)*

- [x] Product-deep grill completed (seeded by A1/A2 — 4 rounds; closed charter OPN-001 → DEC-026)
- [x] `docs/product/mvp/` corpus written: brief, actors (ACT-001..004), glossary, bounded contexts (BC-001..005), use cases (UC-001..021), business rules (BR-001..015), journeys (J1..J4), open decisions (OPN-002..004), backlog-shaping rules (RULE-001..007), foundation-vs-product split, locked decisions (DEC-001..041)
- [x] Charter DEC/OPN entries carried into the corpus decision files (10-locked summarizes DEC-001..025; BOOTSTRAP.md stays authoritative for charter text)
- [x] Verify: readable in one sitting (11 files, ~28 KB total); every UC/BR has a stable ID; decisions are DEC or OPN only — no third state
- [ ] **Human approved** → `Proceed with Phase 1.`

## Phase 1 — Technical scaffolding *(docs 02 + 06 phases 1–2)*

- [ ] OpenSpec installed (`@fission-ai/openspec`), `openspec/config.yaml` context populated from the corpus
- [ ] **project-scaffolding proposal written — zero code** — and opened as a draft PR
- [ ] **Human reviewed the spec (HITL #1)** → `Spec approved. Implement it.`
- [ ] Implemented from `docs/framework/references/` (analyzers, ArchTests, test bases, build props, workflows, hooks) with the rename checklist applied
- [ ] Verify: build fails on a deliberate cross-module reference; a test with a bad name fails ArchTests; commit with a bad message is rejected; CI green
- [ ] **Human approved** → `Proceed with Phase 2.`

## Phase 2 — AI delivery layer *(doc 03)*

- [ ] Proposal reviewed (HITL #1) before implementing
- [ ] `AGENTS.md` router + pointer files for every runtime from A5 — each pointer verified to target `AGENTS.md`
- [ ] `writing-great-skills` vendored with its MIT NOTICE
- [ ] Telemetry stack live per A9 (collector up, `sessions.jsonl` mapping written on session start)
- [ ] Wrapper commands carry **every** gate in doc 05 — including the "known gaps" section
- [ ] Verify: `/ds:propose` on a non-ready issue refuses and names `/ds:grill`; a fifth concurrent implement refuses
- [ ] **Human approved** → `Proceed with Phase 3.`

## Phase 3 — Ceremonies *(doc 04)*

- [ ] Nine `status:*` labels created (one-time `gh label create`)
- [ ] Definition of Ready written on the corpus RULE catalog
- [ ] Append-only retro log + ADR template + `CONTRIBUTING.md` + `ONBOARDING.md` (≤ ~40 lines)
- [ ] Solo/team review path (A5) and spec-less lane (A10) documented
- [ ] Issue/PR templates in place, "Time invested" section kept
- [ ] **Human approved** → `Proceed with Phase 4.` (or skip per charter)

## Phase 4 — Design system *(doc 07 — skip if deferred via A8 OPN)*

- [ ] Canonical system extracted from the A8 reference (tokens CSS, one kit per experience, content/voice fundamentals), aligned to the DEC log with IDs cited
- [ ] `DESIGN.md` + runtime token adapter generated (marked generated, regeneration command in header)
- [ ] Three-stage drift gate wired into CI lint lane
- [ ] Value-free design-router skill written
- [ ] Verify: a raw hex value or hardcoded JSX string in frontend code fails CI
- [ ] **Human approved** → `Proceed with Phase 5: run <small real feature> through the full loop.`

## Phase 5 — Prove the loop

- [ ] One real feature ran grill → propose → implement → sync → refine **guided only by the commands' own refusals**
- [ ] Smoke E2E exists for the first real flow (auth)
- [ ] Main shows exactly one squash commit with synced specs + archived change + first retro entry
- [ ] Any manual intervention needed outside the commands → logged as a framework bug and the **command** fixed
- [ ] Bootstrap done — delete this file or archive it; the retro log takes over from here
