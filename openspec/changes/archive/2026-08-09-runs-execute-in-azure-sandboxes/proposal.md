## Why

[#296](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/296). The cloud path this
programme planned executes each Run in a container from a pod image, launched over the **docker
socket**. `run-dispatch`'s own requirement calls that the operator's explicit grant and warns it is
*root-equivalent on the host*; the container shares that host's kernel. Those two costs are what
opened this line of work in the first place, and they are still unpaid. Around them sit two more: an
image of ours to build, publish, version and pull, and an executor that must live on the sandbox's
machine.

`spike-azure-container-apps-sandboxes` measured a substrate that removes all four. Its findings are
this proposal's evidence and are cited rather than restated — every number below has a command and
a date behind it there.

Actors: **ACT-001 Admin** configures a deployment, **ACT-003 Agent** executes in it, **ACT-002
Member** receives the isolation. Use cases: UC-012, UC-016, UC-020, UC-027. Business rules: BR-004,
BR-005, BR-010, BR-016.

## What Changes

A habitat can name an **ACA sandbox launcher**, and a Run dispatched there executes in a
hardware-isolated microVM created over an authenticated API — no docker socket in the deployment at
all. The workspace reaches it without the executor sharing its machine, which is the constraint the
`--clone` spike found and this one lifted.

The launcher declares what the spike measured must be declared: **auto-suspend off** (it is on at
600 s by default and suspends a sandbox whose agent is thinking), **deny-default egress with an
allow list**, and credentials as **typed providers injected at the boundary** so no value is
readable inside.

One **SandboxGroup per Project**, so the per-project billing identity #244 promises survives into a
substrate whose credentials live on the group.

Preview ports are created Entra-gated and **relayed through the portal**, so run-previews' contract
is unchanged: reachable while the Run lives, nothing afterwards.

**The pod substrate is retired.** Three habitats, three answers — in-process for a machine somebody
owns, sbx for the dev loop, this for a deployment — and the only one with a shared kernel and a
root-equivalent socket goes away.

## Capabilities

### New Capabilities

None. This is a third implementation of a seam that already exists.

### Modified Capabilities

- `agent-sandboxing`: a launcher whose sandboxes are created remotely, with what such a habitat
  must declare; and the locality requirement, which this change finally settles rather than
  narrows.
- `run-dispatch`: the pod-image substrate is removed, and a habitat still naming one is refused.

## Impact

**Composition.** A third `IAgentProcessHost` beside `LocalAgentProcessHost` and
`SbxAgentProcessHost`, selected by configuration presence like the others (ADR-0010). The
executor is untouched: the poll loop the measured ~50 s `exec` ceiling forces lives **inside** the
host, so `Run()` still blocks and still streams through `onOutput`.

**Removed.** `Dispatch:PodImage`, its launcher, the docker-socket grant and the generated compose
warning that went with it, and the `dispatch-worker` image's role as a Run substrate.

**Infrastructure.** SandboxGroups become a per-project resource, which Terraform creates and the
product references. The data plane needs `Container Apps SandboxGroup Data Owner`, and role
propagation is observable — the spike hit 403s for about a minute after granting it.

**Not touched.** sbx and the dev loop, in-process execution, the outbox, the queue, and #288's
session carriage, which cannot exist where there is no machine owner.
