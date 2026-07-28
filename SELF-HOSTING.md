# Self-hosting

Run the whole system on any machine with **Docker and git**. No .NET SDK, no Azure, no registry:
every image builds locally from this repository's Dockerfiles, the queue is
[Azurite](https://github.com/Azure/Azurite), the database is a Postgres container, and the
default agent runtime is opencode's **free model** (DEC-044) — the demo costs nothing and needs
no AI key. The only credential anywhere is your own GitHub PAT.

## Quickstart

```bash
git clone https://github.com/asantamariaplainconcepts/ai-orchestrator.git
cd ai-orchestrator/selfhost
cp .env.example .env    # set POSTGRES_PASSWORD to anything; SERVER_PORT if 8080 is taken
docker compose up --build
```

First build takes a few minutes (three .NET images plus the SPA). When it settles, open
`http://localhost:8080` (or your `SERVER_PORT`).

Then close the loop:

1. Create a project, press **Set up defaults** — six Automations, wired as a pipeline.
2. Configure the Connector against a GitHub repository you control. The secret *name* you enter
   must exist as a configuration value: add `Secrets__<name>=<your PAT>` to the `server` and
   `dispatch` services' environment (an `.env` reference works). Names in the app, values in the
   environment — that split holds everywhere this product runs (BR-010).
3. Label an issue `ai:estimate` (or `ai:grill`, and answer its questions). Watch the Run's
   output live on its page.

## What this is and is not

- **Trusted networks only.** You are the owner of this deployment: the compose sets
  `Identity__Mode=LocalOwner`, so every action runs as an administrator with no sign-in — the
  same posture as running loop-task daemons on your own machines. Do not expose the port to the
  internet. Real sign-in arrives with OPN-002; the product refuses to start in that mode on
  provisioned infrastructure, so this setting cannot travel to a shared deployment by accident.
- **No KEDA.** The dispatch worker drains on a 5-second timer. What compose proves about the
  queue contract is exactly what production does; what it proves about scaling is nothing.
- **`selfhost/docker-compose.yaml` is generated** from the same Aspire AppHost that `aspire run`
  uses — never edit it by hand. Change the AppHost and run `scripts/generate-compose.sh`; CI
  fails if the two drift (ADR-0003).

## The three habitats

| | starts it | queue | secrets |
|---|---|---|---|
| `aspire run` | Aspire (dev) | Azurite (emulator resource) | user secrets / config |
| **this compose** | Docker | Azurite (container) | `.env` |
| Azure dev | GitHub Actions | Storage Queue + KEDA | Key Vault |

One composition, one behaviour, three places to run it (DEC-049).
