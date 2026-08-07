## Why

A Run that changes a web application is judged today from a diff and a transcript. The change
either looks right in the patch or it does not — nobody sees it running until it merges.

The [sbx spike](../archive/2026-08-07-spike-sbx-sandbox/findings.md) noticed the opening, and the
sandboxing change measured it: `sbx ports` allocates an **ephemeral host port bound to loopback**
when the host port is omitted, and content served inside a sandbox is reachable from the host.
An agent already working inside a sandbox can start the app it just changed, and the portal —
the same machine — can show it.

The prize is small and specific: while a Run executes, a Member can look at the thing instead of
at a description of the thing.

## What Changes

- **A Run may expose a live preview while it is executing, and only then.** The window is the
  sandbox's own life. When the agent finishes, the sandbox is disposed (the sandboxing change's
  D3, unchanged by this) and the preview ends with it.
- **A finished Run offers nothing.** Not a dead link, not a disabled control, not an explanation
  of what used to be there — no affordance at all. A preview is not an artifact a Run leaves
  behind, and a UI that implies otherwise would promise something no Run can keep.
- **The record is deliberately not durable.** Which port a Run published lives in the launcher's
  memory, exactly as the pods ledger does and for the same stated reason: a table would outlive
  the sandbox it describes and lie after a restart.
- **The portal proxies rather than links.** A published port is loopback-bound, so a browser
  cannot reach it directly; the Server relays, and agent-authored content is confined so it
  cannot act as the portal.
- Not **BREAKING**: a Run that publishes nothing behaves exactly as today, which is every Run
  until an Automation asks for a preview.

### Prerequisite

This depends on `split-run-pod-into-executor-and-sandbox` being archived: it builds on the
sandbox process host, its per-Run lifecycle, and the `Agents:Sandbox:*` configuration. It cannot
be implemented before that lands.

### Deliberately not in this change

- **Keeping a sandbox alive past its Run** — the whole design rests on not doing this.
- **Previews for the pod substrate or in-process execution.** Only the sandbox lane publishes.
- **Public or shared preview URLs.** Loopback and the portal's own session; nothing leaves the
  machine.

## Capabilities

### New Capabilities

- `run-preview`: what a Run may expose while it executes, how long that lasts, and what the
  portal does with content an agent wrote.

### Modified Capabilities

- `run-orchestration`: a Run's detail gains a live view that exists only in an active state —
  the first thing on that screen whose availability is a function of the Run being alive rather
  than of what it recorded.

## Impact

- **Code**: the sandbox process host (publishing a port and reporting it), a per-process ledger
  beside the pods ledger, a proxy endpoint on the Server, and the Run detail screen.
- **Security**: this is the first time the portal serves bytes an agent authored. The isolation
  decision belongs in design and is the reason this is its own change.
- **Habitat**: only where the executor and the portal share a machine, because a loopback port
  is unreachable from anywhere else. A queue habitat with a separate worker gets no preview, and
  must say so rather than appear broken.
- **Tests**: unit coverage for the lifecycle (published, disposed, terminal Run shows nothing)
  and the proxy's refusals; the end-to-end proof is a manual exercise on a machine with sbx, as
  the sandboxing change established.
