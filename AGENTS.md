# AGENTS.md — AI Orchestrator

Canonical, tool-neutral instructions for AI coding agents (Claude Code, opencode, GitHub
Copilot, …) working in this repository. `CLAUDE.md` and `.github/copilot-instructions.md` just
point here. Keep this file a **router**: it says where things are and how we work — it does not
restate specs, product facts, or decisions.

## What AI Orchestrator is

An internal web application that connects project backlogs (GitHub, Azure DevOps) to AI agents:
users configure **Automations** ("story with trigger label X → an **Agent** performs action Y")
and KEDA-scaled Azure Container Apps Jobs execute them, governed from the website. Backend is a
**.NET modular monolith**; frontend is a **React SPA** served same-origin by the host. Product
truth lives in `docs/product/mvp/` (stable IDs: ACT/BC/UC/BR/DEC/OPN) — coined vocabulary is
locked by DEC-005: *Agent* (never "pod"), *Connector*, *Automation*, *Run*, *Plan*.

## Where things live

| You need… | Look in |
| --- | --- |
| Product truth (what and why, stable IDs) | `docs/product/mvp/` |
| Technical architecture (how, high level) | `ARCHITECTURE.md` |
| A module's context | `src/modules/<Module>/context.md` |
| **Behavioural source of truth** | `openspec/specs/` |
| In-flight changes | `openspec/changes/` |
| Decisions | `docs/adr/` — decisions, immutable once accepted |
| Retro log (append-only) | `docs/process/retro-log.md` |
| Definition of Ready | `docs/process/definition-of-ready.md` |
| How we work (the loop, for humans) | `CONTRIBUTING.md` |
| Workflow tunables (WIP limit, …) | `.claude/workflow.json` |
| Design contract (read before UI work) | `DESIGN.md` — generated from canonical `docs/design-system/` |
| Skill-authoring craft | `.claude/skills/writing-great-skills/` |
| Bootstrap state | `BOOTSTRAP.md`, `BOOTSTRAP-CHECKLIST.md` |

`openspec/specs/` owns behaviour — do not restate requirements here or in other docs; link.

## Three-runtime sharing (Claude Code, opencode, GitHub Copilot)

`.claude/` is the canonical home for agent configuration; all runtimes share it:

| Layer | Canonical location | How each runtime sees it |
| --- | --- | --- |
| **Rules** | `AGENTS.md` | Claude Code: `CLAUDE.md` → here · opencode: reads `AGENTS.md` natively · Copilot: `.github/copilot-instructions.md` → here |
| **Skills** | `.claude/skills/*/SKILL.md` | Claude Code: native · opencode: native (Claude-compat) · Copilot: N/A |
| **Commands** | `.claude/commands/**/*.md` | Claude Code: native (`/aio:grill`) · opencode: Claude-compat · Copilot: N/A |
| **Project config** | `.claude/settings.json` | Claude Code: native · opencode: `opencode.json` at repo root · Copilot: N/A |

**Known gap — telemetry.** The OTel session hooks are Claude Code lifecycle; opencode sessions
run fine but are not auto-mapped in `sessions.jsonl`.

**Skill and command text is tool-neutral.** No runtime-specific tool names — natural-language
instructions any agent can interpret.

## The workflow loop

Spec-first, through the project-owned `/aio:*` commands that wrap OpenSpec. One issue rides **one
branch and one PR**, reviewed twice. The lifecycle is nine `status:*` states:
`backlog → needs-refinement → ready-for-proposal → proposal-review → ready-for-implementation →
in-progress → code-review → done` (plus `blocked`, reachable from any state). The `status:*`
label is the **sole** lifecycle state.

1. **`/aio:grill`** — interrogate an idea (or an existing issue, or a `docs/product/mvp/` item) to
   the Definition of Ready, then create/advance the issue. Items depending on an open `OPN-*`
   decision are blocked, never guessed at.
2. **`/aio:propose`** — `ready-for-proposal` issue → branch (name ends with the change slug, fresh
   `origin/main` base) → OpenSpec change → **draft PR**. **HITL #1**: the spec is reviewed as text.
3. Reviewer moves the label → `ready-for-implementation`.
4. **`/aio:implement`** — refuses beyond the WIP limit (`.claude/workflow.json`); sets
   `in-progress` before its first commit; same branch, same PR, marks it ready → `code-review`.
   **HITL #2**: code and observed behaviour.
5. **`/aio:sync`** — the only merge path. Verifies CI green on the PR head **before** creating the
   `[skip ci]` close-out commit; retro + archive + spec-sync on the branch; **lints the squash
   subject and body against commitlint before merging**; squash-merges exactly one commit whose
   subject is the PR title; sets `done`.
6. **`/aio:refine`** — append a post-merge retro finding.

`/aio:status` is read-only and reports where an issue sits plus the next command.

**Solo path (DEC-016):** GitHub forbids self-approval, so review gates are recorded as the label
transition + the PR checklist, not as a formal PR approval.

**Spec-less lane (DEC-025):** hotfixes and pure infra/tooling may skip grill→propose — label the
issue `lane:spec-less`, branch + PR + CI as normal, retro entry still mandatory, nothing to
archive at sync.

## House rules for agents

- **Respect module boundaries.** Cross-module access via `.Contracts` only; MOD001–005/CQS001
  fail the build otherwise. ArchTests catch what analyzers structurally cannot (see
  `ARCHITECTURE.md`).
- **Don't skip the gates.** No proposing an issue that isn't ready; no implementing an
  unvalidated proposal; no exceeding the WIP limit; no merge outside `/aio:sync`.
- **Verify infrastructure claims by exercising them** — a config existing or a step passing once
  is not evidence it works now (Phase 1's E2E lane proved this twice).
- **Assert your worktree** (`git rev-parse --show-toplevel` matches the session directory) before
  any mutating git batch.
- **Confirm shared-state actions** (issues, labels, PRs, retro log) before executing them.
- **All user-facing copy via the typed i18n catalog** (`src/frontend/shared/i18n/`); hardcoded
  JSX text fails lint.
- **Skills are one-responsibility and never call each other**; commands orchestrate. Author new
  skills against `.claude/skills/writing-great-skills/`.
- **Append-only history**: never rewrite retro entries or accepted ADRs; supersede.

## Telemetry

Retro time comes from OpenTelemetry data captured locally. **Check it works before starting a
change**, not at the retro — nothing recovers telemetry that was never written:

```bash
node .config/otel/verify-telemetry.mjs
```

Setup and the known desktop-client limitation: [docs/process/telemetry-setup.md](docs/process/telemetry-setup.md).
