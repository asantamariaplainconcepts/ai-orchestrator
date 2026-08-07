# runtime-readiness — design

## Context

The pods panel (#254) already solved this exact shape for docker: a `BackgroundService` probe
(`AgentPodsProbe`) recording into a host-held snapshot (`AgentPodsHost`), a filtered read
(`GET /api/pods`), an environment chip and a panel whose copy carries the remedy as a copyable
command. Agent runtimes have none of it: `AgentRuntimeSelector` maps a name to a runtime and an
optional `CredentialSecretName`; the executor resolves the credential (failing with the secret's
name) and the runtime `Process.Start`s a CLI (failing with a raw ENOENT). Two asymmetries bite:
opencode's credential config normalizes whitespace→null, Claude's does not — and Claude
defaults to `anthropic-api-key` in code, so it cannot be switched off at all.

## Goals / Non-Goals

**Goals:**
- A probe per registered runtime: CLI present (answers `--version`), credential resolvable (or
  deliberately switched off) — on the pods panel's cadence pattern, with transitions logged.
- The panel and environment chip render runtime readiness with last-checked time and a copyable
  remedy; copy in i18n as contract.
- Run failures carry the remedy: binary + PATH + install command; secret + store + how to add.
- Empty/whitespace `CredentialSecretName` means no secret for BOTH runtimes; with none, the CLI
  runs with the machine's own session (which pods already mount by deliberate default, #246 D5).

**Non-Goals:**
- New auth mechanisms (OAuth flows, key-less pods); auto-installing CLIs; the conversation
  session runtime; changing which runtimes exist or how Automations select them.

## Decisions

**D1 — mirror the pods pattern, do not generalize it prematurely.** A `AgentRuntimesProbe` +
snapshot holder + read, shaped like `AgentPodsProbe`/`AgentPodsHost`/`GET /api/pods` — a sibling,
not an abstraction over both. Two similar implementations are cheaper than one wrong seam; the
third occurrence can graduate to a shared shape (the repo's own graduation rule).

**D2 — the probe asks the CLI, not the filesystem.** `<command> --version` with a bounded
timeout proves presence AND executability in one call (the pods probe's exit-code-only rule: no
output parsing, so a CLI changing wording cannot turn the host red). Credential readiness is
`Resolve(name)` succeeding, probed against the same store the executor uses — never a config
inspection (ADR-0004: a green config proves nothing).

**D3 — remedy sentences live beside the probe's verdicts, in one place per cause.** The same
sentence reaches the panel (via the read) and the Run failure (via the executor), so the two
cannot drift: CLI missing → names the binary, that PATH lookup failed, and the pinned install
command (the versions the repo already pins); secret missing → names the secret and the store,
and where a value goes (BR-010: names always, values never).

**D4 — empty means off, for both runtimes, spelled once.** `AddAgentRuntime` normalizes both
credential configs with the same whitespace→null rule opencode already has. With null, the
executor resolves nothing and the runtime spawns the CLI without injecting `ANTHROPIC_API_KEY` —
verifying (task) that an empty-string env var is not exported to the child, which would shadow
the CLI's own session auth.

**D5 — proof is a real machine walking the matrix.** Fresh state on this machine: CLI absent →
panel says so with the command; install → panel flips ready without restart (probe cadence);
secret configured-but-absent → panel names it; switch off → a real Run completes on session
auth. #99's lesson applies to environment truth as much as to compose truth.

## Risks / Trade-offs

- [A probe spawning CLIs every 30s costs process churn] → `--version` is milliseconds; the pods
  probe set the precedent and cadence constant; reuse it.
- [An empty ANTHROPIC_API_KEY exported to the child shadows session auth] → D4 makes the
  no-secret path skip the variable entirely; a test asserts the child env has no empty key.
- [The remedy install command drifts from the pinned versions] → the pins live in one constant
  the copy reads; the ConversationSession Dockerfile cites the same versions.
- [Panel surface growth (i18n, design)] → route through the aio-design skill and the existing
  pods panel components; the chip/panel pattern is already tokenized.

## Migration Plan

One change: normalization + remedy sentences (executor/runtime), probe + snapshot + read,
panel/chip UI, then the machine-matrix proof. Rollback is one revert; no data, no config
contract breaks (an operator who set a secret name keeps exactly today's behavior).

## Open Questions

(none)
