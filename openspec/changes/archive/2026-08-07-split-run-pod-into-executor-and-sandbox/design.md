## Context

Two facts from the code decide most of this design.

**One.** Today's isolation lives at the dispatch seam: `PodRunLauncher` starts a container from
the *worker image* running `--run <id>`, so the container holds the module host, the database
connection string, the secret-store paths and the host's agent sessions
([DispatchComposition.cs:123](../../../src/shared/AiOrchestrator.ServiceDefaults/Dispatch/DispatchComposition.cs:123)).
The agent CLI is a child process inside all of that.

**Two.** Every agent CLI is spawned through exactly one chokepoint:
[`HeadlessProcess.Run`](../../../src/shared/AiOrchestrator.ServiceDefaults/Agents/HeadlessProcess.cs),
shared by `ClaudeCodeHeadlessRuntime` and `OpenCodeRuntime` precisely so BR-005's kill-on-timeout
cannot drift between them. Command line, environment, working directory, streaming callback and
timeout all pass through that one function.

So "put the agent in a sandbox and leave the executor outside" is not a rewrite of the runtimes.
It is a substitution at that chokepoint.

The [spike](../archive/2026-08-07-spike-sbx-sandbox/findings.md) supplies the mechanics, all
exercised: a sandbox mounts the host workspace over virtiofs **at the same absolute path**;
`sbx exec` is docker-exec-shaped with faithful exit codes and captured streams; an inner
non-zero travels verbatim; refusals name their cause; `rm` needs `--force` off a tty; secrets
never enter the sandbox at all (`GITHUB_TOKEN` was empty while `git ls-remote` on the private
repository succeeded); warm creation ~4.5s.

## Goals / Non-Goals

**Goals:**

- The agent CLI runs inside a boundary that holds the workspace and the CLI, and no orchestrator
  credential or connection string.
- The runtimes' own logic — flags, stream-json parsing, usage extraction, timeout semantics — is
  untouched by where the process runs.
- Selection by configuration presence; every habitat that names nothing behaves byte-identically
  to today.
- Every new way this can fail names its remedy, in the same places #279 already speaks.

**Non-Goals:**

- Selfhost/Azure rollout (Linux/KVM unverified — proposal says so).
- Retiring or reshaping the pod substrate.
- Warm-pooling sandboxes, snapshots, or Run previews over published ports.
- Changing DEC-062: the agent still publishes its own PR. What changes is that it can do so
  while holding nothing.

## Decisions

### D1 — The seam is the process host, not a second runtime per driver

Introduce `IAgentProcessHost` with `HeadlessProcess`'s exact signature (command, arguments,
workspace, environment, timeout, cancellation, `OnOutput`) returning the same `Outcome`. The
local implementation is today's `Process.Start` body, moved. The sandboxed implementation runs
the same command inside a per-Run sandbox. `ClaudeCodeHeadlessRuntime` and `OpenCodeRuntime`
take the host as a dependency and are otherwise unchanged.

*Alternative rejected — a sandboxed `IAgentRuntime` per runtime* (`SandboxedClaudeRuntime`, …):
it multiplies N runtimes × M drivers, duplicates the stream-json parsers, and reintroduces
exactly the timeout drift `HeadlessProcess` was extracted to prevent. Adding a third runtime
would then mean writing it twice.

*Consequence to accept:* the seam speaks in "command + args + env", which is a process-shaped
vocabulary. A future driver whose API is not process-shaped (an HTTP sandbox service) would
adapt to it rather than the reverse. Given sbx is CLI-only (spike H4) and the pod path is
process-shaped too, this is the right shape now, and the interface is internal to composition —
changing it later costs one refactor, not a contract break.

### D2 — Credentials: injected or passed, never silently neither

`AgentCredentials` today travels values into the child's environment. A sandbox driver that
injects at egress (sbx) must **not** receive or forward them. The process host therefore
declares whether it supplies credentials out-of-band; when it does, the runtimes omit the
credential environment variables entirely, and the transcript names the source — the same
honesty the pod substrate already owes about the host's sessions.

The failure mode this creates is the dangerous one: a driver claiming injection whose secret was
never stored, leaving the agent to run unauthenticated and fail deep inside a Run for a reason
that reads like a repository problem. **Mitigation is a precondition, not a hope**: an injecting
host asserts the credential is present before the Run's agent starts, and a missing one refuses
in the #279 voice, naming the store and the command that fixes it.

