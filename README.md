# AI Orchestrator

An internal web application that connects project backlogs (GitHub, Azure DevOps) to AI agents:
configure **Automations** that fire KEDA-scaled **Agent** jobs (Azure Container Apps) to act on
user stories — implementing them as PRs, refining, transitioning or estimating them — with every
run visible and governable from the website.

## Quick start

Requires the .NET 10 SDK, Node 22 + pnpm, Docker, and the [Aspire CLI](https://aka.ms/aspire/cli).

```bash
cd src/frontend && pnpm install && cd ..
aspire run --project root/AiOrchestrator.AppHost
```

That is the whole inner loop: it starts PostgreSQL, Azurite, the API host, and the Vite dev
server, and serves the app same-origin through the host. Git hooks install themselves on the
first `dotnet build`.

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
