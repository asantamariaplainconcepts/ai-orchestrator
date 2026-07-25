# Design — project-scaffolding

## Verified reality (ADR-0006 discipline — checked, not assumed)

- `dotnet` SDK **10.0.100** installed locally (pins `global.json`).
- Docker daemon **29.2.1** running (Testcontainers-based functional tests are viable).
- Aspire CLI **13.4.4** installed (`aspire start` dev loop is viable).
- Node **v23.11.0** + pnpm installed (Vite frontend + hooks tooling viable).
- OpenSpec CLI **1.6.0** (`@fission-ai/openspec` — the bare `openspec` package does
  not exist; CI must pin this package name and version).
- GitHub repo is public on a personal account → branch protection/rulesets available
  on the free plan (verified: repo created successfully as public).

## Decisions

### D1 — Projects is the reference module

The exemplar every later module copies (`Backlog`, `Agents`) must exist from day 0.
**Chosen:** `Projects` (BC-001) with one exemplar slice `CreateProject` (UC-003),
deliberately minimal: name + connector placeholder fields only — no vendor logic, no
Key Vault, no validation beyond shape (those arrive via product changes).
**Rejected:** a synthetic `Sample` module (dead code the day the first real module
lands; violates "no speculative surface").

### D2 — Modules at real seams only

One module ships in this change. `Backlog` and `Agents` are *not* scaffolded empty —
kit ADR-0002: a module exists only when an independent lifecycle/boundary demands it.
The analyzers + ArchTests land **before** the second module exists (doc 02 order).

### D3 — Azurite in the dev loop from day 0

The Storage Queue is the product's dispatch spine (DEC-013). The AppHost composes
Azurite alongside Postgres now so the queue contract never gets mocked later.
**Rejected:** adding it "when dispatch lands" — recurring kit lesson: dev-convenience
divergence between local and real infra is a defect source.

### D4 — Terraform deferred to its own change

**Chosen:** no `infra/` in this change; the deploy lane is a follow-up change once the
loop is proven locally (bootstrap Phase 5). Rationale: greenfield (DEC-020), no
consumer for a deploy lane yet, and the scaffolding review should stay one-sitting
readable. **Rejected:** doc 02's full day-0 sweep including Terraform — right for
ds-connect (client deadline), oversized for the first reviewable unit here.
**Consequence:** the `e2e` CI lane runs in local-emulator mode only until then.

### D5 — Frontend deviations already locked

Vite + React Router web-only SPA per DEC-009 (equivalents recorded in the charter).
The i18n hardcoded-copy gate ships **in this change** (ESLint rule in the lint lane);
the design-token drift gate ships with Phase 4 (no canonical tokens exist yet —
gating on a nonexistent artifact would red every PR).

### D6 — Postgres via EF Core, schema-per-module, GUID v7

Per DEC-008 and the guardrails companion (DEC-015). `Projects` owns `projects` schema,
its migrations, and seed. Domain events dispatch post-commit; the transactional outbox
arrives only when the first async consumer exists (Agents dispatch — a later change).

## Cross-check against plain-dotnet-guardrails (A4 / DEC-015)

Adopted verbatim in this change: ErrorOr + `{Entity}Errors` + `ApiResults.Problem`;
CQS decorator order Logging → Validation → Caching → Handler → InvalidateCaching via
`AddVsaCqsArchitecture()`; CPM + `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild`;
`.editorconfig` from the kit references; CQS001 + MOD001–005 analyzers as build-failing
errors; `LogEventIdTests` equivalent via ArchTests unique-event-ID rule;
`Subject_Should_Constraint` naming enforced by ArchTests. Deviation: none new
(PostgreSQL already recorded, DEC-008/DEC-015).

## Risks

- .NET 10 + Aspire 13 are current-generation; template/API churn is possible. The
  tasks pin exact versions in `global.json`/CPM so drift is explicit, and any
  incompatibility found during implementation returns to this design, not silent
  workarounds.
- Windows contributors: `.gitattributes` (`eol=lf`) + bounded test parallelism are in
  from day 0 (kit failure family #6).
