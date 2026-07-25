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

- [x] OpenSpec installed (`@fission-ai/openspec` 1.6.0; pinned by name+version in the CI lane), `openspec/config.yaml` context populated from the corpus
- [x] **project-scaffolding proposal written — zero code** — and opened as a draft PR (#1, `openspec validate` green)
- [x] **Human reviewed the spec (HITL #1)** → `Spec approved. Implement it.`
- [x] Implemented from `docs/framework/references/` (analyzers, ArchTests, build props, `.editorconfig`, hooks, CI shapes) with the `DsConnect.*` → `AiOrchestrator.*` rename checklist applied
- [x] Verify: build fails on a deliberate cross-module reference (MOD002, signature-level) — and the body-only case it cannot see is caught by the ArchTest assembly check; public entity/handler probes fail with MOD003/MOD005/CQS001; a test with a bad name fails ArchTests; `git commit -m "fixed stuff"` is rejected and does not land; hardcoded JSX copy fails `pnpm lint`; Release build 12 projects / 0 warnings; 18 tests pass locally
- [x] **CI green on every lane, including E2E** (PR #1, branch HEAD). The E2E lane's first run found two real defects — the host had no `http` endpoint (which also broke `aspire run`) and nothing applied migrations — both fixed. Still unproven by any run: `aspire run` itself (registry egress blocked locally). See the close-out note in `openspec/changes/archive/2026-07-25-project-scaffolding/tasks.md`
- [x] **Synced to main** — retro entry appended, delta specs folded into `openspec/specs/` (7 capabilities), change archived as `2026-07-25-project-scaffolding`, squash-merged as one commit (`797c56c`) with no `[skip ci]` leak. CI green on main. One post-merge finding recorded: the squash message itself failed commitlint, because a squash body is authored at merge time and no local hook sees it
- [ ] **Human approved** → `Proceed with Phase 2.`

## Phase 2 — AI delivery layer *(doc 03)*

- [x] Proposal reviewed (HITL #1) before implementing (PR #2, spec approved)
- [x] `AGENTS.md` router + pointer files for every runtime from A5 — pointer audit clean: all three target `AGENTS.md`, zero refs elsewhere
- [x] `writing-great-skills` vendored with its MIT NOTICE (extended with this repo's adaptation note)
- [x] Telemetry stack live per A9 — `map-session-change.mjs` probed with a real payload (attributed this branch to its change; fail-soft verified); data in gitignored `.telemetry/` (DEC-022). Caveat noted: this machine's :4317 is shared with another project's collector
- [x] Wrapper commands carry **every** gate in doc 05 including all seven known gaps as implemented guards, plus an eighth of our own: `/aio:sync` lints the squash subject/body pre-merge (probed both ways — the exact Phase 1 defect is now caught while the merge is still preventable)
- [x] Verify: `/aio:propose`'s gate refuses toward `/aio:grill` (dry-run against real state); the WIP gate reads its limit (2) from `.claude/workflow.json` alone; a third concurrent implement would refuse. Full-refusal exercising with real issues happens in Phase 5, once labels exist
- [x] **Synced to main** via `/aio:sync`'s own steps (its first real run) — retro appended, 4 delta specs folded in (11 living specs), archived as `2026-07-25-ai-delivery-layer`, squash-merged as one commit (`5e8d6e4`), no `[skip ci]` leak, CI green on main. **The squash-lint gate worked in production:** the message was validated (exit 0, longest line 81/100) before the merge — the Phase 1 defect cannot recur
- [ ] **Human approved** → `Proceed with Phase 3.`

## Phase 3 — Ceremonies *(doc 04)*

- [x] **FIRST: the two overdue ADRs written** — [ADR-0001](docs/adr/0001-verify-claims-by-exercising-them.md) (verify claims by exercising them) and [ADR-0002](docs/adr/0002-test-tiers-must-not-provision-their-own-preconditions.md) (a test tier must not hide a precondition the application lacks). Each cites its incidents and names the check that catches the class; both linked from their triggering retro entries
- [x] Nine `status:*` labels created (one-time `gh label create`), plus `lane:spec-less` — ten total, verified present
- [x] Definition of Ready written **as citations** of `RULE-001..007`, not copies, so a rule change propagates without editing it; names the three open OPNs blocking work today
- [x] Append-only retro log (4 entries) + ADR template + README + `CONTRIBUTING.md` + `ONBOARDING.md` at **exactly 40 lines** (trimmed from 49 by moving facts to canonical homes — design D4's prediction observed working)
- [x] Solo path (DEC-016) and spec-less lane (DEC-025) documented in `CONTRIBUTING.md`, along with the no-branch-protection decision stated with its cost
- [x] Issue/PR templates re-checked against the lifecycle — both apply `status:backlog`, "Time invested" retained; no edit needed
- [x] **Synced to main** via `/aio:sync` — retro appended, 4 delta specs folded in (15 living specs), archived as `2026-07-25-ceremonies`, squash-merged as one commit (`c5c11ef`), message linted pre-merge (exit 0, longest line 79/100), no `[skip ci]` leak, CI green on main
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
