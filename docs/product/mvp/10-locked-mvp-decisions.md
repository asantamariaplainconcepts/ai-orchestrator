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
- **DEC-044 — opencode runtime contract** *(closes [OPN-004](07-open-decisions.md), observed
  against CLI v1.18.6 on the authoring machine AND in a clean container)*: invocation is
  `opencode run -m <provider/model> --format json "<prompt>"`; output is a JSONL event stream —
  `text` events carry the reply, `step_finish` events carry `part.tokens` and `part.cost`,
  summed for BR-011. Provider credentials ride the environment (`OPENCODE_API_KEY`) and MAY be
  absent: free models (`opencode/*-free`) run with none, verified with no ambient state.
  Default model `opencode/deepseek-v4-flash-free`, config-overridable.

- **DEC-045 — Azure DevOps mapping** *(closes [OPN-003](07-open-decisions.md))*: a work item is
  a Story (`System.Id` is the vendor id); the Connector's two coordinates are the organisation
  and the project; `System.Tags` — one semicolon-delimited string — is the label set, so a
  trigger label is a tag and matching is unchanged; `System.State` is passed through verbatim
  exactly as GitHub's is, because the process template owns the vocabulary and normalising it
  would invent states no board has. The estimate field is process-dependent
  (`Microsoft.VSTS.Scheduling.StoryPoints` on Agile, `…Effort` on Scrum, absent on Basic): the
  Connector tries them in order and **refuses** rather than guessing. Code lives in a repository
  *inside* the project rather than being the project, so a Connector may name one separately;
  GitHub leaves it empty. The implementation is **unexercised** — see ADR-0005.

- **DEC-046 — the deploy credential lives in CI, behind an approval** *(supersedes design D7 of
  the azure-dev-infrastructure change)*: `terraform apply` and `deploy.sh` run in GitHub Actions
  under a federated (OIDC) identity scoped to this repository's `dev` Environment, released only
  when a required reviewer approves a run whose plan has already been printed. D7 held that apply
  must be a human action with their own Azure identity; that was right about *what* needed
  protecting — a human decision in front of a plan — and wrong about the form, because it assumed
  the human always has a terminal. When the owner did not, the posture stopped protecting
  anything and simply prevented all deployment. The credential is short-lived, has no client
  secret, is scoped to a resource group rather than the subscription, and cannot be minted at all
  for an unapproved run. Pull-request validation stays credential-free (`terraform.yml`), which
  is the property that actually made D7 safe and is unchanged.

- **DEC-047 — approval is a property of the environment, not of the pipeline** *(refines
  DEC-046)*: the Environment is what scopes a credential — a token minted for `dev` names `dev`
  in its subject and no credential elsewhere accepts it, so `prod` will hold its own identity
  that a dev run cannot reach. Whether a run *waits for a human* is separate, configured as
  required reviewers on each environment, and set per environment by how expensive being wrong
  is. **`dev` runs unattended**: it is disposable, `terraform destroy` recreates it, and the
  owner deploys to it many times a day. **`prod` will require a reviewer.** DEC-046 read as
  though the gate were intrinsic to the design; it is not, and saying so is what keeps the next
  environment from inheriting dev's answer by accident. The consequence to state plainly: with no
  reviewer on `dev`, anyone who can merge to `main` can change that resource group and read its
  secrets, including the database password — Terraform manages those secrets, so it reads them on
  every refresh. That is acceptable for a disposable environment in a solo repository and would
  not be for production data.
