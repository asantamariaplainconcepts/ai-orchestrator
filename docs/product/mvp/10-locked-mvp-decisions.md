# Locked MVP decisions

Binding decisions with rationale. Locked ≠ sacred: challengeable until something is
built on one; after that, changing it is a BREAKING product change rippling through
the corpus. DEC-001..025 were made in the bootstrap charter
([BOOTSTRAP.md](../../../BOOTSTRAP.md), authoritative text there — summarized here for
one-stop reading); DEC-026+ were made in the Phase 0 product grill.

## Carried from the charter (summaries)

- **DEC-001 — Product:** AI Orchestrator, internal web app orchestrating AI agents over
  vendor-abstracted project backlogs.
- **DEC-002 — MVP claim:** website → real backlog → labeled story → Agent Run in a sandbox →
  configured action → result visible in website. *(The middle step read "KEDA Agent job" until
  DEC-013's supersession retired that substrate; the claim's shape is unchanged.)*
- **DEC-003 — Authority:** owner, solo; no written prior sources.
- **DEC-004 — Name:** `ai-orchestrator` / `AiOrchestrator` / "AI Orchestrator".
- **DEC-005 — Terms:** Agent, Connector, Automation.
- **DEC-006 — Actors:** Admin, Member, Agent.
- **DEC-007 — Modules vocabulary:** Projects / Backlog / Agents.
- **DEC-008 — Backend:** .NET modular monolith, PostgreSQL schema-per-module.
- **DEC-009 — Frontend:** React web-only (Vite), same-origin, VSA slices, token-only
  styling, typed i18n.
- **DEC-010 — Infra:** Terraform/Azure, ACA + Aspire dev loop. *(Amended 2026-08-09 with
  DEC-013's supersession: the ACA **Jobs + KEDA** half is gone. Agents run in ACA **Sandboxes**,
  created per Run over an authenticated API; the portal is still an ACA app.)*
- **DEC-011 — Connectors:** GitHub + Azure DevOps in MVP, sequenced (GitHub first).
- **DEC-012 — Runtimes:** pluggable per Automation; Claude Code headless first,
  opencode second.
