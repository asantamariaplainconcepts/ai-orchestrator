# spike-sbx-sandbox — design

## Context

Explored 2026-08-07 (session: sandboxes source of truth). The ecosystem pattern — lightweight
central orchestrator outside, ephemeral per-task sandbox inside — is already this product's
architecture (#246). The real deltas found:

1. **Contents of the pod**: today the pod is the full DispatchWorker image — .NET module host,
   database connection string, secret-store paths, the host's `~/.claude` (ro) — with the agent
   CLI as a child process. The reference pattern's sandbox holds repo + agent + scoped
   credentials only, with the orchestration layer as the firewall between thinking and acting.
2. **Isolation tech**: runc containers share the host kernel; the launcher's docker-socket
   grant is root-equivalent. MicroVMs remove both, at the price of KVM (Linux) or a
   platform-specific VMM.

## Decisions

### D1 — sbx is the PoC target, not (yet) the product decision

Chosen because it is the only surveyed option that runs the same way on the dev Mac and a
selfhost Linux server (its own cross-platform VMM: Hypervisor.framework / KVM / WHP), and the
operator grant it needs is purpose-built for this exact use case instead of a root-equivalent
docker socket.

**Rejected for now — CubeSandbox** (Tencent, Apache 2.0): the better *server* story (REST API,
E2B-compatible, credential-injecting egress proxy — secrets never enter the sandbox) but no
macOS support, v0.6.x maturity, and little independent validation yet. It re-enters the
conversation if the follow-up adopts a protocol-shaped seam (`ISandboxLauncher`) — then it is a
driver, not a bet.

**Rejected for now — Kata Containers**: smallest code delta (`HostConfig.Runtime = "kata"`,
same socket, same probes) but Linux-only, so local and server diverge — the exact property this
exploration set out to remove.

### D2 — local first, cloud as a documented outlook

The user's sequencing: PoC on the Mac now; the cloud arrives later, shaped as one or several
VMs running sbx. H6 therefore stays a desk-check: prerequisites (KVM, nested virtualization on
cloud VM SKUs), who invokes the CLI on a remote host (sbx has no REST API — a host-side
launcher process, SSH, or a future API are the candidate answers), and what one-vs-several VMs
changes. No cloud resource is created by this spike.

### D3 — the spike lives inside the change directory

Kit specs, the .NET shell-out harness, timings and raw failures go under
`openspec/changes/spike-sbx-sandbox/` (`poc/`, `findings.md`), never under `src/`. ADR-0003: a
derived artifact has one owner — this evidence belongs to the spike, and archiving the change
archives the evidence with it. Nothing in the product references it.

### D4 — evidence discipline

ADR-0006/ADR-0001: claims are verified by exercising them. Every H1–H5 verdict in findings.md
carries the command run and the observed output (truncated, verbatim). A hypothesis that cannot
be exercised on this machine is recorded as **not verified**, never inferred from
documentation — the v0.35 Linux-builds retraction is a reminder that sbx documentation and sbx
behaviour can differ.

### D5 — what "go" must name

A "go" recommendation is only actionable if it names the follow-up's seam: the pod split
(executor stays with the orchestrator; sandbox holds workspace + agent CLI + in-memory
credentials) with the launcher chosen by configuration presence, exactly as `Dispatch:PodImage`
selects pods today (ADR-0010: a habitat contract is asked, never inferred). DEC-062's "the
agent publishes its PR itself" is the open product question the follow-up must answer — sbx
network policy makes "the PAT never enters the sandbox" *possible*, and possible is not
decided.

## Deviations

None from backend conventions — no backend code changes. The spike harness is throwaway by
design and exempt from vertical-slice structure (it is not product code; D3).
