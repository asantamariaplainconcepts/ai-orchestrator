## Why

A Member watching a self-host Run fail on something trivial — a missing dependency, a merge
conflict, a test that only fails inside the sandbox — can today read about it and re-run, and
nothing else. The sandbox holding the answer is a microVM on their own machine with no door.

[ADR-0021](../../../docs/adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
/ DEC-065 opened that door in self-host, and closed it in a deployment: hardware its operator owns
is not hardware someone else pays for. This change builds the **beside-the-agent** half — a real
terminal in the Run's sandbox while the agent keeps running headless
([#304](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/304)). Typing into the
agent's own process is #308 and sequences behind this.

Realises **UC-024** (the grill, whose interrogation is only usable if a stuck Run is recoverable),
for **ACT-002 Member** and **ACT-001 Admin**. Upholds **BR-006** (any bound times the machine, never
the person), **BR-001** (a waiting Run still blocks its Story), and **BR-009**/**BR-010**
(permissions, and credentials never crossing their boundary). **BR-005 is deliberately untouched:**
the agent stays headless, so its phase timeout keeps exactly its current meaning — the clearest
evidence this is the smaller of the two slices.

## What Changes

- **A per-Run sandbox registry.** The sandbox's name is a local variable in
  `SbxAgentProcessHost.Run` and reaches nothing. A `RunSandboxHost` modelled precisely on
  `RunPreviewHost` records it beside creation and removes it in the same `finally` — so "a finished
  Run has no terminal" is a property of the composition rather than a branch to remember.
- **A `run.attach` permission**, beside the existing `run.*` constants, granted to **both** the Admin
  and Member bundles.
- **A terminal surface** over SignalR at `/hubs/run-terminal`, authorizing itself in the hub the way
  `RunLogHub.Watch` does — a hub dispatches nothing through the CQS pipeline, so the decorator that
  guards every other read never sees it.
- **A host-side pty**, because `sbx exec -it` refuses a redirected pipe (measured) and live resize
  needs `TIOCSWINSZ` on the master, which the spike's `script` trick cannot reach.
- **A habitat answer that is not a failure.** Where the launcher is not sbx, the surface reports
  *no terminal is hosted here* — distinct from *you may not* and from *this Run has no terminal* —
  following `RunPreviewHost.Hosted`'s three-way distinction rather than collapsing them.
- **An xterm.js terminal in the Run screen**, sibling to `RunPreviewFrame`, appearing only while the
  Run executes and disappearing with it.

Not **BREAKING**. No integration contract moves: the Aspire model, the outbox message schema, the
host csproj and CI are untouched. One new frontend dependency (`@xterm/xterm` + the fit addon) and
one new backend package for the pty — both additive, both centrally pinned.

## Capabilities

### New Capabilities

None. This extends capabilities that already exist rather than introducing a new area of the
product; the terminal is a new surface on `run-orchestration` and a new lifetime rule in
`agent-sandboxing`.

### Modified Capabilities

- `run-orchestration`: a Run's observation surfaces gain a terminal, with its own permission and its
  own habitat answer. The requirement that a Run's live surfaces exist only while it executes
  extends to cover it.
- `agent-sandboxing`: the sandbox becomes **addressable by Run id** for the first time. The existing
  requirement is that a sandbox is per Run and does not outlive it; this adds that its *name* is
  discoverable for exactly that window and by nothing else.
- `authorization`: a new project-scoped permission and which role bundles hold it.

## Impact

- **Backend:** `AiOrchestrator.Infrastructure/Agents` (`RunSandboxHost`, an interactive-process seam
  beside `HeadlessProcess`, the sbx host recording the name), `Modules/Runs` (the hub, the permission
  constant, the habitat read), `BuildingBlocks` (the monitor contract, mirroring
  `IRunPreviewMonitor`).
- **Frontend:** a `RunTerminal` slice under `features/runs`, new i18n keys, and the mock-mode guard
  every live surface needs.
- **Dependencies:** `@xterm/xterm` and `@xterm/addon-fit`; a pty package pinned in
  `Directory.Packages.props` — **no pty package is pinned today**, so the design must choose one and
  justify it against a P/Invoke.
- **Habitats:** self-host only. The deployed ACA host answers *not hosted here*, which is the same
  shape its preview answer already has.
- **Security:** this is arbitrary command execution inside a sandbox carrying the machine owner's own
  session (#288), reachable by a Member. That is the accepted consequence recorded on #304, and it is
  why every attach is recorded.
