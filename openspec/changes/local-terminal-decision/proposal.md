## Why

A terminal is a property of the **sbx launcher**, not of locality.
`AgentSandboxComposition.AddAgentProcessHost` registers `IRunTerminalHost` and `RunSandboxHost` only
inside the sbx branch; the local branch registers `LocalAgentProcessHost` and **returns** at line 103,
before either. So the local habitat resolves `UnhostedRunTerminalHost` (`RunsModule.cs:131`), whose
`Hosted` is `false` — and the one habitat [ADR-0021](../../../docs/adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
explicitly *permits* attaching in is the one habitat with no terminal at all.

A developer running `aspire run` without sbx installed can watch a Run's log and nothing else.

Removing that gap is not a configuration change. Without the sandbox, `run.attach` stops meaning
*"a shell in a disposable microVM"* and starts meaning *"a shell on this machine, in the server
process's privileges, over a WebSocket"*. DEC-065 permits an attached session in self-host, but it was
decided **with the sandbox in frame** — the microVM was the bound that made the grant safe to give.
That is a different grant with the same name, so **RULE-006** requires the decision before the
capability. This change delivers the decision and nothing executable, closing **OPN-008**
([#357](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/357)) and unblocking
[#358](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/358).

## What Changes

- **Record OPN-008** in [`07-open-decisions.md`](../../../docs/product/mvp/07-open-decisions.md) —
  *whether a terminal may open outside a sandbox on a machine its operator owns* — and **close it in
  the same change**, the convention OPN-002, OPN-005, OPN-006 and OPN-007 all followed.
- **Write an ADR** evaluating **four** options with their blast radius on `IRunTerminalHost`, on
  `run.attach`, and on ADR-0021's deployed refusal:
  - **(a) status quo** — terminals require a sandbox;
  - **(b) an unbounded host terminal** — a plain shell on the machine;
  - **(c) a bounded host terminal** — working directory pinned to the Run's own checkout, the server's
    environment not inherited, a named shell;
  - **(d) neither — make a sandbox cheap instead**, keeping the boundary.
- **Land the outcome as `DEC-070`** in
  [`10-locked-mvp-decisions.md`](../../../docs/product/mvp/10-locked-mvp-decisions.md), with OPN-008
  moved to that file's closed list — never edited in place.
- **Settle the naming**, because the seam's vocabulary is sandbox-shaped (`LocalSandbox`,
  `RunSandboxHost`, `MachineSandboxAccess`, `ListMachineSandboxes`) and stops being true the moment a
  terminal may open on something that is not a sandbox. Left to implementation, this becomes a rename
  discovered halfway through #358.
- **Record the spec delta** on `agent-sandboxing`, which owns the terminal requirements today.

Not a **BREAKING** change: no integration contract moves. This change edits documents and spec
requirement text; it ships no code.

## Capabilities

### New Capabilities

None. This is a Foundation decision-closure item (RULE-006): its deliverable is a recorded decision,
not a user-visible capability. The capability it unblocks (#358) is a separate issue by RULE-002.

### Modified Capabilities

- `agent-sandboxing`: the requirements that describe a terminal today are written as though a terminal
  is always inside a sandbox — *"a human attached to a sandbox does not extend its life"*, *"this
  machine's own sandboxes are enumerable"*. The decision states which of those bind the **sandbox**
  and which bind **any terminal**, so #358 is not left to infer it.
