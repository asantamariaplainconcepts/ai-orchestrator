# Proposal: github-connector-backlog-mirror

Closes [#7](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/7).

## Why

A Project is currently a name in a database. This change connects one to real work: an Admin
points it at a GitHub repository (**UC-004**), the system polls that repository (**UC-009**), and
the team sees its Stories inside the application (**UC-007**). Actors: **ACT-001 Admin**
configures, **ACT-002 Member** reads.

It is first on the sequencing spine for a reason that is not preference — Automations match
Stories, dispatch creates Runs from Stories, Agents act on Stories. **None of that can be built,
or even meaningfully specified, until Stories exist in the system.**

## What Changes

Three new capabilities (delta specs under `specs/`):

1. **connector-configuration** — a Project gains exactly one Connector: vendor, repository
   coordinates, and the **name** of the secret holding its access token. Saving verifies the
   credential against the live vendor before storing, so a Connector that exists is a Connector
   that works.
2. **backlog-mirror** — Stories read through the Connector are persisted as a read model (id,
   title, state, labels, last-seen). The vendor stays the source of truth (**BR-008**); the mirror
   exists so the UI is fast, so trigger-matching can diff, and so a vendor outage degrades to
   stale data rather than an empty screen.
3. **secret-resolution** — an `ISecretResolver` seam resolving **per read**. The database stores a
   secret **name**, never a value (**BR-010**). Development resolves from user-secrets; the Key
   Vault implementation arrives with the infrastructure change (#8) behind the same interface.

   Aspire's Key Vault integration was checked and it shapes this (design D3): it has **no
   emulator**, so it does not remove the need for a development resolver; its client integration
   is an `IHostApplicationBuilder` extension, so wiring must live in the host — a module
   structurally cannot call it; and its `AddAzureKeyVaultSecrets` option is **rejected**, because
   loading the vault into `IConfiguration` at startup cannot see a secret an Admin creates
   afterwards.

## The conflict this resolves, and how

BR-010 requires secrets outside the database. DEC-030 requires a per-project token. **Key Vault
does not exist** — infrastructure was deferred in Phase 1 and is now sequenced *behind* this
change (#8). Guessing would have meant either violating BR-010 or blocking on infra.

The seam resolves it honestly: nothing but a secret *name* is ever persisted, so BR-010 holds
**today**, and #8 supplies the real resolver without touching a single call site.

## Out of scope (each with its reason)

- **Writing back to GitHub** — labels, comments, transitions. This slice reads only; UC-008 and
  the Agent actions own the write path.
- **Azure DevOps Connector** — depends on **OPN-003**, and RULE-006 forbids proposing on an open
  decision. DEC-011 sequences it second regardless. The seam is designed so it slots in; no AzDO
  code appears here.
- **Automations, matching, Runs, Agents, dispatch** — later spine slices (#9).
- **Webhooks** — DEC-028 ships polling first.
- **Azure infrastructure / Terraform** — #8.
- **Authentication** — blocked by **OPN-002**. This slice runs against the app as it is today.

## Impact

- **New module: `Backlog`.** The second module in the monolith — which means the boundary
  analyzers and ArchTests face a real cross-module situation for the first time, rather than the
  throwaway probe used in Phase 1. See `design.md` D1 for why it is a module and D2 for why it
  needs **no** `.Contracts` project.
- New: `AiOrchestrator.Modules.Backlog` (Connector, Story, GitHub client, poller), an
  `ISecretResolver` in BuildingBlocks, a project page in the frontend.
- Modified: `AiOrchestrator.Server` (module discovery already handles it — no host edit expected,
  which is itself worth verifying), CPM gains Octokit.
- Affected specs: three ADDED. `backend-architecture` is **not** modified — this change is the
  first real test of whether its rules hold under a second module, not a change to them.