- **DEC-013 — Dispatch:** ~~Azure Storage Queue + KEDA queue scaler; Azurite locally~~
  *(superseded 2026-08-09 by `runs-execute-in-azure-sandboxes`, #296)*: dispatch goes through the
  Postgres outbox in every habitat, consumed by the Server's own subscriber. The queue's three
  reasons retired with the pod substrate — execution stopped being heavy (an API call and a poll
  loop; the heavy half lives in a per-Run sandbox that scales itself), the worker's scale-to-zero
  stopped saving anything, and its separate identity no longer stood between the portal and a
  root-equivalent socket. Given up knowingly: horizontal scale-out of execution and the separate
  execution identity; either returns by placing a consumer elsewhere.
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

- **DEC-048 — the action catalogue grows past MVP, starting with the grill** *(revises DEC-026)*:
  DEC-026 fixed four actions for the MVP; the MVP shipped, and the catalogue now admits
  post-MVP actions recorded here as they land. Fifth: `GrillToReady` (#79, UC-024) — the owner's
  own workflow entry brought into the product, built on the conversational wait (#78). Its two
  settings (rubric path, ready label) default in code to the framework's conventions; the rubric
  is always the project's own document, read live, because a product-wide readiness bar would
  impose one team's standards on every repository it touches.

- **DEC-049 — MIT, and self-hostability is a product goal.** The repository was public but
  unlicensed, which legally meant all rights reserved: nobody could run or fork it, and the
  owner's stated ambition — anyone can run this — was void before engineering began. Locked
  together because the second half is the operative part: future infrastructure choices are
  evaluated against "can a stranger with Docker still run it?". Concretely: a third backend
  behind a seam (e.g. Dapr) is justified when a self-hoster needs a non-Azure component, and not
  before; features must work under `aspire run` and the published-images path (#99), not only in
  the owner's subscription. Decided 2026-07-28 during the Orbion exploration.

- **DEC-050 — a Run's output is observable while it executes** *(revises DEC-031's
  fetched-logs-only)*: DEC-031 predates the conversational actions and minutes-long implement
  Runs; watching an agent work is the strongest trust surface an agent product has. The record
  is Postgres chunks in the Runs schema — the durable store IS the stream, so BR-014 comes free
  and a crash preserves everything committed — and the window is a 3-second poll, stated lag
  ≤5s (500ms flush + 3s poll — corrected from "2s flush" in #144, which the code never did). Chosen over the SignalR hub matured on #96 because it works
  identically in every habitat DEC-049 cares about and needs no ingest-auth story while OPN-002
  is open; the hub remains the recorded latency upgrade, layering on the same writer with no
  schema change.

- **DEC-051 — the visual source of truth is the Platform theme** *(revises DEC-021's aesthetic
  half and DEC-009's styling half)*: the owner's organisation published Foundations
  (`@plainconceptsplatform/ui-theme`: design tokens for shadcn/ui + Tailwind v4) and this
  product follows it. What transfers is the foundation — the theme package, shadcn primitives
  used unwrapped, Tailwind utilities — not the whole stack: Next.js is a delivery choice
  DEC-009 already made differently, and the theme, deliberately tokens-only CSS, does not care
  which bundler serves it. Vite, VSA slices and the typed i18n catalogue stand. Migration is by
  replacement, one screen per change, both CSS systems coexisting until the last kit screen
  falls; the design gate's job is unchanged — no visual decision outside the token source — the
  source just moves. Decided 2026-07-27 when the owner pointed at Foundations mid-loop.
- **DEC-052 — a secret is protected by the habitat's own store, not by Key Vault specifically**
  *(revises BR-010's mechanism, keeps its intent; extends DEC-014 and DEC-030)*: BR-010 said
  Connector PATs exist in Postgres and logs "only as Key Vault references". That sentence names a
  mechanism, and DEC-049 introduced a habitat — a self-hoster's machine — where the mechanism does
  not exist, which made the rule false by construction rather than by anybody's choice. The rule
  now states its intent: **no secret in plaintext at rest outside the habitat's secret store, and
  names only in logs, API responses and telemetry.** Every existing guarantee survives that
  wording — a Key Vault deployment is unchanged, nothing new is logged, no response gains a value
  — while a deployment with no vault becomes expressible. Where the habitat has no managed store,
  values are protected with ASP.NET Core Data Protection and held apart from both the application
  database and the key ring; hand-rolled cryptography is forbidden, because every decision it
  would involve has a wrong answer that passes tests. The product may also *write* a secret it was
  handed, under a name it derives itself, and may never read one back — the storing seam exposes
  no read at all. Decided 2026-07-29 with #124, when asking a first-time user to pre-create a
  secret in a vault they do not have was the last thing standing between them and a connected
  backlog.
- **DEC-053 — the catalogue and the workflow are two things** *(refines DEC-040's surface; closes
  what #122 was reaching for)*: a project's Automations are an **inventory** — what it can do — and
  the Automations that hand work to one another form a **workflow**, the path they make. An
  Automation belongs to the workflow exactly when it has an edge: it hands work to another, or
  another hands work to it. Membership is derived from the edges and never stored, so the picture
  cannot claim a chain that would not fire. Everything else is a catalogue entry and its absence
  from the workflow is not an omission — it is a trigger that acts on its own when somebody applies
  its label. Rationale: the two shared a name and a list, and the cost showed up twice. #122 existed
  to give the unchained Automations "their place after the ordered ones" — a position inside the
  workflow for things that are not in it — and was closed as superseded rather than solved; the
  first draft of #136 repeated the confusion by describing the palette as "what can be placed" while
  creation lived elsewhere. Locking the vocabulary is the point: a distinction that exists only in
  one implementation's shape survives until the next refactor. Decided 2026-07-29 with #136.
- **DEC-054 — the phase timeout is bounded at 60 minutes** *(amends BR-005's
  "Admin-configurable")*: an Admin sets a phase timeout per Automation, default 30 minutes, and the
  product refuses a value above 60. The bound is not a limitation, it is what makes the rule
  keepable: a phase runs inside a platform execution budget, and with no ceiling there is no budget
  value that is provably sufficient — "Admin-configurable" silently meant "configurable up to
  whatever the infrastructure happens to allow". The provisioned budget is the ceiling plus a drain
  margin, because a worker needs time after a phase to write its outcome and a budget equal to the
  phase timeout kills it mid-write. **Three sites hold this and each names the other two** —
  `PhaseBudget.MaximumMinutes`, `replica_timeout_in_seconds` in `infra/dev/dispatch.tf`, and BR-005 —
  because no test can span a C# constant, a Terraform value and a written rule. Rationale: dev ran
  with a 600-second replica timeout against BR-005's 30-minute promise, so every implement Run over
  ten minutes was killed by the platform rather than by its own budget, and the Run that exposed it
  read as "stuck" when it was a container already terminated. Decided 2026-07-29 with #144.
- **DEC-055 — a live conversation costs a pass per message** *(closes OPN-005; analysed in
  [ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md))*: discussing a Story
  with an agent from the portal is implemented over the existing `AwaitingInput` resume loop — the
  portal writes a comment through the Connector, the resume path picks it up, the agent's next
  questions arrive as they do today. **No process stays alive for a conversation.** BR-006 decides
  it: a human wait is untimed, so a paid process waiting on a person has no cost bound, and the only
  way to bound it is a session timeout that either contradicts BR-006 or adds a second timing rule
  aimed at the human rather than the work. Weakening "a person is not a resource we hurry" to buy
  latency is the worse trade. Accepted cost, stated rather than discovered: each message pays a full
  pass and each pass re-reads the thread, so tokens grow with the conversation's length. The
  prerequisite is the Connector's first comment **write** — the seam reads comments and writes labels
  today. Rejected: a live session (contradicts DEC-013, unbounded under BR-006, and needs the ingest
  authentication OPN-002 still blocks) and a presence-based hybrid (a liveness signal that can be
  wrong, which ADR-0008's design argument already rejected once). Decided 2026-07-29 with #149; the
  capability itself is a separate item.
- **DEC-056 — a trigger's identity is the vendor's, and the schema enforces it** *(amends BR-003 and
  DEC-033)*: two triggers are the same when the vendor would treat them as the same — labels and
  states compare case-insensitively — and the identical comparison is used by the overlap guard and by
  matching, so the two can never disagree about what "the same label" means. An exact duplicate is
  refused regardless of `Enabled`, while subsumption stays enabled-only because a disabled Automation
  matches nothing. Uniqueness is a unique index over the project, the lowercased label and the
  lowercased state with an absent state coalesced to a value — not a handler convention. Rationale:
  BR-003 was enforced only in memory, so two concurrent saves both passed the check and both inserted;
  `Overlaps` carried the comment *"compare them the way the vendor does"* above an `Ordinal`
  comparison; and matching used the same `Ordinal`, so `AI:Implement` never fired for a Story labelled
  `ai:implement` — silently, with no error and no Run. The NULL trap is explicit in the index: Postgres
  treats NULLs as distinct, so an index over the raw nullable state would have permitted exactly the
  duplicate it exists to prevent. Decided 2026-07-29 with #147.
- **DEC-057 — an Automation may run a prompt the project wrote, and the project says where prompts
  live** *(extends DEC-026's catalogue in DEC-048's lane)*: a new action names a markdown file in the
  connected repository, read live at execution time; the file's **body is the prompt** and any leading
  YAML frontmatter is stripped and ignored; the answer becomes exactly **one Story comment** — no
  label, no state, no workspace, no pull request. The Automation stores only a file **name**, and the
  prompts directory is a Connector setting (default `ai/prompts/`, editable on the Settings tab,
  UC-004). Rationale: the frontmatter rule is a safety rule, not a shortcut — honouring a `model:` line
  would let a file in somebody's repository choose what this product spends, and a `tools:` line would
  let it grant itself powers the Automation withheld; ignoring it is also what makes an existing
  agentic-workflow file reusable unchanged. The single-comment surface is the reason this can ship at
  all: the prompt is untrusted text that can ask for anything, so what it may *do* is decided by this
  product, not by the file. The directory is held once rather than per Automation because a team that
  moved its prompts would otherwise edit every Automation, and each edit is a chance to leave one
  pointing at a file that no longer exists; a name that is absolute or traverses upward is refused
  rather than normalized, since a boundary that can be stepped over bounds nothing. Both refusals — an
  unreadable file, and an empty body once frontmatter is removed — precede the agent and name the
  **resolved** path, so a misconfigured directory gives itself away instead of looking like a missing
  file. Decided 2026-07-29 with #150.
- **DEC-058 — Entra ID is viable, and the portal authenticates as a BFF** *(closes OPN-002; confirms
  DEC-024 without its reopen trigger firing)*: both of OPN-002's unverified claims were exercised for
  real on 2026-07-30. (a) `infra/entra-app.sh` created the app registration, its service principal and
  a vaulted client secret in the owner's test tenant on the first run — a **directory** bootstrap
  scripted beside `ci-identity.sh` rather than Terraformed, because managing directory objects from CI
  would mean granting the deploy identity Graph permissions with admin consent (#167). (b) needed no
  new mechanism at all, and the shape is the decision: the portal is a same-origin single web app, so
  it authenticates as a **backend-for-frontend** — a confidential web client whose session is an
  `HttpOnly` cookie on the server, no token ever reaching the browser. The test tiers therefore keep
  injecting `ICurrentPrincipal` (#119) and Entra is composed only in the real host, which dissolves
  "Entra cannot be containerized" instead of solving it. Two silent traps are recorded where the
  wiring will happen: `SameSite=Strict` is right for the session cookie and wrong for the OIDC
  handshake cookies (the response arrives cross-site from `login.microsoftonline.com`), and a `Secure`
  cookie never arrives over the local profile's plain-http origin. Scope, stated: the verification ran
  against a **test tenant** — it answers *is this viable*, which is what OPN-002 asked; pointing the
  registration at any particular organisation's tenant is configuration, not a new decision. Decided
  2026-07-30 with #11 and #167.
- **DEC-059 — the session cookie is Lax, and DEC-058's Strict guidance is corrected** *(amends
  DEC-058's rationale, not its decision)*: DEC-058 recorded "SameSite=Strict is right for the session
  cookie", reasoned from every request that carries it being same-origin. The first real sign-in
  disproved it: the provider's response is a cross-site form post, the redirect that follows is a
  navigation initiated from that cross-site context, and a Strict cookie does not ride it — so the
  landing page challenges again, the provider silently signs the user back in, and the loop never ends
  (#176, observed by the owner). Lax is the correct setting: still absent from cross-site subrequests
  and POSTs, present on top-level navigations, which is precisely the post-login redirect. The BFF
  decision itself is unchanged. Decided 2026-07-30 with #176.
- **DEC-060 — the shell is anonymous, which is what lets the session cookie be Strict** *(corrects
  DEC-059's setting and DEC-058's reasoning; the BFF decision itself still stands)*: the portal's SPA
  bundle is served to anyone, and only `/api` answers `401`. The SPA navigates to sign-in itself when
  it sees one. With the shell anonymous, the single cross-site-initiated navigation in the whole flow
  — the provider's callback returning to `/` — needs no cookie, so the session cookie is
  `SameSite=Strict`. Rationale, and why DEC-059 was wrong to relax it: #176's infinite loop came from
  `RequireAuthorization` on the SPA fallback, which demanded the cookie on exactly the navigation
  Strict withholds it from. The cause was that requirement, not the setting. Removing it is strictly
  better than loosening the cookie — a Strict session cookie is not sent on any cross-site request,
  which is the stronger CSRF posture. Provenance, stated because it is not ours: this shape is
  ds-connect's, whose ADR-0001 challenges with Bearer so protected calls fail as `401` and the SPA
  drives the interactive login. The handshake cookies remain at the library's defaults — that response
  genuinely arrives cross-site. Decided 2026-07-30 with #182.
- **DEC-061 — A conversation's container stays warm while the conversation lives** *(revises
  [ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md)'s "nothing idles")*: a
  portal conversation (#166) runs in an on-demand Container Apps **session**, one per conversation,
  addressed by the conversation's id. It starts on the first message, keeps its cloned workspace, and
  the platform reclaims it after ten minutes of inactivity. Rationale: ADR-0008 chose a pass per
  message on the argument that nothing should idle between them — true of the *model*, but taken
  literally about the *container* it means a cold start on every reply. Ten seconds per answer is the
  difference between a conversation and a ticket queue, and the workspace would be re-cloned each
  time. What is accepted is bounded idling, by the pool's own cooldown, with no ready instances held
  in reserve — nobody talking costs nothing. What is preserved is the property the shape was chosen
  for: one conversation is one container is one project's PAT, so the isolation boundary coincides
  with the credential boundary (DEC-030) and the portal never holds a project credential. Decided
  2026-07-31 with #166.
- **DEC-062 — The catalogue is one action: the repository's prompt, unbounded** *(revises DEC-026's
  four-action catalogue, DEC-048's licensed growth of it, and #150's "what it can do is decided
  here")*: every built-in action is removed and an Automation runs the prompt its project's
  repository names, with the project PAT and a cloned workspace, exactly as the repository's own
  `/aio:*` commands run locally. The orchestrator keeps what makes it a product — triggers and
  matching (BR-003), one-active-Run and the cap (BR-001/BR-002), dispatch, timeouts (BR-005), the
  live log (UC-027), usage (BR-011), terminal states (BR-004) — and performs **no vendor or
  repository write of its own**, with one stated carve-out: output labels are still applied on
  success, because the workflow's wiring is machinery like matching, not action ceremony.
  **Costs accepted and stated:** (1) BR-007's approval gate is a workflow control now, not a
  containment control — "a plan phase publishes nothing" and "a cancelled Run produces no PR" are
  prompt-level promises until the **grants model** (the named follow-up) lands; (2) the
  conversational wait is dormant — the grill's question path was `AwaitingInput`'s only producer,
  and the state, its machinery and the inbox category are kept unreached rather than removed,
  because Run states were out of scope; (3) no Run carries an output link — only the retired
  publish step ever set one, and the agent reporting what it did belongs with grants. Naming a
  prompt is **required at save**: with one action, an Automation that names none could never run.
  Decided 2026-08-01 with #162.

- **DEC-063 — One session always idles, because the platform allows nothing less** *(revises
  [DEC-061](#)'s "no ready instances held in reserve — nobody talking costs nothing")*: the
  conversation session pool holds `readySessionInstances = 1` at 1 vCPU and 2 GiB, continuously.
  Rationale: DEC-061 chose zero on a cost argument and Azure refuses the value —
  `SessionPoolInvalidReadySessionInstances`, *"supported values should be greater than 0 and smaller
  than maxConcurrentSessions"*. This is not a preference that was reconsidered; it is a constraint
  that was discovered, at apply, after the provider's own schema had accepted the zero. The
  alternatives were a smaller always-on container or no pool at all, and the second was refused on
  the same ground DEC-030 rests on: without a pool the deployed portal composes the in-process
  runtime and resolves a project's PAT inside the portal. **What is accepted:** a standing cost in
  dev for a container nobody is talking to, at the full size, because an agent cloning a repository
  and running a model is not a small workload and a warm session that then thrashes on memory would
  buy nothing. **What it buys back:** the first conversation of the day answers without a cold
  start — the one thing zero ever gave away. Decided 2026-08-01 with #198.

- **DEC-053 — a Connector asks for the permissions its configuration will use** *(revises
  DEC-030's one-PAT-covering-everything)*: DEC-030 gave each project a single token covering
  backlog read/write and code clone/push/PR, and said finer scoping was post-MVP. Two later
  changes made that breadth costly rather than simple. A LocalFolder project (#210) never uses
  the code half — its working copy is the host's own and git runs with the host's credentials —
  so those scopes are granted and unused. And since #162 the agent holds the token and runs
  unbounded, which makes what the token *can* do the containment that remains. Breadth therefore
  follows configuration: one capability set, computed once, used both to say what to grant and to
  decide what to verify, so the two cannot drift. **An unverifiable capability is reported, never
  assumed:** verification still writes nothing in any habitat, so a write is checked by asking the
  vendor what the credential may do — GitHub answers from the repository's permission grant, and
  Azure DevOps declares that it has no such call rather than passing a check nobody performed
  (ADR-0005). Not-verifiable does not block a save; a pass nobody earned would be worse than a
  stated gap.

- **DEC-054 — the dispatch substrate follows the habitat, and the local one gives up the
  worker/portal separation** *(extended DEC-013; **absorbed 2026-08-09** when DEC-013 was
  superseded — there is no longer a habitat that dispatches any other way, so "follows the
  habitat" has one branch and the outbox is simply the substrate)*: DEC-013 chose a Storage Queue
  with KEDA, which is right where there is something to scale. On a machine one person owns there
  is no KEDA,
  nothing to scale to zero and no second consumer, so the queue costs a container and buys
  nothing. A queueless habitat therefore dispatches through the **same Postgres outbox integration
  events already use** — durability from the outbox, not the transport, so BR-004's crash story
  holds across a process death. Chosen by configuration presence and refusing ambiguity at startup
  (ADR-0010), so the deployed habitat structurally cannot acquire the in-process consumer.
  **The trade, stated:** CAP's in-memory transport requires publisher and consumer in one process,
  so locally the portal resolves project PATs and clones repositories — the separation
  `infra/dev/dispatch.tf` exists to keep. This is the same trade #166's design D2 already
  made for the in-process conversation runtime: correct on a machine one person owns, not a
  degraded mode, and local only. **Second consequence:** CAP has no SQLite storage provider, so
  this couples the local habitat to Postgres and constrains any future embedded database.

- **DEC-064 — the product may seed a process document it does not find, and what it writes is the
  project's own** *(revises DEC-048)*: DEC-048 settled that the grill's rubric "is always the project's
  own document, read live, because a product-wide readiness bar would impose one team's standards on
  every repository it touches", and `automation-configuration` restated it as "the copy is the weaker of
  the two". Both were written about a product that only ever **read** documents. #269 makes the
  spec-first workflow installable, and its prompts read a definition of ready, a retro log and an
  OpenSpec layout — so installing the prompts alone ships a workflow whose first Run fails on a missing
  file, which is the abandoned-prerequisite failure the tiering exists to prevent, moved one step later.
  **The revision is narrow: "the weaker of the two" presumes two.** Where a team has its own document it
  still wins — a prerequisite whose path exists is not written, not modified and absent from the pull
  request, decided against the clone the branch is cut from. Where a team has none there is no second
  term, and the alternative on offer is not their own rubric but a broken workflow. DEC-048's
  **read-time** invariant is untouched: `GrillToReady` still reads the project's document live at a
  configured path, never a bundled copy. What changes is only that the product may put a first version
  there, after which it is at the project's own path, in their repository, theirs to edit or delete.
  Gated by a consent that is off by default and names every path it will write ([ADR-0012](../../adr/0012-a-seeded-document-is-the-projects-own.md)).
  **The cost, stated:** on day one every repository that presses the button holds the same readiness
  bar. Bounded, not dismissed — it is theirs from the moment it lands. The seed carries the rubric and
  the shaping rules it must cite, ships `openspec/config.yaml` with its project-context section an
  explicit TODO because context cannot be inherited, and **never** carries this product's own corpus
  (`ACT-*`, `UC-*`, `BR-*`, `DEC-*`): that is AI Orchestrator's identity, not a template.

- **DEC-065 — a developer's own machine may hold an agent session; a deployment may not**
  *(supersedes [DEC-055](#) / [ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md)
  in self-host only, and closes OPN-007)*: in the self-host habitat a human MAY attach to a Run's
  sandbox beside the headless agent **and** MAY attach to the agent's own process, bounded by the
  machine's inactivity in the shape DEC-061 established — never by a deadline on the person (BR-006).
  In a deployment neither is permitted and DEC-055 stands: a conversation costs a pass per message.
  Rationale: ADR-0008 refused a live session on three premises, and two moved without this change —
  DEC-013 was superseded with its substrate (#296), and "nothing idles" was already revised by DEC-061
  and DEC-063, which now pays for one continuously idle session at 1 vCPU and 2 GiB. What is left is
  BR-006, and BR-006 is satisfied by an inactivity bound. **The habitat is the whole of the
  difference:** a self-host Run runs on hardware its operator owns, where a held sandbox spends their
  own 4 GiB; a deployed Run runs on metered infrastructure where the same affordance turns an untimed
  human wait into unbounded spend someone else pays. **The cost, stated:** the same Automation now
  produces different records in different habitats — a locally attached agent yields a terminal byte
  stream where a deployed Run yields the structured transcript of #299/#300, and `transcript.ts`
  renders the former as `raw` lines. Accepted knowingly, against the analysis's own recommendation
  (permit the beside-the-agent shell everywhere, the into-the-agent session nowhere), which is
  preserved in the change's `evidence.md`. BR-005 needs a stated rule before the into-the-agent form is
  implementable: a timeout that bounds unattended work cannot also bound work a human is typing into.
  Decided 2026-08-10 with #301 ([ADR-0021](../../adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)).
