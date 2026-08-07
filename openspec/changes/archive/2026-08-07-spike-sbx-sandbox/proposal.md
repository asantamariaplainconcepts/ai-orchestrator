# spike-sbx-sandbox — proposal

Issue: none (spike) · Investigation · Actors: the dev-loop developer today, the selfhost
operator tomorrow · Touches the execution seam behind UC-012 · DEC-012 (pluggable runtimes),
DEC-013 (dispatch), BR-010 (secret names only), #246 (each Run in its own container)

## Why

Since #246 a Run executes in an ephemeral per-Run container, launched over the docker socket.
Two known costs travel with that design: the container shares the host kernel (runc), and the
socket grant to the launcher is root-equivalent on the host. Docker Sandboxes (`sbx`, GA
2026-01-30) runs each sandbox in a microVM with its own kernel and a host-side network policy
proxy, on macOS (Hypervisor.framework) **and** Linux x86_64/KVM — the same tool on the dev
machine and on a selfhost server, which is the convergence the current socket path cannot offer
once isolation hardens.

Before any adoption decision, the load-bearing assumptions are unverified (ADR-0005: claims
that depend on verification are hypotheses). This spike buys the evidence, locally first; the
cloud shape (one or several Linux VMs running sbx) is an outlook question the spike documents
but does not build.

## What Changes

- **Nothing in the product.** No module, no seam, no configuration key changes. The spike
  produces evidence and a recommendation, captured inside this change's `findings.md`.
- Hypotheses under test (each mapped to a task):
  - **H1 — it runs here**: `sbx` installs and boots a sandbox on this Apple Silicon Mac.
  - **H2 — it runs our work**: a kit spec (custom image + entrypoint) executes a real Run
    shape — clone a repository, run Claude Code headless with a credential, produce a diff.
  - **H3 — the firewall is real**: with deny-by-default network policy plus allowances for
    GitHub and the AI provider only, H2 still completes; an unallowed egress observably fails.
  - **H4 — a .NET process can drive it**: create/run/wait/collect/destroy over the `sbx` CLI
    from a small console harness — the `PodRunLauncher` shape survives shell-out, including
    exit codes and log capture. (sbx exposes no REST API; CLI is the only contract.)
  - **H5 — the overhead is tolerable**: wall-clock of the H2 Run under sbx vs the same Run in
    today's docker pod, order-of-magnitude comparison, not a benchmark.
  - **H6 — the cloud shape is nameable** (desk-check only): what changes when the sbx host is
    a remote Linux VM — who invokes the CLI there, KVM/nested-virt prerequisites, one VM vs
    several. Documented as the follow-up's opening questions.

## Out of scope

- Any change to `PodRunLauncher`, dispatch composition, or the selfhost compose.
- CubeSandbox / Kata evaluation (recorded as rejected-for-now alternatives in design.md).
- The pod split (executor out, agent-only sandbox). The spike informs it; a follow-up change
  owns it.

## Non-breaking

**No integration contract is touched** — Aspire graph, queue message schema, host csproj and CI
are all preserved; the spike's harness lives inside the change directory, not in `src/`.

## Exit criteria

A `findings.md` answering H1–H6 with observed evidence (commands, timings, failures verbatim),
and a go/no-go recommendation naming the follow-up change shape if "go" (expected:
`split-run-pod-into-executor-and-sandbox`, with sbx as a launcher driver behind the existing
configuration-presence pattern).
