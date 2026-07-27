# AI Orchestrator

An internal web application that connects project backlogs (GitHub, Azure DevOps) to AI agents:
configure **Automations** that fire KEDA-scaled **Agent** jobs (Azure Container Apps) to act on
user stories — implementing them as PRs, refining, transitioning or estimating them — with every
run visible and governable from the website.

## Quick start

Requires the .NET 10 SDK, Node 22 + pnpm, Docker, and the [Aspire CLI](https://aka.ms/aspire/cli).

```bash
cd src/frontend && pnpm install && cd ../..
aspire run --apphost src/root/AiOrchestrator.AppHost
```

That is the whole inner loop: PostgreSQL, Azurite, migrations, the API host, the Vite dev
server **and the dispatch worker**, served same-origin through the host. Git hooks install
themselves on the first `dotnet build`.

It seeds a **Demo project** with an Automation on opencode's free model, so the loop is
clickable immediately and costs nothing to run — no AI credential is needed.

To point that project at a repository you control, add its coordinates and a PAT before the
first run:

```bash
cd src/root/AiOrchestrator.Server
dotnet user-secrets set "LocalLoop:Repository" "your-org/your-repo"
dotnet user-secrets set "local-github-pat" "<a PAT with repo scope>"
```

Then label a Story `ai:implement` in the portal and watch a Run appear, execute, and open a
pull request.

**What the local loop proves, and what it does not.** It exercises the real queue contract,
matching, agent execution and pull-request publication — the same code that runs deployed.
It does **not** exercise KEDA (Aspire restarts the worker on a timer; KEDA scales on queue
length) or Key Vault (locally, secrets come from user secrets through the same resolver
interface). Those two have exactly one proof, and it is in Azure.

### Webhooks (optional)

Polling is the baseline and always runs. To also trigger within seconds of a change, add a
webhook at the vendor pointing at `POST /api/webhooks/github`, choose a secret, store it under
a name your `ISecretResolver` can reach, and put that **name** on the Connector. A webhook that
never arrives, or is refused, costs latency only — the next poll reconciles regardless.

### Everything else

```bash
dotnet build src/AiOrchestrator.slnx                              # warnings are errors
dotnet test src/AiOrchestrator.slnx --filter "Category!=E2E"      # unit + functional + arch
dotnet test src/tests/AiOrchestrator.EndToEndTests                # boots the real app, drives a browser
dotnet csharpier format src                                       # C# formatting
cd src/frontend && pnpm lint && pnpm typecheck && pnpm build      # frontend gates
```

Functional and E2E tests use real containers — Docker must be running. Behind a registry mirror,
point Testcontainers at it with `TESTCONTAINERS_HUB_IMAGE_NAME_PREFIX` rather than editing image
names in the fixtures.

## Where things live

| You want | Go to |
|---|---|
| What the product is and must do | [docs/product/mvp/](docs/product/mvp/00-product-brief.md) — stable IDs (ACT/BC/UC/BR/DEC/OPN) |
| How the code is arranged and why | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Current behaviour, as specs | `openspec/specs/` |
| Work in flight | `openspec/changes/` |
| Bootstrap state and locked decisions | [BOOTSTRAP.md](BOOTSTRAP.md) · [BOOTSTRAP-CHECKLIST.md](BOOTSTRAP-CHECKLIST.md) |

## How work happens

Spec-first: an idea is grilled to a Definition of Ready, proposed as a reviewable spec on a draft
PR **before any code**, implemented on that same PR, then squash-merged with its spec archived and
a retro entry appended. Hotfixes and pure infra changes may use the lighter spec-less lane
(DEC-025). The ceremonies and their commands land in bootstrap Phases 2–3.