### D3 — One sandbox per Run, destroyed with it

Created before the agent starts, destroyed in a `finally` that survives cancellation — the
`PodRunLauncher` precedent, for the same reason: an abandoned sandbox is the leak, and the Run's
truth lives in the database. Disposal passes `--force` (spike H4: sbx refuses prompts off-tty).

*Alternative rejected — a warm pool.* It would save the ~4.5s creation, but a reused sandbox
carries the previous Run's filesystem and network state across project boundaries, which is the
property this change exists to establish. 4.5s against a minutes-long Run is noise (H5).

### D4 — The workspace is the host path, and the driver must prove it

sbx mounts the workspace at the same absolute path inside the sandbox (observed), so
`WorkspacePath` needs no translation. Rather than assume that of every driver, the host maps the
workspace and returns the path the command will see; the sbx driver's mapping is the identity
and it asserts the directory is visible inside before running — a wrong assumption must fail
loudly at the boundary, not as an agent confused about a missing repository.

### D5 — Ambiguous isolation is refused, not layered

A habitat naming both a pod image and a sandbox launcher is asking for the agent to be isolated
inside a container that is itself the isolation. Composition refuses that combination naming
both keys, exactly as it already refuses a host that holds both the queue and its consumer
([DispatchComposition.cs:60](../../../src/shared/AiOrchestrator.ServiceDefaults/Dispatch/DispatchComposition.cs:60)).
Refusing is better than picking: silently preferring one would make the operator's second key a
no-op they cannot see.

### D6 — Readiness tells the truth about where the CLI actually lives

`AgentRuntimesProbe` today runs `<cli> --version` in this process. Where Runs execute in
sandboxes, that answer is about the wrong machine: the CLI that matters lives in the sandbox
template. The probe therefore asks the sandbox host its own preconditions — daemon reachable,
identity present, network policy initialized — and reports the CLI's readiness from where the
CLI will run. A probe that keeps reporting the host's binary would state a truth that no Run
depends on, which is worse than silence.

### D7 — What may not enter, enforced structurally

The connection string, secret-store paths and module configuration must be unable to reach the
sandbox — so the driver is constructed with its own options only and never sees `IConfiguration`.
The prohibition is a shape, not a review comment: there is no code path that could pass them.

## Risks / Trade-offs

- **Silent unauthenticated Runs** (D2's failure mode) → precondition assertion before the agent
  starts, refusal in the remedy voice; plus a test that a claimed-injecting host with no stored
  secret refuses rather than proceeding.
- **CI cannot exercise a real sandbox** (needs KVM/a VMM) → unit-test the selection, the refusal
  sentences and the credential-omission contract; the end-to-end proof is a documented manual
  exercise on the dev machine, recorded as evidence the way the spike recorded its own
  (ADR-0001). Do not fake it with a stub that would pass whatever we wrote.
- **The injecting proxy terminates TLS** → a service that pins certificates fails inside a
  sandbox in a way it does not outside. Note it where the operator opts in; GitHub and the AI
  providers do not pin.
- **sbx is young and its install is awkward here** (brew cask refuses macOS 26; v0.35 pulled its
  Linux builds) → pin the version in configuration, have the probe report the version it found,
  and keep the local process host as the default so a broken upgrade degrades to today.
- **Default sandbox memory is 50% of host RAM** (H5) → the driver passes an explicit limit;
  concurrency times limit must fit the machine, and the panel already has a place to say so.
- **Two isolation stories in the codebase at once** (pods and sandboxes) → D5 refuses the
  ambiguous combination, and the proposal states plainly that retiring pods is not promised. A
  later change decides whether one supersedes the other, with the Linux evidence in hand.

## Migration Plan

Additive and off by default. Named nowhere, no behaviour changes anywhere. The dev loop opts in
by naming the launcher in user-secrets; rollback is removing the key — no data, no schema, no
queue-shape involvement. The manual proof runs on the dev machine before the change is
considered done.

## Open Questions

- **The Linux/KVM leg** — sbx on a selfhost VM, and headless `sbx login` enrolment. Blocks the
  habitat rollout, not this change.
- **Does the sandbox eventually supersede the pod substrate**, or do they serve different
  habitats permanently? Answerable only with the Linux evidence.
- **Per-host concurrency policy**: today `MaxConcurrentPods` bounds pods at the dispatch seam.
  Sandboxes are bounded at the runtime seam, in a different process. Whether these become one
  notion is deferred until a habitat runs both shapes for real.
