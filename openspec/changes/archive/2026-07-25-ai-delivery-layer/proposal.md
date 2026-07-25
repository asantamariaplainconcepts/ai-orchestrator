# Proposal: ai-delivery-layer

## Why

The scaffolding change gave the project machine-checkable guardrails but no *workflow*. Today the
loop exists only as prose in this repo's docs and as habit in a session: the Phase 1 sync was
performed by hand, and the one step no human hook could see — the squash commit message — is
exactly the step that broke ([retro-log](../../../docs/process/retro-log.md), post-merge finding).
That is the argument for this change in one sentence: **rules that live in prose get skipped;
rules that live in a command's refusal do not.**

This change builds the agent-facing layer — instructions, skills, wrapper commands, telemetry —
as a reviewed change, the same way the framework it came from built its own.

## What Changes

Four new capabilities (delta specs under `specs/`):

1. **agent-instructions** — `AGENTS.md` as the single tool-neutral router, plus pointer files for
   the three runtimes in DEC-018 (Claude Code, opencode, GitHub Copilot). Every pointer must
   resolve to `AGENTS.md`; a pointer aimed anywhere else is a drift vector and is treated as a
   defect, not a preference.
2. **skill-catalog** — atomic skills under `.claude/skills/<name>/SKILL.md`, each with one
   responsibility, none calling another. Mutating skills confirm before touching shared state;
   refusals name the command that unblocks them. Includes vendoring `writing-great-skills`
   (MIT, Matt Pocock) **with its NOTICE**, adopted as the review standard for every skill we author.
3. **workflow-commands** — the `/aio:*` layer: `grill`, `propose`, `implement`, `sync`, `refine`,
   `status`. Commands orchestrate skills and own every gate; OpenSpec stays an implementation
   detail behind `/opsx:*`. Carries the full doc-05 catalog **including its "known gaps"**, plus
   one gap this project discovered on its own (below).
4. **usage-telemetry** — OTel Collector as the durable system of record, attribution by
   **join on `session.id`** (DEC-022), with `usage.jsonl` appended never truncated. Data stays
   out of the public repo.

## The gap this project found, and why it ships as a gate here

Doc 05's "known gaps" are inherited. This one is ours: the Phase 1 squash commit failed commitlint
on `main`, because a squash message is authored at merge time on the platform where **no local
hook can see it**, and the only gate that checks it runs after the merge is irreversible.

`/aio:sync` already sets the squash subject and body explicitly, so it is the single place that can
validate them while the merge is still preventable. It will. Generalized as a requirement:
*every artifact that will become main's history is validated before the merge, not after.*

## Out of scope (deliberate)

- **Ceremonies content** — the nine `status:*` labels, the Definition of Ready document,
  `CONTRIBUTING.md`, `ONBOARDING.md`, the ADR template (bootstrap Phase 3). The commands here
  *depend* on those artifacts and will reference them by path; Phase 3 writes them. Where a
  command needs a label that does not exist yet, it fails loudly rather than inventing one.
- **The design-router skill** (Phase 4 — no canonical design system exists to route to).
- **`verify`** and the grill rubric's product bindings beyond citing the existing corpus IDs.
- Any change to the application code from `project-scaffolding`.

## Impact

- New: `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, `opencode.json`,
  `.claude/skills/**`, `.claude/commands/**`, `.claude/settings.json`, `.config/otel/**`.
- Affected specs: four ADDED. No existing capability is modified — this layer sits beside the
  application, not inside it.
- `.gitignore` gains the telemetry data paths (DEC-022: the repo is public and telemetry carries
  user emails).
- No application code changes, so the code lanes of CI are untouched by design.
