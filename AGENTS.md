# AGENTS.md — AI Orchestrator

Canonical, tool-neutral instructions for AI coding agents (Claude Code, opencode, GitHub
Copilot, …) working in this repository. `CLAUDE.md` and `.github/copilot-instructions.md` just
point here. Keep this file a **router**: it says where things are and how we work — it does not
restate specs, product facts, or decisions.

## What AI Orchestrator is

An open-source web application that connects project backlogs (GitHub, Azure DevOps) to AI
agents: users configure **Automations** ("story with trigger label X → an **Agent** performs
action Y") and an Agent executes them in a per-Run sandbox, governed from the website — one
product in two habitats (deployment and self-host, DEC-066). Backend is a **.NET modular
monolith**; frontend is a **React SPA** served same-origin by the host. Product truth lives in
`docs/product/v1/` (stable IDs: ACT/BC/UC/BR/DEC/OPN) — coined vocabulary is locked by
DEC-005: *Agent* (never "pod"), *Connector*, *Automation*, *Run*, *Plan*.

## Where things live

| You need… | Look in |
| --- | --- |
| Product truth (what and why, stable IDs) | `docs/product/v1/` (history: `docs/product/mvp/`) |
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
label is the **sole** lifecycle state. (`proposal-review` is now set by no command — the hold
replaced it; see below.)

Alongside it, an issue may carry a **hold** — the label named by `holdLabel` in
`.claude/workflow.json`, meaning a person must act before anything else does. It is **not** a
`status:*` label: a held issue still carries exactly one of the nine. `/aio:propose`,
`/aio:implement` and `/aio:sync` **refuse** while it is on (implement checks it *before* the WIP
gate, so a held issue consumes no slot); `/aio:grill` still evaluates and comments but sets no
status; `/aio:status` reports it and refuses nothing; `/aio:refine` is unaffected. `/aio:ship`
applies **no** hold on its happy path — it needs none, having no pause between stages — and applies
one to halt. **Nothing in this repository ever removes a hold** — clearing it is a person's act,
always, on every route.

1. **`/aio:grill`** — interrogate an idea (or an existing issue, or a `docs/product/v1/` item) to
   the Definition of Ready, then create/advance the issue. Items depending on an open `OPN-*`
   decision are blocked, never guessed at.
2. **`/aio:propose`** — `ready-for-proposal` issue → branch (name ends with the change slug, fresh
   `origin/main` base) → OpenSpec change → **draft PR** → sets `ready-for-implementation` **and the
   hold**, in one edit. **HITL #1**: the spec is reviewed as text.
3. Reviewer **removes the hold**. That is the whole act — no label to set; the gating state is
   already there. Clearing the hold *is* the approval.
4. **`/aio:implement`** — refuses a held issue **before** the WIP gate, then refuses beyond the WIP
   limit (`.claude/workflow.json`); sets `in-progress` before its first commit; same branch, same
   PR, marks it ready → `code-review` **plus the hold**. **HITL #2**: code and observed behaviour,
   released the same way.
5. **`/aio:sync`** — the only merge path. Verifies CI green on the PR head **before** creating the
   `[skip ci]` close-out commit; retro + archive + spec-sync on the branch; **lints the squash
   subject and body against commitlint before merging**; squash-merges exactly one commit whose
   subject is the PR title; sets `done`.
6. **`/aio:refine`** — append a post-merge retro finding.

`/aio:status` is read-only and reports where an issue sits plus the next command.

**`/aio:ship`** — the unattended route (DEC-068, ADR-0027): steps 2, 4 and 5 in one run, with **no**
review stage. It owns no gates; it runs those three commands in *unattended mode*, whose whole content
is that the status advances without the hold and sync answers its three human questions from the
invocation. The invocation is the recorded authorisation, in place of DEC-016's go-ahead. Any refusal
becomes a **halt**: the hold goes on, a comment says why, the `status:*` label stays — a person clears
it and resumes with the ordinary staged command. Every other gate is untouched, no ADR is written
unattended, and the PR body and retro entry both record that nobody read the spec or the diff.

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
- **Don't trust filtered command output where it gates a decision** — a config read, `git` porcelain,
  a build or test verdict. A filtering proxy can drop content and still exit 0; re-run under
  `rtk proxy` when the exact bytes matter. See [A filtering command proxy can lie to you](#a-filtering-command-proxy-can-lie-to-you-341).
- **Assert your worktree** (`git rev-parse --show-toplevel` matches the session directory) before
  any mutating git batch.
- **Confirm shared-state actions** (issues, labels, PRs, retro log) before executing them.
- **All user-facing copy via the typed i18n catalog** (`src/frontend/shared/i18n/`); hardcoded
  JSX text fails lint.
- **Skills are one-responsibility and never call each other**; commands orchestrate. Author new
  skills against `.claude/skills/writing-great-skills/`.
- **Append-only history**: never rewrite retro entries or accepted ADRs; supersede.

## A filtering command proxy can lie to you (#341)

Some machines rewrite an agent's shell commands through a token-saving proxy (`rtk`, via a global
Claude Code hook). **It sometimes drops content while still exiting 0**, and the truncated result
stays syntactically valid — so nothing signals the loss. This is not hypothetical; it is recorded
four times in [`docs/process/retro-log.md`](docs/process/retro-log.md):

| What was run | What came back | What it nearly caused |
|---|---|---|
| `cat .claude/workflow.json` | the file **without `holdLabel`** | hardcoding `hitl`, or skipping the hold — breaking the gate that stops work proceeding without a person |
| `git diff --name-only origin/main...HEAD > file` | formatted prose with a `--- Changes ---` header | a branch-overlap check computing **0** changed files where there were **23** |
| `rtk pnpm build` | success | a broken build reported as green |
| `rtk prettier --check` | clean | a non-zero exit read as passing |

**The rule.** When a command's output *gates a decision* — a config read, `git` porcelain, a build or
test result you are about to act on — you must know the bytes are the command's own. Verify:

```bash
node .config/proxy/verify-command-proxy.mjs
```

It reproduces the two recorded failures rather than reading configuration, because a green config
proves nothing here (ADR-0004): the proxy's passthrough list is a **token-wise prefix match with no
regex**, so a pattern can look correct and match nothing at all.

**What the passthrough list cannot cover** — for these, `rtk proxy <cmd>` is the only guard, and it
stays a discipline rather than a setting:

- a flag carrying a value (`git log --format=%H`) — only whole tokens match;
- a redirection (`> file`) or a pipe into a parser (`| jq`, `| wc`, `| comm`) — which is exactly how
  the 23-files-read-as-0 failure happened;
- a lossy exit 0 from a build or test filter.

**Do not edit the hook script** to fix this. The proxy hashes its own hook and refuses to execute if
it changed, which silently disables every rewrite — the same class of invisible failure, in the
opposite direction. Narrow `[hooks] exclude_commands` in the proxy's config instead.

## Telemetry

Retro time comes from OpenTelemetry data captured locally. Nothing recovers telemetry that was never
written, so the check **runs itself at session start** (`--preflight`, wired into `SessionStart` in
`.claude/settings.json`) rather than relying on anyone remembering it: silent when healthy, loud with
the failing check and its remedy when not. It **never blocks** — the cost is a lost measurement, not
an incorrect change.

The instruction to check it manually lived here before and did not work: it was in place for both of
the occurrences it was meant to prevent (#331, #332), and #332 lost all of its telemetry anyway.

For the full report at any time:

```bash
node .config/otel/verify-telemetry.mjs
```

The endpoint must be exported from **`~/.zshenv`**, not `~/.zshrc`: zsh reads `.zshrc` only for
interactive shells, so a non-interactive client inherits nothing and the check still reads `UNSET`.

Setup and the known desktop-client limitation: [docs/process/telemetry-setup.md](docs/process/telemetry-setup.md).
