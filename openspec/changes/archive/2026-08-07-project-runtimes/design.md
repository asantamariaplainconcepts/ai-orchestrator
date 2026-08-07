## Context

Runtime selection today: `Automation.Runtime` is a mandatory enum-backed name (Projects module);
the selector seam (`IAgentRuntimeSelector.For(name)` → `AgentRuntimeSelection(Runtime,
CredentialSecretName, CliName, Remedy)` since #279) carries one deployment-wide credential name
per runtime from config (`Agents:{Name}:CredentialSecretName`, DispatchComposition). The executor
resolves `automation?.Runtime ?? run.RuntimeName ?? "ClaudeCodeHeadless"` since run-on-a-pr, and
`Run.RuntimeName` already exists as a column. Both runtimes export `GITHUB_TOKEN`/API-key env vars
unconditionally — the AC6 shadowing defect.

Module boundaries: Projects owns project configuration; Runs consumes through Contracts only.

## Goals / Non-Goals

**Goals:** one place to change a project's runtime; per-project billing identity by name
(BR-010); the human's per-Run choice at launch points, recorded (BR-014); the Local lane's host
auth no longer shadowed.

**Non-Goals:** new runtimes (Copilot follow-up); model selection; per-project gateway URL;
changing how the selector composes runtimes.

## Decisions

### D1 — Settings live on the Projects module, read through a new Contracts member

`Project` gains `DefaultRuntime` (nullable string; null = deployment default) and a
`ProjectRuntimeCredential` owned collection (runtime name → secret name) — one Projects migration
together with `Automation.Runtime` becoming nullable. Runs reads them via
`IAutomationCatalog`-adjacent Contracts: a new `IProjectRuntimeSettings.Resolve(projectId)`
returning `(DefaultRuntime, IReadOnlyDictionary<string,string> CredentialNames)`. Asked per
execution, never cached — the same freshness rule `IProjectCatalog` states.

*Rejected — settings on the Connector.* The Connector is the backlog's identity; runtimes execute
work and exist without a Connector.

### D2 — The chain is one function with one order

`run.RuntimeName ?? automation?.Runtime ?? settings.DefaultRuntime ?? "ClaudeCodeHeadless"`, and
credential `settings.CredentialNames[runtime] ?? selection.CredentialSecretName ?? none`. The
human's recorded choice outranks the Automation because AC3 says the choice is for that Run —
recording it and then losing the race to the Automation's value would make the dialog a lie. The
transcript names the credential's source (project/deployment/none).

### D3 — Launch points pass the choice; approval does not re-choose

Run now and run-on-change gain an optional `runtime` (run-on-change already has it); the re-run
button reuses Run now's dialog. The approval bar does NOT offer a runtime change: an approved plan
was produced by a runtime, and executing it on another would approve one agent's plan and run
another's hands — AC3's "approval" launch point is satisfied by *showing* the resolved runtime
there, not by changing it. This narrowing is deliberate and recorded here.

### D4 — Automation form: "Project default" is the first option

The runtime select gains a first option (empty value) labelled with what the default currently
resolves to. The update request sends `runtime: null` for it; the API stores null; existing rows
are untouched by the migration.

### D5 — Empty env vars are never exported

`ClaudeCodeHeadlessRuntime`/`OpenCodeRuntime` add env entries only for non-empty values. Pinned by
a unit-level assertion where the process env is built; the real-Local-run exercise (AC6's
"exercised, not assumed") is recorded as run when it is run — the functional tier cannot spawn the
host CLI, and a claim beyond the tier would be ADR-0005's violation.

## Risks / Trade-offs

- **Two rules disagree about who wins (run vs automation).** → One resolution function, one test
  pinning the order; the executor's old inline `??` chain is deleted, not paralleled.
- **A project credential name that resolves to nothing.** → The existing SecretNotFound failure
  path already names the missing name; the transcript now also names its source.
- **Migration touches the Automations table.** → Nullability-widening only; the migration test
  reality (fixtures migrate from scratch) covers it, plus the existing-rows scenario asserts no
  behaviour change.

## Migration Plan

One Projects migration: `automations.Runtime` → nullable; `projects.DefaultRuntime` column;
`project_runtime_credentials` table. Down reverses; no data backfill (existing rows keep values).

## Open Questions

None. AC3's approval-point narrowing is D3's recorded decision.
