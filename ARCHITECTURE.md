# Architecture

Bird's-eye map. Behaviour lives in `openspec/specs/`; product truth in
[docs/product/mvp/](docs/product/mvp/00-product-brief.md); decisions in `docs/adr/` and the
[locked-decision log](docs/product/mvp/10-locked-mvp-decisions.md). This file links, it does not
restate.

## Shape

```
src/
├── AiOrchestrator.slnx  Directory.Build.props  Directory.Packages.props  global.json  .editorconfig
├── shared/
│   ├── AiOrchestrator.BuildingBlocks/        # Modules, CQS, Domain, Api — primitives only
│   ├── AiOrchestrator.ServiceDefaults/       # OpenTelemetry, health, service discovery, resilience
│   └── AiOrchestrator.ArchitectureAnalyzers/ # MOD001–005 + CQS001, auto-attached to every module
├── modules/
│   ├── Projects/AiOrchestrator.Modules.Projects/   # the reference module (BC-001)
│   └── Backlog/AiOrchestrator.Modules.Backlog/     # Connector + mirrored Stories (BC-002)
├── root/
│   ├── AiOrchestrator.Server/                # BFF host: module composition + SPA same-origin
│   ├── AiOrchestrator.MigrationService/      # the migration step; Server waits on its completion
│   └── AiOrchestrator.AppHost/               # Aspire: Postgres + Azurite + migrations + host + Vite
├── frontend/                                 # Vite + React SPA, standalone pnpm project
└── tests/
    ├── AiOrchestrator.ArchTests/             # runtime complement to the analyzers
    ├── AiOrchestrator.SharedFunctionalTests/ # Testcontainers + Respawn fixture base
    ├── AiOrchestrator.EndToEndTests/         # real AppHost + Playwright
    └── modules/{Projects,Backlog}/{...UnitTests, ...FunctionalTests}
```

Outside `src/`: `infra/` holds the Terraform for the Azure dev environment and the
bootstrap/deploy scripts.

`src/` is the solution root; the repo root holds only cross-cutting tooling and docs.

## Backend

A modular monolith with enforced seams. The host discovers `AiOrchestrator.Modules.*.dll` at
startup ([ModuleRegistration](src/shared/AiOrchestrator.BuildingBlocks/Modules/ModuleRegistration.cs)),
so adding a module needs no host edit. Each module owns a PostgreSQL schema, its migrations, and
its feature slices.

