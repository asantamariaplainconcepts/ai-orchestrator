# Locked MVP decisions

Binding decisions with rationale. Locked ≠ sacred: challengeable until something is
built on one; after that, changing it is a BREAKING product change rippling through
the corpus. DEC-001..025 were made in the bootstrap charter
([BOOTSTRAP.md](../../../BOOTSTRAP.md), authoritative text there — summarized here for
one-stop reading); DEC-026+ were made in the Phase 0 product grill.

## Carried from the charter (summaries)

- **DEC-001 — Product:** AI Orchestrator, internal web app orchestrating AI agents over
  vendor-abstracted project backlogs.
- **DEC-002 — MVP claim:** website → real backlog → labeled story → KEDA Agent job →
  configured action → result visible in website.
- **DEC-003 — Authority:** owner, solo; no written prior sources.
- **DEC-004 — Name:** `ai-orchestrator` / `AiOrchestrator` / "AI Orchestrator".
- **DEC-005 — Terms:** Agent, Connector, Automation.
- **DEC-006 — Actors:** Admin, Member, Agent.
- **DEC-007 — Modules vocabulary:** Projects / Backlog / Agents.
- **DEC-008 — Backend:** .NET modular monolith, PostgreSQL schema-per-module.
- **DEC-009 — Frontend:** React web-only (Vite), same-origin, VSA slices, token-only
  styling, typed i18n.
- **DEC-010 — Infra:** Terraform/Azure, ACA + ACA Jobs + KEDA, Aspire dev loop.
- **DEC-011 — Connectors:** GitHub + Azure DevOps in MVP, sequenced (GitHub first).
- **DEC-012 — Runtimes:** pluggable per Automation; Claude Code headless first,
  opencode second.
- **DEC-013 — Dispatch:** Azure Storage Queue + KEDA queue scaler; Azurite locally.
- **DEC-014 — AI credentials:** Key Vault, per-project secrets.
- **DEC-015 — plain-dotnet-guardrails as-is** (Postgres the sole deviation).
- **DEC-016..019 — Ways of working:** solo review path, WIP 2, runtimes Claude Code +
  opencode + Copilot, kit ceremony defaults.
- **DEC-020 — Cloud:** owner's subscription, greenfield, Terraform owns everything.
- **DEC-021 — Design:** Atlas Plain Concepts as style reference only; one desktop web
  experience; English copy.
- **DEC-022/023 — Telemetry:** framework usage telemetry local/private; product OTel →
  Azure Monitor.
- **DEC-024 — Identity: Entra ID** (recorded override of kit precedent; verification
  precondition [OPN-002](07-open-decisions.md); reopen trigger defined).
- **DEC-025 — Spec-less lane:** hotfix + infra/tooling, `lane:spec-less`, retro mandatory.

## Phase 0 decisions

- **DEC-026 — MVP action catalog** *(closes charter OPN-001)*: Implement→PR,
  Refine/comment, Transition state, Estimate. All four in MVP; Implement→PR carries
  the MVP claim and lands first. Rationale: owner wants the full governance story
  demonstrable; sequencing contains the risk.
- **DEC-027 — Trigger UX: both sides, website writes back.** The vendor-side label is
  the single trigger semantics; the website applies/removes it through the Connector.
  Rationale: one mechanism, two entry points — no divergent trigger truths.
- **DEC-028 — Detection: webhooks + polling, polling first.** Owner override of the
  leaner polling-only default. Both normalize into one event stream (BR-015). Build
  order: polling lands first (also serves local dev, where webhooks cannot reach);
  webhooks layer on per vendor. Poll default 60 s, per-project configurable.
- **DEC-029 — Backlog is a cached mirror** in Postgres (read model); vendor stays
  source of truth (BR-008). Rationale: trigger diffing, run-history joins, fast UI,
  rate-limit safety.
- **DEC-030 — One PAT per project** covering backlog read/write and code clone/push/PR,
  stored as a Key Vault reference. Rationale: simplest MVP credential shape; finer
  scoping (GitHub App / service connections) is post-MVP.
- **DEC-031 — Run visibility:** status + output link + fetched logs (no live streaming)
  + cost, per project and per story.
- **DEC-032 — Concurrency:** one active Run per story (BR-001); per-project cap,
  default 2, Admin-configurable (BR-002).
- **DEC-033 — Automation overlap rejected at config time** (BR-003). Rationale: kit
  spirit — put the rule in a gate; runtime stays deterministic.
- **DEC-034 — Authorization is permission-based;** roles are permission bundles; MVP
  ships exactly two fixed bundles (Admin = all; Member = observe + trigger + approve +
  cancel). Custom roles post-MVP.
- **DEC-035 — Run now in MVP,** available to Admin and Member; doubles as the re-run
  path (BR-013).
- **DEC-036 — Failure policy:** no automatic retries (BR-004); per-phase timeout,
  default 30 min, per-Automation configurable (BR-005).
- **DEC-037 — Notifications are out of MVP scope.** The website is the only surface.
  (The only confirmed out-of-scope item; approval gate, cancellation and cost tracking
  are IN — owner scope-up, DEC-039..041, DEC-038.)
- **DEC-038 — Cost stored in Postgres:** the runtime reports tokens/cost at run end;
  the orchestrator persists them on the Run (BR-011). Chosen over Azure-Monitor-only
  sourcing so cost works in the local loop too.
- **DEC-039 — Approval is a per-Automation toggle** (`requiresApproval`), not global.
- **DEC-040 — Approval shape: plan-then-approve.** Two-phase Run mirroring this
  framework's own spec review: Agent produces a Plan, human reviews it in the website
  and marks it ready, execution follows (BR-007). Owner chose the richer shape
  deliberately, knowing it doubles job orchestration.
- **DEC-041 — Cancellation in MVP** for both queued and running states (BR-012).

## Phase 2 decisions

- **DEC-043 — Workflow command prefix is `/aio:*`.** The reference kit's `/ds:*` prefix
  abbreviated its source project's domain (dental system) and carried no meaning here; the
  owner locked `aio` (AI Orchestrator) before the AI-layer change merged. The prefix is coined
  vocabulary like DEC-005's terms: renaming it later is churn across commands, skills, docs,
  and muscle memory. The retro log's existing `/ds:*` mentions stay as written — it is
  append-only history.

## Phase 1 decisions

- **DEC-042 — The framework kit stays out of the public repo.** `docs/framework/`
  (internal Plain Concepts reference material) is gitignored; the public repo carries
  only artifacts authored for ai-orchestrator. Kit assets enter individually as they
  are adapted and renamed. Resolves the DEC-018 (public) vs kit-provenance conflict.
