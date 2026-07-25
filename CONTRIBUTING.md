# Contributing to AI Orchestrator

How work goes from idea to merged-and-synced. It is spec-first and runs through a project-owned
`/aio:*` command layer that wraps OpenSpec. New here? Start with [`README.md`](README.md) for
setup and [`ONBOARDING.md`](ONBOARDING.md) for the short tour. Working as an agent?
[`AGENTS.md`](AGENTS.md) is the same loop from that side.

## The loop

```
idea / use case ──/aio:grill──▶ needs-refinement ──(resolve gaps)──▶ ready-for-proposal
                   (DoR gate)          │                                    │
                                       └────────────/aio:grill──────────────┘
                                                                            │
                                                              /aio:propose (gate)
                                                                            ▼
                                              proposal-review ──(human validates)──▶ ready-for-implementation
                                              draft PR · HITL #1                          (gate, WIP cap)
                                                                                                │
   done ◀──/aio:sync── code-review ◀──/aio:implement── in-progress ◀───────────────────────────┘
  (retro+archive+sync,  same PR, ready · HITL #2   (set before the first commit)
   lint, squash-merge)
```

One issue rides **one branch and one PR**, reviewed twice on that same PR.

| Step | Command | What happens |
| --- | --- | --- |
| 1. Clarify | [`/aio:grill`](.claude/commands/aio/grill.md) | Interrogate an idea, a corpus use case, or an existing issue against the [Definition of Ready](docs/process/definition-of-ready.md). Gaps → commented by name + `status:needs-refinement`; met → `status:ready-for-proposal`. |
| 2. Propose | [`/aio:propose`](.claude/commands/aio/propose.md) | OpenSpec change on a fresh-based branch whose name ends with the change slug; opens a **draft PR**; `status:proposal-review`. **HITL #1** — the spec is reviewed before code exists. |
| 3. Validate | *(human)* | Correct the spec on the draft PR, then move to `status:ready-for-implementation`. |
| 4. Implement | [`/aio:implement`](.claude/commands/aio/implement.md) | `status:in-progress` **before** the first commit; code on the **same** branch and PR; marks the PR ready; `status:code-review`. **HITL #2** — code and observed behaviour. |
| 5. Sync | [`/aio:sync`](.claude/commands/aio/sync.md) | The only merge path. Verifies CI green *before* writing the `[skip ci]` close-out commit, appends the retro, archives + folds specs, **lints the squash subject and body**, squash-merges one commit, sets `status:done`. |
| 6. Refine | [`/aio:refine`](.claude/commands/aio/refine.md) | Post-merge findings only — appends a new retro entry, never rewrites one. |

[`/aio:status`](.claude/commands/aio/status.md) is read-only and tells you where an issue sits and
which command comes next. **The gate mechanics live in those command files** — this page links to
them deliberately so a command change cannot silently orphan the prose.

## Issue status convention

The lifecycle state is the `status:*` label and nothing else. Nine states in order:

`backlog` → `needs-refinement` → `ready-for-proposal` → `proposal-review` →
`ready-for-implementation` → `in-progress` → `code-review` → `done`, plus `blocked` (reachable
from any state).

Two are **machine gates**: `/aio:propose` runs only on `ready-for-proposal`, `/aio:implement` only
on `ready-for-implementation`. Two are **human review stages** on the one PR: `proposal-review`
(draft, spec) and `code-review` (ready, code). Exactly one `status:*` label per issue — the
commands remove the old one as they add the new.

The labels were provisioned **once**, manually, with `gh label create`. No committed script
recreates them, and no command invents a missing one — it stops and says which label is absent.
If a repo ever needs re-provisioning, that is a deliberate manual operation.

If you use a GitHub Project, it is a **label-filtered saved view**. Nothing reconciles it: editing
a card's status field changes no lifecycle state. Change the label.

Bulk-seeding issues from [`docs/product/mvp/`](docs/product/mvp/) is fine — each seeded issue
still passes the grill individually to reach ready.

## Solo review path (DEC-016)

GitHub forbids approving your own pull request, so `/aio:sync` does **not** gate on a formal PR
approval. For a solo maintainer the recorded review is the **label transition plus the PR
checklist**: moving an issue to `ready-for-implementation` records the spec review, and marking
the PR ready plus ticking the Definition-of-Done boxes records the code review. State the
go-ahead explicitly when syncing. When a second committer joins, this reverts to real approvals —
and the `[skip ci]` mechanism must be revisited at the same time (see below).

## The spec-less lane (DEC-025)

Hotfixes and pure infra or tooling changes have no spec delta, and forcing them through
grill→propose dead-ends at sync with nothing to archive. Label the issue `lane:spec-less` and:

- it still gets an issue, a branch, a PR, and green CI;
- it **skips** `/aio:propose` only;
- the retro entry is still mandatory;
- `/aio:sync` detects the label and skips the archive step.

Anything user-visible goes through the full loop. The lane is for work with genuinely no
behavioural delta — not for work in a hurry.

## Merge = archive = sync = retro

Closing a change happens **on the branch, before the merge**, so the squash puts a single commit
on `main` carrying the implementation, the retro entry, the synced `openspec/specs/`, and the
archived bundle. Two orderings in `/aio:sync` are load-bearing and were each paid for:

- **CI green is verified while the last implementation commit is still the PR head** — after the
  `[skip ci]` close-out commit exists, that SHA has no check runs and an empty rollup reads as
  "nothing failing".
- **The squash subject and body are linted before merging** — a squash message is composed at
  merge time on the platform, where no local hook can see it, and the CI check that would catch
  it runs only after the merge is irreversible.

**No branch protection is configured on `main`, deliberately.** A required status check would
deadlock on the `[skip ci]` commit, and required approval is impossible solo. The consequence is
real: nothing at the platform level prevents a direct push. The gates live in the commands and in
CI. Revisit this — together with `[skip ci]` — when a second committer appears.

## Conventions

- **Conventional Commits**, enforced by commitlint on commit-msg and again in CI, so `--no-verify`
  is still caught.
- **Formatting is not a review topic**: CSharpier (C#) and Prettier/ESLint (frontend) run on
  staged files and in the lint lane.
- **Module boundaries**: cross-module access via `.Contracts` only — MOD001–005 and CQS001 fail
  the build, and ArchTests catch what the analyzers structurally cannot.
- **All user-facing copy** comes from the typed i18n catalog; hardcoded JSX text fails lint.
- **Decisions**: record them in [`docs/adr/`](docs/adr/README.md). A lesson that recurs graduates
  to an ADR on its **second** occurrence, in the change that noticed it.
- **Append-only history**: retro entries and accepted ADRs are never rewritten — supersede.