**The Server never migrates — in any environment.** Migrations are a separate resource in the
AppHost graph (`AiOrchestrator.MigrationService`, which runs every module's `IModule.Migrate`
and exits); the Server starts only after it completes. The in-process predecessor was gated on
`!IsProduction()` and under `aspire run` the environment silently defaulted to Production —
fresh database, no schema. In production the same executable runs as a deliberate deploy step
(#8); the functional-test fixture calls the same `MigrateModules` itself. Backlog was the first module to test that claim rather than assert it: adding
it changed the solution file and nothing in `Program.cs`.

There are two modules, and the boundary between them cost something worth knowing about. The
Connector is configuration of a Project but lives in **Backlog**, because everything that reads
or writes one — verification, polling, failure recording — is a Backlog concern. The price is
that `Connector.ProjectId` carries no foreign key, since a cross-schema constraint is the
coupling the boundary exists to prevent. [The Backlog context](src/modules/Backlog/context.md)
records the reasoning and the deletion debt that follows from it.

A use case is **one file**: route + request/response + command + validator + handler, nested and
`internal` — see the exemplar,
[CreateProject.cs](src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Projects/UseCases/CreateProject.cs).
Requests travel a fixed decorator pipeline owned solely by
[`AddVsaCqsArchitecture()`](src/shared/AiOrchestrator.BuildingBlocks/CQS/AddVsaCqsArchitecture.cs):

```
Logging → Authorization → Validation → Caching → Handler → InvalidateCaching
```

Authorization sits there and not elsewhere (#13, BR-009). Each request declares the **permission** it
requires — `[Requires(BacklogPermissions.Configure)]` — and **an undeclared request is refused**, so a
use case added without thought is locked rather than open. It is outside validation, so a caller with no
role learns nothing about the payload's shape, and therefore outside caching, so an answer cached for
somebody allowed cannot reach somebody who is not.

A request names a permission, never a role. Roles are **bundles** over permissions
([`PermissionGrants`](src/shared/AiOrchestrator.BuildingBlocks/Identity/PermissionGrants.cs)), each
module contributing its own mapping beside the use cases that declare them — so DEC-034's post-MVP
custom roles are a line in a table rather than a sweep over every declaration. Admin holds everything by
rule, not by enumeration. Which bundle a caller holds is a function of the caller **and** the project:
`ICurrentPrincipal` says who,
[`IProjectPermissions`](src/shared/AiOrchestrator.BuildingBlocks/Identity/IProjectPermissions.cs) says
which bundle, there.

Three ArchTest sweeps police it: every request declares, every declared permission is one of the
modules' constants, and every constant is declared by something. The last two are the compile error the
strings gave up — a typo'd permission is held by nobody, so it would be refused for Member, allowed for
Admin, and silent.

A surface that dispatches nothing cannot be covered by a decorator over dispatch: the run-log hub checks
the same permission for itself, and anything like it must do the same.

Two error channels, deliberately distinct:

- **Domain errors** are values: handlers return `ErrorOr<T>`, endpoints map them through
  [`ApiResults.Problem`](src/shared/AiOrchestrator.BuildingBlocks/Api/ApiResults.cs).
- **Input-validation failures** short-circuit the pipeline as an exception, rendered by the one
  [`GlobalExceptionHandler`](src/shared/AiOrchestrator.BuildingBlocks/Api/GlobalExceptionHandler.cs).

Both emit RFC 7807 `application/problem+json`. Nothing else writes an error body.

## Deployment

The dev environment is Terraform in `infra/dev/` (northeurope, `aio-dev-*`): resource group, Log
Analytics, a Container Apps environment, ACR, PostgreSQL Flexible Server, Key Vault, the portal
container app, and the migration job. `infra/bootstrap.sh` creates the remote-state backend once;
`infra/deploy.sh` performs a release.

**Who applies what.** Humans apply Terraform and run deploys with their own Azure identity. CI
validates (`fmt`, `validate`, `shellcheck`) and holds no credentials — a federated CI identity is
a later, deliberate decision, not a default. The configuration refuses the wrong target: a guard
compares the resolved subscription against a committed SHA-256 and fails at plan time, which is
how the subscription id stays out of a public repository while still being checked.

**Credentials flow one way.** Terraform generates the database password *into* Key Vault. Both
the app and the migration job carry a system-assigned identity with read-only vault access and
pull-only registry access; their configuration holds a vault URI and nothing secret. The
application resolves names through `ISecretResolver` at the moment of use — the same seam that
reads user-secrets locally (BR-010).

**Release ordering.** `deploy.sh` pushes images, runs the migration job, waits for exit 0, and
only then moves the app revision. A failed migration leaves the previous revision serving. The
Server never migrates, in any environment — locally the AppHost's `migrations` resource does it,
in Azure the job does.

**Who holds the credential.** Pull-request validation holds none and asks for none, which is what
makes a fork's PR harmless. Deployment holds a federated OIDC credential scoped to the `dev`
Environment, and GitHub will not mint a token for it until a reviewer approves the run — with the
Terraform plan already printed in the summary (DEC-046). The approval is the same human decision
the earlier terminal-only posture protected; moving it into the browser is what stopped it from
being a single point of failure attached to one laptop.

**The Connector seam writes exactly one thing: labels.** UC-008 licenses the portal to apply
or remove a trigger label; the write goes to the vendor first and the mirror follows through
the ordinary reconciliation, so a `StoryChanged` produced by a portal click is
indistinguishable from one produced at the vendor (DEC-027). Everything else about the mirror
stays read-only (BR-008).

**The seam's one repository-level write.** Every other Connector method names a Story;
`EnsureLabel` names only the repository. It exists because a trigger label nobody has applied yet
does not exist at the vendor, so a Member cannot choose it there — the product could apply labels
long before it could create one. GitHub creates-or-succeeds. **Azure DevOps returns success
without acting**, because its tags are not repository objects: they come into being when applied
to a work item, and the only way to fake one would be to tag somebody's backlog item to satisfy
our own bookkeeping. The caller reports that labels were not ensured, so the asymmetry reaches
the Admin rather than being swallowed.

**Two vendors, one seam — and only one of them proven.** Azure DevOps implements every
`IBacklogConnector` method beside GitHub (DEC-045): a work item is a Story, `System.Tags` is the
label set, and the two Connector coordinates are the organisation and the project. Because code
lives in a repository *inside* an Azure DevOps project rather than being the project, a Connector
may name a code repository separately; GitHub leaves it empty. Where a concept is
process-dependent — the state vocabulary, the estimate field, which differ between Agile, Scrum
and Basic — the connector attempts what was asked and surfaces the vendor's refusal rather than
assuming a mapping.

That implementation is **unexercised**: it has never run against a real Azure DevOps
organisation, because no organisation is available to this project. What is verified is the
translation (unit-tested in both directions) and the containment (the guardrail suite passes with
two vendor implementations present, which is the property the seam existed to provide). The REST
calls themselves are a stated hypothesis, labelled as one in the class and in the portal's vendor
picker, per ADR-0005. Treat the first real connection as a test, not a deployment.

**A webhook is a reason to look, not data to trust.** UC-010's endpoint verifies the vendor's
signature (constant-time, mandatory — it is unauthenticated and triggers work) and then runs
the *same* reconciliation the poller runs. Nothing is read from the payload but the repository
it names. That is how BR-015's "webhook and polling events are identical" holds structurally
rather than by two code paths promising to agree, and every refusal answers alike so an
unauthenticated caller learns nothing about which repositories exist. Polling continues, so a
missed webhook costs latency and never correctness.

**A Run could wait for a human and resume (#78) — the machinery stands, dormant (#162).** The
shape was the approval gate's, generalised: an agent pass ending with questions posted them on the
Story signed with a run marker, the Run entered `AwaitingInput`, and a checker resumed it when an
unsigned newer comment arrived. The grill's question path was that state's only producer, and it
left with the catalogue — so today nothing reaches `AwaitingInput`, nothing enters the inbox's
waiting-for-input category, and `ConversationGate`, `ResumeChecker` and `RunMarker` have no caller.
They are kept rather than removed because #162 put Run states out of scope, and this paragraph is
the stated dormancy rather than a grep surprise. A prompt can ask a question by commenting; it
cannot pause its own Run.

The retired ceremonies — the grill's readiness interrogation, propose's documentation PR, the
one-click defaults that seeded the pipeline — are all realisable as prompts, which is the point of
DEC-062: they were this product doing an agent's job, and the sibling ds-connect repository runs
the same workflow entirely from prompt files today.

**One session idles, and not by choice (DEC-063).** DEC-061 accepted bounded idling with no ready
instances — nobody talking costs nothing. Azure refuses `readySessionInstances = 0`, so the pool
holds one warm container continuously. The provider's schema accepted the zero and the service did
not, which is the general shape of this whole seam: `azapi` is an escape hatch, and its embedded
schema is not the API.

**A session may be held on your own machine, and nowhere else (DEC-065, ADR-0021).** In self-host a
human may attach to a Run's sandbox beside the headless agent, or to the agent's own process; both are
bounded by the machine's inactivity, never by a clock on the person (BR-006). In a deployment neither
is permitted and DEC-055 stands — a conversation costs a pass per message. ADR-0008's cost argument
was always a deployment argument: a sandbox held on hardware its operator owns spends their own 4 GiB,
while the same affordance on metered infrastructure turns an untimed human wait into spend somebody
else pays. **The asymmetry has a price the seam must carry:** an agent attached to locally emits a
terminal byte stream, not the structured output `transcript.ts` reads, so the same Automation's Run
reads as `raw` lines in one habitat and as steps in the other.

**A prompt can be tried before it is committed (#189), and the scratchpad is a conversation.** With
the catalogue at one action, writing a prompt is the configuration activity — so the portal runs
supplied text once against the project's repository and shows the reply and the cost. It stores
nothing: the repository stays the only place a prompt lives, and the Run path never consults it.
Each attempt starts a *fresh* conversation, because reusing one would hand the agent the previous
draft and its own reply, and a trial contaminated by the draft it replaced predicts nothing.

**A project with no prompts is offered a starter set (#190), and the product writes none of it.** The
catalogue is versioned markdown in this repository, embedded so the bytes a test loads are the bytes
the endpoint serves, and shown against a project: where each file would go, resolved by the same
`PromptPath` a Run uses, and which ones that project already has. Nothing scaffolds, commits or opens
a pull request — this is the first change since #162 that could have reintroduced an orchestrator
repository write, and it declines to for the same reason. Where there is no Connector, presence reads
**unknown** rather than absent, because nothing looked.

The set is tiered by prerequisite, and the tiering is the decision rather than presentation: a
portable tier that names no document outside the project's own repository, and the spec-first
workflow this product runs on, offered as a bundle that states what it needs. Measured against a
fresh repository, five of the six workflow prompts read documents that will not be there — presented
as one list, somebody takes a `sync` prompt into a project with no `openspec/` and learns the
prerequisite from an agent's confusion.

That fidelity is why **a Story is described to an agent in exactly one way**, by a single helper both
`RunExecutor` and the conversation use: number, title, state, labels, and a description bounded at
8,000 characters. Before #189 the two disagreed — a conversation sent title and body, unbounded —
so a prompt tried in one and run by the other was tried against a different input, and state and
labels are precisely what a real prompt branches on. Two differences remain and are stated in the
surface's own copy rather than left to be discovered: a trial has no approval-gate planning phase,
and no per-Automation timeout.

## Dispatch

A Run reaches an Agent through an Azure Storage Queue (DEC-013) that KEDA watches: a message
arrives, a Container Apps Job starts, it drains the queue and exits. `IRunDispatcher` is the
seam; the queue implementation sits in ServiceDefaults beside the Key Vault resolver, so no
module reaches a cloud SDK.

**The message is a Run id and nothing else.** The worker reads the Run, its Story and its
Automation from Postgres — one source of truth, and nothing to go stale between enqueue and
execution.

**A claimed message is deleted before any work happens, and that is load-bearing.** Storage
Queues are at-least-once: a consumer that dies leaves its message to reappear, and KEDA starts
another job — an automatic retry, which [BR-004](docs/product/mvp/05-business-rules.md) forbids.
The rule wins. The cost is stated rather than hidden: a job killed by infrastructure is
indistinguishable from an Agent that failed, and both need a human to re-trigger via *Run now*
(BR-013). **Do not "fix" this back to at-least-once** — the deletion is the rule.

Agent jobs run under a **different identity from the portal's**: they will clone repositories
with project PATs, and one compromise should not reach both.

**What the local path does and does not prove.** Azurite exercises the full enqueue → claim →
delete contract on every machine and in the functional tier. KEDA has no local equivalent, so
the scale rule is only ever verified in Azure — a green local run says nothing about it.

## Integration events

Modules never call each other. A module announces a fact — `StoryChanged` — through
`IIntegrationEventPublisher`, and other modules react through `IIntegrationEventHandler<T>`.
Both interfaces live in BuildingBlocks and speak product vocabulary only; the implementation
(DotNetCore.CAP: Postgres outbox + in-memory transport) sits in ServiceDefaults, exactly like
the Key Vault resolver and the dispatch queue. No module references CAP, directly or
transitively — the ArchTests pin it.

**Publish is transactional, and that is the entire point.** The publisher's `BeginTransaction`
spans the module's own writes and its staged events: a Story change and its `StoryChanged`
event commit or roll back together, so a consumer never reacts to a write that didn't happen
and a write never goes unannounced. The functional suite asserts the rollback case against the
outbox itself.

**Delivery is at-least-once; every handler must be idempotent.** A process that dies mid-handle
redelivers after restart (observed in the change's spike, not assumed). Retries are a
deliberate small ceiling (3), and an exhausted message is dead — loudly logged, never silently
dropped. Automatic *re-running of Runs* is still forbidden (BR-004); retrying an event handler
is not a Run retry.

**Events carry identity, never state.** `StoryChanged` says *which* Story changed and *how*
(added/updated/removed) — a consumer reads current truth through the owning module's
`.Contracts` assembly rather than trusting a payload that may be stale by the time it arrives.
The wire name is versioned (`backlog.story-changed.v1`); an unrecognised name is dropped
explicitly rather than misread.

**`.Contracts` assemblies are the only cross-module surface.** They hold events, enums, and
read interfaces — no implementation types (the ArchTests verify both directions). Module
discovery skips them; the owning module registers any implementations itself. The `cap` schema
is created by the MigrationService like every other schema — the Server migrates nothing.

## Runs and matching

The Runs module (BC-003) is the first consumer of the event stream: `StoryChanged` arrives, the
handler reads the Story's **current** labels and state through `IStoryReader`
(Backlog.Contracts) and the Project's enabled Automations through `IAutomationCatalog`
(Projects.Contracts), and a match becomes a Run plus a dispatch message. The event is only a
pointer — matching never trusts a payload (BR-015), so a superseded change matches the newer
truth, which is what BR-008 wants.

**BR-001 is a partial unique index, not a handler convention.** One Run per Story reference
across the active states; a second match while a Run is active is *ignored, not queued*, and a
concurrent duplicate delivery loses the insert and reports success — idempotency comes from the
constraint, not a message ledger. BR-002 holds at creation: at the cap the Run waits `Queued`
and nothing is enqueued.

**Run now shares the exact creation path.** UC-012 bypasses detection only (BR-013): the
endpoint validates through the same Contracts reads and creates through the same `RunCreator`
as matching — the difference is voice, not rules. Where the event handler is correctly silent
(a duplicate delivery, an active Run), the endpoint answers the human: a 409 naming BR-001, a
stated two-phase limitation, a "waiting at the cap" note.

**Both lanes are real.** `requiresApproval = false` goes straight to execution;
`requiresApproval = true` produces a Plan, pauses at `AwaitingApproval` publishing nothing, and
waits for a human — untimed (BR-006) and holding no concurrency slot (BR-002), though the Story
stays held (BR-001). Approval stamps the Run and re-enqueues it, and phase 2 runs with the
approved Plan in its instruction; rejection ends the Run `Cancelled`. The routing lives in the
Run's own record, not in a fifth state.

**Cancellation is cooperative, and the gap is stated.** Cancelling writes `Cancelled`
immediately — terminal, so the Story frees and the UI stops implying work — and the worker
checks at two boundaries: before invoking the runtime, and immediately before publishing. A Run
cancelled mid-agent therefore produces no branch and no pull request, and its outcome cannot
overwrite the cancellation. What it does **not** do is kill an Agent already running: the
portal holds no handle on a KEDA-started job, and a control-plane kill would need
management-plane credentials in the portal identity plus an Azure-only path with no local
equivalent. The invocation finishes (bounded by BR-005) and its work is discarded.

**Stated limitations, on purpose.** Nothing promotes
a `Queued` Run when capacity frees, because nothing can complete yet. And the Run insert and the
queue enqueue cannot share a transaction: the Run commits first, so a crash between the two
leaves a visible `Queued` Run with no message — logged loudly. *Run now* does **not** recover it:
BR-001 holds the Story, so a second dispatch answers `AlreadyActive`. The sweeper (#140) ends the
Run once its phase deadline plus grace has passed, which frees the Story, and a human re-triggers
from there (BR-013). Nothing re-runs automatically (BR-004)
when it lands.

## Agent execution

`IAgentRuntime` (BuildingBlocks) is the job contract: instruction in — prompt, action,
timeout, workspace, in-memory credentials — result out: log, optional output link, optional
usage. Claude Code headless implements it first (DEC-012), version-pinned in the worker image;
the CLI is confined to its implementation file the way Octokit is confined to the GitHub
connector.

**The Automation's runtime picks the implementation.** `IAgentRuntimeSelector` maps the
runtime name to its implementation and its credential's name — which may be absent: opencode's
default model is a free one (`opencode/deepseek-v4-flash-free`, DEC-044) and a free-model Run
performs no vault lookup at all. Adding a runtime is a composition registration, never an
executor edit.

**Nothing secret travels.** The queue message is still only a Run id. The worker — a full host
composing the modules — loads the Run, Story and Automation through Contracts, resolves the
project PAT and the AI credential **by name** at execution time (BR-010, DEC-014, DEC-030),
and the values live exactly as long as the child process does.

**Usage is honest.** The runtime reports tokens and cost when its output carries them
(BR-011/DEC-038); anything missing or unparseable reads as "unknown" on a Run that otherwise
succeeded. The result-JSON shape is verified by exercising, and the parser is defensive so a
shape surprise degrades to honesty, not to failed Runs.

**The catalogue is one action, and the prompt decides what happens (#162, DEC-062).** An
Automation runs the prompt its repository names: the executor clones the project's repository with
the project PAT, resolves the prompt from the prompts directory read live (#150 — a missing file
fails naming the **resolved** path), hands the agent the prompt plus the story, and records what
came back. **Nothing is published afterwards** — no PR opened, no comment posted, no state written,
no estimate parsed. The agent held the same credential and did those itself, or they did not
happen. The single carve-out is the workflow's own wiring: output labels are still applied on
success, because that is machinery like matching, not action ceremony.

Two promises degraded with the ceremonies, and the decision says so rather than the code
pretending: "a plan phase publishes nothing" and "a cancelled Run produces no PR" are prompt-level
promises until the grants model lands. The approval gate still routes two phases and still refuses
to run phase two unapproved — it decides whether a human sees the plan, and no longer guarantees
the work has not already happened. The grill's conversational wait is dormant: nothing reaches
`AwaitingInput`, and its machinery is kept rather than removed because Run states were out of
scope. No Run carries an output link — only the retired publish step ever set one.

**Terminal states exist now.** Queued → Executing at claim; Succeeded/Failed with timestamps
at the end — and a crash mid-execution still ends the Run, because nothing redelivers
(BR-004). BR-001's index filters on active states only, so a finished Story runs again; a
Failed Run is re-run by a human, never automatically.

## Frontend

One React SPA (Vite, React Router, TanStack Query) served **same-origin** by the host: proxied to
the Vite dev server in development, served from `wwwroot` with an `index.html` fallback everywhere
else. Consequently there is no CORS configuration and no API base URL — calls are relative paths.
Reserved prefixes (`/api`, `/openapi`, `/scalar`, `/health`) are matched by routing before either
fallback branch.

Feature code is co-located in `features/<feature>/`; `app/` holds thin routes; only cross-cutting
plumbing lives in `shared/`. All user-facing copy comes from the typed catalog in
`shared/i18n/` — hardcoded JSX copy fails lint.

## Guardrails

| Where | What |
|---|---|
| Compile | MOD001–005, CQS001 (Error severity), `TreatWarningsAsErrors`, Roslynator, `.editorconfig` style rules |
| Test | ArchTests: cross-module assembly refs, no controllers, `I`-prefix, unique `LoggerMessage` IDs, `Subject_Should_Constraint` naming |
| Commit | Husky (self-installing): CSharpier + lint-staged; commitlint |
| CI | Same gates mirrored, so `--no-verify` is still caught; plus build/test, E2E, spec validation |

MOD002 and the ArchTest assembly check are **complementary, not redundant**: the analyzer catches
another module's type in a member *signature*; the ArchTest catches the assembly reference however
it arises, including use only inside a method body. Both are needed — verified with probes.

## What is deliberately absent

Terraform and the deploy lane (their own change), authentication (blocked by
[OPN-002](docs/product/mvp/07-open-decisions.md)), the design system (bootstrap Phase 4), and every
product capability beyond the exemplar slice: Connectors, Automations, dispatch, Agent execution.
Those arrive one reviewed change at a time.
