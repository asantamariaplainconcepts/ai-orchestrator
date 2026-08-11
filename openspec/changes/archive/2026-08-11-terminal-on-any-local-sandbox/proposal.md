## Why

[#304](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/304) gave a terminal to the
sandbox of a Run you happen to be looking at. That is one sandbox, reachable from one screen, and only
while its Run is `Executing`. A developer running this product on their own machine has others — the
`aio-run-*` sandbox a killed process abandoned, the `aio-probe-*` sandbox a readiness sweep made thirty
seconds ago — and no way into any of them. The substrate is reachable only through the slice a Run page
exposes.

This makes the machine's own sandboxes the surface: list them, open a shell in any. Licensed by
[ADR-0021](../../../docs/adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
/ DEC-065, which permits attaching in self-host and refuses it in a deployment.

Realises **UC-029 — Member opens a terminal on this machine's sandboxes**, which this change adds to
`docs/product/mvp/04-mvp-use-cases.md`: the corpus stops at UC-027 and every entry is about Stories,
Runs, Automations or conversations — none describes a person working directly in the substrate. Adding
it is the owner's call under DEC-003, made deliberately rather than by citing a near-miss (issue #311).
For **ACT-002 Member** and **ACT-001 Admin**, holding `run.attach`. Upholds **BR-006** (any bound times
the machine, never the person), **BR-009** and **BR-010**.

**BR-001 and BR-005 are absent, deliberately.** No Run is involved, so no Story lock and no phase
timeout — the same signal that told #304 it was the smaller slice.

## What Changes

- **A read that lists this machine's sandboxes** — `sbx ls`, filtered to the namespace this host already
  claims, annotated with the Run each belongs to where one is known.
- **A terminal keyed by sandbox name** rather than by Run id: a second entry point on the terminal seam
  and the hub, resolving a caller's name against the listed set so a caller can never name a sandbox
  into existence.
- **A sandboxes surface** in the portal, self-host only; a deployment answers that none are hosted.
- **An attach record that survives having no Run**, since the current one writes into a Run's log and a
  Run-less sandbox has none.
- **UC-029** added to `docs/product/mvp/04-mvp-use-cases.md`.

Not **BREAKING**. #304's Run-keyed path, `IRunTerminalHost.Open(Guid, int, int)` and
`RunTerminalHub.Open` keep their behaviour; the queue message schema, Aspire wiring, host csproj and CI
are untouched.

**Three things the issue leaves open, decided in `design.md` rather than discovered in review:**

1. **`aio-*` is shorthand for two prefixes, not a wildcard.** `SbxSandboxLifecycle.ReapAbandoned` claims
   exactly `aio-probe-*` and `aio-run-*`
   ([SbxSandboxLifecycle.cs:138](../../../src/shared/AiOrchestrator.Infrastructure/Agents/Sbx/SbxSandboxLifecycle.cs#L138)).
   Other `aio-` names on the machine — `aio-carry-*`, `aio-workspace-*` — are host temp paths and not
   sandboxes at all. The listed set is the reaper's two prefixes, so the surface reaches exactly what the
   lifecycle manages, which is the whole of the issue's boundary argument.
2. **`run.attach` is project-scoped and a Run-less sandbox has no project.** The hub authorizes through
   `IProjectPermissions.RoleOn(run.ProjectId)`; an abandoned `aio-run-*` sandbox resolves to no Run and
   therefore no project. A machine-wide list cannot be authorized per project without dropping exactly
   the sandboxes the surface exists to reach.
3. **The invariant #304 wrote down is deliberately loosened.**
   [SbxRunTerminalHost.cs:11](../../../src/shared/AiOrchestrator.Infrastructure/Agents/Sbx/SbxRunTerminalHost.cs#L11)
   states there is "no path by which a caller-supplied name reaches `sbx exec`, which is what stops this
   becoming a way to enter any sandbox on the machine." This change introduces that path on purpose. The
   namespace bound replaces the ledger as what makes it safe, so it is a requirement and not a nicety.

## Capabilities

### New Capabilities

None. This extends the two surfaces #304 built rather than opening a new area.

### Modified Capabilities

- `agent-sandboxing`: the machine's sandboxes become enumerable within the namespace this host claims,
  and a sandbox becomes addressable by its own name and not only by the Run id that owns it — which the
  existing requirement *"a Run's sandbox is addressable by Run id for exactly as long as it exists"*
  currently states as the only way in.
- `run-orchestration`: the terminal gains a second entry point keyed by sandbox rather than Run, with its
  own refusal requirement beside the Run-keyed *"a terminal refuses in three distinguishable ways"* — that
  one stays exactly as written, since it still governs the Run's terminal correctly. *"An attach is
  recorded against the Run"* does change: it must hold for an attach that has no Run to be recorded
  against.
- `authorization`: `run.attach` gains a habitat-scoped reading for a resource that belongs to the
  machine rather than to a project.

## Impact

**Backend.** `Modules/Runs/Features/Observation` — the listing use case, the hub's second entry point,
the attach record. `shared/AiOrchestrator.BuildingBlocks/Agents/IRunTerminalHost.cs` — the seam grows a
sandbox-keyed `Open` and an enumeration, and `UnhostedRunTerminalHost` answers for both.
`shared/AiOrchestrator.Infrastructure/Agents/Sbx` — `SbxRunTerminalHost` (the loosened invariant and its
doc comment), `SbxSandboxLifecycle` (the `ls` parse, currently first-token-only and reused rather than
duplicated). `RunSandboxHost` gains enumeration for Run attribution; it stays in memory and unpersisted,
so attribution is available for this process's Runs and absent for everything else — which is what makes
"the Run each belongs to where it has one" the honest shape rather than a gap.

**Frontend.** A new self-host-only sandboxes surface reusing `features/runs/RunTerminal.tsx`'s xterm
transport; copy through the i18n catalogue (DEC-021), tokens only (DEC-009).

**Tests.** `ProjectRoles_Should_Constraint.EnforcedOutsideThePipeline` names `RunTerminalHub.Open` as
`run.attach`'s only enforcer — its doc comment must name the second one.
`RunTerminalRefusal_Should_Constraint` gains the out-of-namespace and no-such-sandbox refusals.
`RealSbxTerminal_Should_Constraint` (DispatchTests, real `sbx`) is where the shell-in-a-listed-sandbox
claim gets exercised for real rather than asserted.

**Docs.** `docs/product/mvp/04-mvp-use-cases.md` gains UC-029. UC-024 is defined twice there, which makes
every citation of it ambiguous — noticed while grilling #311, and already being fixed by #315 /
[PR #316](https://github.com/asantamariaplainconcepts/ai-orchestrator/pull/316), so this change leaves it
alone rather than colliding.

**Security.** The same #288 exposure #304 accepted — a shell in a sandbox carrying the machine owner's
session — now reachable without a Run in view. Self-host only (ADR-0021). No new secret handling, so
BR-010 is unaffected.
