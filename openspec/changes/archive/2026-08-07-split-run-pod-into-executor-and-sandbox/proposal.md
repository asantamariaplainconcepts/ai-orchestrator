## Why

A Run's pod is the **whole DispatchWorker image** (#246): the .NET module host, the database
connection string, the secret-store paths and the host's `~/.claude` all sit in the same
container as the agent CLI that reads a third-party repository. Isolation exists — but the thing
isolated is the orchestrator's own execution machinery, so a prompt-injected agent is already
next to the database credential without escaping anything.

The [sbx spike](../archive/2026-08-07-spike-sbx-sandbox/findings.md) (verdict GO, 2026-08-07)
exercised the alternative on real hardware: an agent working inside a microVM, reading and
editing a repository whose diff the host then saw, under a three-domain allowlist, while
`GITHUB_TOKEN` **inside the sandbox was empty** — and `git ls-remote` against the private
product repository still succeeded, because the credential is injected at egress by a host-side
proxy. The agent cannot leak what it never holds. That is BR-010's end state, and today's pod
cannot reach it because the pod must hold credentials to do its orchestration half.

## What Changes

- **Isolation moves one seam inward.** Today the *dispatch* seam decides where a Run's whole
  worker runs (`IDispatchedRunHandler` → pod). This change puts the boundary at the *runtime*
  seam instead: `IAgentRuntime` gains an implementation that runs the agent CLI in a sandbox
  while the executor — Run lifecycle, Contracts reads, secret resolution, state writes — stays
  in the worker process, outside.
- **A sandbox carries the workspace and the agent CLI, and nothing else.** No connection string,
  no secret-store path, no module host. What the agent needs to authenticate arrives by the
  driver's own mechanism (sbx: keychain + egress injection), and the contract admits drivers
  that cannot inject, which must then pass values as today.
- **The launcher is chosen by configuration presence**, exactly as `Dispatch:PodImage` selects
  pods (ADR-0010). Nothing named keeps today's in-process `Process.Start`, unchanged.
- **sbx is the first driver**, proven on macOS. Streaming output (`OnOutput`, #96) and the two
  runtimes (Claude Code, opencode — DEC-012/DEC-044) survive the move.
- **Runtime readiness (#279) learns the new failure modes**: the sbx daemon, the host's Docker
  identity and the network policy are new things that can be absent, and each must fail naming
  its remedy rather than as a raw process error.
- Not **BREAKING**: the queue message schema, the Aspire graph, `IAgentRuntime`'s callers and
  the existing pod substrate are all untouched when the sandbox launcher is not configured.

### Deliberately not in this change

- **Selfhost and Azure adoption.** All spike evidence is macOS; the Linux/KVM leg, headless
  `sbx login` enrolment and nested-virtualization prerequisites are unverified. This change
  makes the dev loop real and leaves habitat rollout to a follow-up that starts by verifying
  them (findings H6).
- **Retiring the pod substrate.** It stays exactly as specified. Whether the two mechanisms
  should ever compose is a design question, not a scope promise.
- **Run previews over `sbx ports`** (findings: "Run previews are nearly free") — a separate
  capability, noted so it is not smuggled in here.

## Capabilities

### New Capabilities

- `agent-sandboxing`: where a Run's agent executes, what may enter that boundary, and how a
  credential reaches the agent without entering it.

### Modified Capabilities

- `agent-execution`: the runtime seam gains a sandboxed implementation — the requirement that a
  dispatched Run executes through the seam holds, but *where the CLI process lives* becomes a
  habitat's choice rather than always this process. Runtime readiness (`the agent runtimes are
  observable where they run`) extends to the sandbox host's own preconditions.

## Impact

- **Code**: `src/shared/AiOrchestrator.BuildingBlocks/Agents/` (the seam and its instruction
  record), `src/shared/AiOrchestrator.ServiceDefaults/Agents/` (the new driver beside
  `ClaudeCodeHeadlessRuntime` and `OpenCodeRuntime`, plus `AgentRuntimesProbe`),
  `ConversationRuntimeComposition`/`DispatchComposition` for the presence-driven registration.
- **Configuration**: one new key group naming the sandbox launcher; absent everywhere by
  default, so every existing habitat behaves identically.
- **Operator surface**: a habitat that opts in acquires host prerequisites the product must
  state — the sbx daemon, a Docker identity per host, an initialized network policy. The
  environment panel is where they become visible.
- **Tests**: unit coverage for the presence-driven selection and the refusal sentences;
  functional coverage for the streaming contract. Exercising a real sandbox in CI is out of
  reach (it needs KVM/a VMM), so the proof obligation is a documented manual exercise on the
  dev machine — the ADR-0001 discipline the spike already followed.
