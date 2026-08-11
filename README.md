# AI Orchestrator

An internal web application that connects project backlogs (GitHub, Azure DevOps) to AI agents:
configure **Automations** that run an **Agent** in a sandbox of its own — a microVM created for
one Run and gone with it — to act on user stories, implementing them as PRs, refining,
transitioning or estimating them, with every run visible and governable from the website.

## Quick start

Requires the .NET 10 SDK, Node 22 + pnpm, Docker, and the [Aspire CLI](https://aka.ms/aspire/cli).

```bash
cd src/frontend && pnpm install && cd ../..
aspire run --apphost src/root/AiOrchestrator.AppHost
```

That is the whole inner loop: PostgreSQL, migrations, the API host and the Vite dev server,
served same-origin through the host. Git hooks install themselves on the first `dotnet build`.

The dev loop runs the Agent in an `sbx` sandbox by default. `sbx` refuses to create its
**first** sandbox on a machine until its global network policy exists —
every Run fails with `global network policy has not been initialized` until you run, once per
machine:

```bash
sbx policy init balanced
sbx policy allow network opencode.ai
```

(`balanced` is `sbx`'s own recommended default: typical dev traffic — AI services, package
registries — is allowed, and the app's own per-sandbox rules still layer on top. `opencode.ai`
is not in `balanced`'s default allowlist, but the seeded Demo project's Automation runs on
opencode's free model through it — without the second line, that Run fails with
`Blocked by network policy: domain opencode.ai:443`.)

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

**What the local loop proves, and what it does not.** It exercises the real dispatch path,
matching, agent execution and pull-request publication — the same code that runs deployed,
because dispatch is the Postgres outbox in every habitat since #296.
It does **not** exercise the deployed sandbox substrate (locally the Agent runs in `sbx`, or as
a child of this process; deployed it runs in an Azure Container Apps sandbox) or Key Vault
(locally, secrets come from user secrets through the same resolver interface). Those two have
exactly one proof, and it is in Azure.

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

| You want                                   | Go to                                                                                         |
| ------------------------------------------ | --------------------------------------------------------------------------------------------- |
| What the screens are and how they are used | [docs/product/manual/](docs/product/manual/README.md) — the tour, with screenshots            |
| What the product is and must do            | [docs/product/v1/](docs/product/v1/00-product-brief.md) — stable IDs (ACT/BC/UC/BR/DEC/OPN) |
| How the code is arranged and why           | [ARCHITECTURE.md](ARCHITECTURE.md)                                                            |
| Current behaviour, as specs                | `openspec/specs/`                                                                             |
| Work in flight                             | `openspec/changes/`                                                                           |
| Bootstrap state and locked decisions       | [BOOTSTRAP.md](BOOTSTRAP.md) · [BOOTSTRAP-CHECKLIST.md](BOOTSTRAP-CHECKLIST.md)               |

## How work happens

Spec-first: an idea is grilled to a Definition of Ready, proposed as a reviewable spec on a draft
PR **before any code**, implemented on that same PR, then squash-merged with its spec archived and
a retro entry appended. Hotfixes and pure infra changes may use the lighter spec-less lane
(DEC-025). The ceremonies and their commands land in bootstrap Phases 2–3.

## Run it yourself

A machine with Docker and git runs the whole system — no SDK, no Azure, no registry. See
[SELF-HOSTING.md](SELF-HOSTING.md).

## License

MIT — see [LICENSE](LICENSE). This product is open source on purpose: **anyone should be able to
run it**, the same ambition its sibling control planes carry. Self-hostability is a recorded
product goal (DEC-049), and design decisions — a third backend behind a seam, a managed service
versus a container — are evaluated against it.
