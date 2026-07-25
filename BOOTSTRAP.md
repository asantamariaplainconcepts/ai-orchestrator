# BOOTSTRAP.md — charter for AI Orchestrator

Bootstrap Definition of Ready, produced by the Stage A grill on 2026-07-25.
Framework kit: `docs/framework/` (ds-connect post-mortem + reproduction recipes).
These DEC/OPN entries seed the Phase 0 product corpus decision files
(`docs/product/mvp/10-locked-mvp-decisions.md` / `07-open-decisions.md`).

Rule of the log: every entry is either **DEC** (locked, with rationale — one current
answer) or **OPN** (deliberately open, naming what it blocks). No third state.

---

## Decision log

### Product seed (A1)

- **DEC-001 — Product.** *AI Orchestrator*: an internal web application where users
  create **Projects**, connect each to an issue backlog through a vendor-abstract
  **Connector**, and configure **Automations** ("story with label/state X → an
  **Agent** does Y"). When a user story matches, a KEDA-scaled Azure Container Apps
  Job (the Agent) reads the story and executes the configured action (code changes,
  issue changes, …). Audience: Plain Concepts teams (internal tool).
- **DEC-002 — MVP claim.** The MVP proves exactly: *"From the website, a user connects
  a project to a real backlog, labels one user story, and an AI job spins up via KEDA
  and performs the configured action on it — with the result visible back in the
  website."* Everything not needed for that claim is post-MVP.
- **DEC-003 — Sources & authority.** No written product material exists; the product
  is extracted from the owner by the Phase 0 grill. Product authority: **the repo
  owner (Andoni), solo** — their word settles product questions the docs don't answer.

### Vocabulary locks (A2)

- **DEC-004 — Name.** Product/repo name **`ai-orchestrator`** (corrected spelling),
  solution `AiOrchestrator`, display name "AI Orchestrator".
  *Action before Phase 1:* rename the local folder `ai-loop-app` → `ai-orchestrator`
  and create the GitHub repo under that name.
- **DEC-005 — Coined terms.**
  | Term | Meaning |
  |---|---|
  | **Agent** | The AI execution unit: a KEDA-scaled ACA Job that reads a story and acts. (Not "pod" — collides with Kubernetes.) |
  | **Connector** | The vendor abstraction over issue backlogs (GitHub, Azure DevOps, …). |
  | **Automation** | A configured mapping: story label/state → Agent action. The product's core noun. |
- **DEC-006 — Actors.** **Admin** (creates/configures Projects, Connectors,
  Automations), **Member** (marks stories, watches Agent runs), **Agent** (system
  actor). Full ACT catalog in Phase 0.
- **DEC-007 — Module vocabulary.** **Projects / Backlog / Agents**. Whether each is a
  real backend module is decided at scaffolding, at real seams only (kit ADR-0002).

### Stack & deviations (A3)

- **DEC-008 — Backend.** Kit default confirmed: .NET modular monolith — self-registering
  modules, vertical slices, CQS decorator pipeline, ErrorOr, schema-per-module
  **PostgreSQL** (house deviation from SQL Server, same as ds-connect's).
- **DEC-009 — Frontend (deviation, equivalents named).** **React web-only**
  (Vite + React Router), not Expo. Invariant equivalents:
  - *Same-origin serving:* Vite build output copied into host `wwwroot`, `index.html`
    fallback, reserved-prefix list; dev via Aspire proxy. No CORS.
  - *Slice conventions:* frontend mirrors backend VSA — `features/<feature>/` slices,
    thin routes, no `services/`/`utils/` dumping grounds.
  - *Design/copy gates:* token-only styling + typed i18n catalog + hardcoded-copy CI
    gate, unchanged from the kit.
  - *Four-tier tests:* unchanged; E2E is Playwright against the real Aspire-booted host.
- **DEC-010 — Infra.** Terraform on Azure: ACA hosts the website/API, **ACA Jobs +
  KEDA** run Agents, Azure Database for PostgreSQL. Aspire composes the local dev loop
  (host + Postgres + frontend dev server + queue emulator) — one-command inner loop.
- **DEC-011 — Connector scope (override of kit default).** MVP ships the Connector
  seam with **two implementations: GitHub and Azure DevOps**. Recommended default was
  one; owner deliberately chose both. Standing rule: **sequenced, never parallel** —
  GitHub lands first and proves the seam; AzDO rebases onto the proven contract
  (post-mortem failure family #3).
- **DEC-012 — Agent runtime.** Pluggable per Automation. MVP ships **two runtime
  images: Claude Code headless first, opencode second** — same sequencing rule as
  DEC-011. The seam must make a third runtime trivial to add.
- **DEC-013 — Dispatch.** Queue-based KEDA trigger on **Azure Storage Queue**: the
  website enqueues an Automation-run message; KEDA scales Agent jobs on queue length.
  Dev loop uses Azurite.
- **DEC-014 — AI credentials.** **Azure Key Vault, per-project secrets**, referenced
  by ACA Jobs. Automations reference a named secret. No provider keys in Postgres or
  in git — the repo is public (DEC-018).

### House conventions (A4)

- **DEC-015 — plain-dotnet-guardrails adopted as-is.** ErrorOr + `{Entity}Errors` +
  `ApiResults.Problem`, fixed CQS decorator order via `AddVsaCqsArchitecture()`,
  CPM/build props, `.editorconfig`, logging hygiene, CQS001 analyzer, LogEventIdTests,
  `Subject_Should_Constraint` naming. Sole recorded deviation: PostgreSQL (DEC-008).

### Team & review reality (A5)

- **DEC-016 — Solo path from day 0.** Owner is both HITL reviewers. Per the kit's
  ADR-0011 story: self-review checklist on the PR + the `status:*` label transition is
  the recorded gate; no formal PR approval required (GitHub forbids self-approval).
- **DEC-017 — WIP limit 2**, enforced by the `/ds:implement` command's refusal.
  Raising it requires an ADR.
- **DEC-018 — Runtimes & hosting.** Contributor agent runtimes: **Claude Code,
  opencode, GitHub Copilot** → Phase 2 creates `CLAUDE.md` and
  `.github/copilot-instructions.md` pointers to `AGENTS.md` (opencode reads
  `AGENTS.md` natively — verified at Phase 2). Repo: **GitHub, personal account,
  public**. Branch protection/rulesets are free on public repos — actual behavior
  verified at Phase 1, not assumed.

### Ceremonies calibration (A6)

- **DEC-019 — Kit defaults accepted.** Retro entry after every change
  (telemetry-sourced time/cost); ADR graduation **enforced at the second occurrence**
  of any pattern; the owner owns the retro log's honesty.

### Cloud reality (A7)

- **DEC-020 — Greenfield, confirmed by the human.** Owner's personal Azure
  subscription; **zero existing infra state** (no resource groups, no Terraform state,
  no prior deployment). Terraform owns everything from the first apply. Per kit
  ADR-0006, any future claim about live infra is verified against reality (a CI step
  existing ≠ it ever succeeded).

### Design source (A8)

- **DEC-021 — Design.** One experience: the desktop-first web app.
  Visual reference: the **Atlas Plain Concepts style** (<https://atlas.plainconcepts.com/>)
  as a *style* reference only — tokens (spacing, type scale, color feel) are derived;
  no Plain Concepts logos/brand assets enter this public repo. Visual authority: owner,
  solo. Product copy: **English**, in the typed i18n catalog from day 0.
  Phase 4 proceeds (not deferred).

### Telemetry (A9)

- **DEC-022 — Framework telemetry.** Kit default accepted: OTel collector as system of
  record, **session-id join** via SessionStart hook (env-var tagging is proven
  broken). `sessions.jsonl` and collector storage are **gitignored — never committed
  to the public repo** (telemetry carries user emails). Human time is recorded in the
  PR "Time invested" section — telemetry cannot capture it.
- **DEC-023 — Product telemetry.** The Agent pods (and the hosted app) export OTel to
  **Azure Monitor** in cloud environments. Same ServiceDefaults plumbing, different
  exporter per environment.

### Identity (gap-sweep)

- **DEC-024 — Entra ID (company tenant), a recorded override of kit precedent.**
  ds-connect locked Entra and reversed to Keycloak when foundation work exposed the
  cost; the owner chooses Entra knowingly (true internal SSO for Plain Concepts
  users). **Verification precondition — OPN-002 must close before the auth slice is
  proposed.** Reopen trigger: if tenant app-registration access or a workable
  local-dev/functional-test auth strategy cannot be verified, this decision reopens
  (candidates: GitHub OAuth, Keycloak) — treat locked decisions as challengeable
  until something is built on them.

### Spec-less lane (A10)

- **DEC-025 — Kit default accepted.** Hotfixes + pure infra/tooling changes may bypass
  grill→propose: issue label `lane:spec-less`, normal branch + PR + CI, **retro entry
  still mandatory**, no OpenSpec change to archive (sync detects the label and skips
  the archive step). Everything user-visible goes through the full loop.

## Open decisions

- **OPN-001 — MVP Automation action catalog.** "Code changes, issue changes, or
  whatever the user decides" is not yet a deterministic list. **Blocks:** completion
  of the Phase 0 use-case catalog (UC-xxx) for the Agents module and the Automation
  config schema. **Closes:** during the Phase 0 product grill.
- **OPN-002 — Entra ID reality check.** Unverified: (a) owner can create app
  registrations in the Plain Concepts tenant from this project's context; (b) a
  local-dev + functional-test auth strategy exists (Entra cannot be containerized —
  real-token tests need a test tenant, or the decision reopens per DEC-024).
  **Blocks:** the auth foundation slice and Phase 5's smoke E2E. **Closes:** by the
  owner exercising both paths for real (kit ADR-0006), before the auth slice is
  proposed in Phase 1's backlog.

## Accepted kit defaults (unmodified)

Nine-state label lifecycle · one issue = one branch = one PR with two HITL gates ·
OpenSpec (`@fission-ai/openspec`) behind `/ds:*` wrapper commands · hard guardrails of
doc 05 including its "known gaps" from day 0 · four-tier test pyramid · Husky hooks
mirrored in CI · docs-as-maps with stable IDs (ACT/BC/UC/BR/RULE/DEC/OPN) ·
append-only retro log · ADR template · ~40-line onboarding.

## Phase plan

| Phase | Scope | Notes |
|---|---|---|
| 0 — Product corpus | Eleven-catalog corpus in `docs/product/mvp/`, product-deep grill | Carries this DEC/OPN log in; closes OPN-001 |
| 1 — Technical scaffolding | OpenSpec + project-scaffolding proposal (zero code) → HITL #1 → implement from `references/` | Folder/repo rename (DEC-004) happens first; verify branch protection (DEC-018) |
| 2 — AI delivery layer | AGENTS.md router + 3 runtime pointers, vendored writing-great-skills, telemetry stack (DEC-022), `/ds:*` gates incl. known gaps | |
| 3 — Ceremonies | 9 labels, DoR on RULE catalog, retro log, ADR template, CONTRIBUTING, ONBOARDING, solo path (DEC-016), spec-less lane (DEC-025) | |
| 4 — Design system | Atlas-style tokens (DEC-021), DESIGN.md + adapter, drift gate in CI, design-router skill | Not deferred |
| 5 — Prove the loop | One small real feature through grill→propose→implement→sync→refine; smoke E2E for auth | Requires OPN-002 closed |

**Standing rules for every phase:** verify infra claims against reality before building
on them · sequence dependent slices, check surface overlap before proposing · branch
from fresh `origin/main`, branch names end with the change slug · confirm before
mutating shared state · never skip a lifecycle gate · on second occurrence, write the
ADR and turn it into a gate.

---

*Status: awaiting human review. Approve with:* `Charter approved. Proceed with Phase 0.`
