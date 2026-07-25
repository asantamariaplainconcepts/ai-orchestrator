# Proposal: project-scaffolding

## Why

Nothing exists yet — this change stands up the technical foundation every later change
builds on: the modular-monolith skeleton with its enforced seams, the four-tier test
pyramid, the commit/CI gates, and the one-command dev loop. It is Foundation work
([09-foundation-vs-product-split.md](../../../docs/product/mvp/09-foundation-vs-product-split.md)),
proposed before any code exists so the layout and open questions get shaped at review
cost, not rework cost.

## What Changes

Seven new capabilities (delta specs under `specs/`):

1. **repo-structure** — `src/` as solution root, build props (analyzers auto-attach,
   `TreatWarningsAsErrors`, CPM, SDK pin), repo-root hygiene.
2. **backend-architecture** — self-registering modules, vertical slices, CQS decorator
   pipeline, ErrorOr + ProblemDetails, schema-per-module Postgres; **Projects** as the
   reference module with one exemplar slice (`CreateProject`, cites UC-003).
3. **frontend-architecture** — Vite + React + TypeScript SPA served same-origin
   (DEC-009), VSA slices, TanStack Query, typed English i18n catalog.
4. **testing-strategy** — unit / functional (Testcontainers + Respawn) / E2E
   (Aspire + Playwright) / ArchTests, all four tiers wired on day 0.
5. **dev-workflow** — Husky self-installing hooks: CSharpier + lint-staged pre-commit,
   commitlint (Conventional Commits) commit-msg.
6. **ci-pipeline** — orchestrator workflow + reusable stages with the paths-filter
   `changes` gate; lint, build-test, spec-validate, e2e lanes; artifacts on failure only.
7. **dev-orchestration** — Aspire AppHost composing host + Postgres + **Azurite** +
   frontend dev server; OpenTelemetry via ServiceDefaults from day 0.

## Out of scope (deliberate, reviewable)

- **Terraform/deploy lane** — its own change after the loop is proven locally
  (greenfield per DEC-020; nothing exists to respect; a deploy lane has no consumer yet).
- **Auth** (blocked by [OPN-002](../../../docs/product/mvp/07-open-decisions.md)),
  **AI delivery layer** (bootstrap Phase 2), **design system** (Phase 4).
- Any product slice beyond the single exemplar. The Connector seam, queue dispatch,
  and Agent execution arrive as product changes through the loop.

## Impact

- New: `src/` (solution, BuildingBlocks, Projects module, host, AppHost, tests),
  `src/frontend/`, `.husky/`, `.config/`, `.github/workflows/`.
- Affected specs: all seven are ADDED (no existing specs yet).
- No breaking changes possible — first change in the repo.
- Copy-as-is assets lifted from the local framework kit references with the
  `DsConnect.*` → `AiOrchestrator.*` rename checklist applied (DEC-042: the kit itself
  stays out of the repo).
