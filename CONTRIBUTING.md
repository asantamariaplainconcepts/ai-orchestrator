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
                                    ready-for-implementation + HOLD ──(human clears the hold)──▶ ready-for-implementation
                                    draft PR · HITL #1                                                (gate, WIP cap)
                                                                                                            │
   done ◀──/aio:sync── code-review ◀──(human clears the hold)── code-review + HOLD ◀──/aio:implement── in-progress ◀──┘
  (retro+archive+sync,                                          same PR, ready · HITL #2      (set before the first commit)
   lint, squash-merge)
```

Or, on one explicit invocation, with no review stage at all:

```
ready-for-proposal ──/aio:ship──▶ propose ─▶ implement ─▶ sync ─▶ done
                    (no hold applied, so none is ever cleared)
                                    │
                        any refusal ▼
                              HOLD + comment ──(a person clears it)──▶ resume with the staged command
```

One issue rides **one branch and one PR**, reviewed twice on that same PR — or, on the unattended
route, not reviewed at all ([DEC-068](#the-unattended-route-dec-068)). At each review stage the
reviewer's whole act is **removing one label** — see [The hold](#the-hold) below.

| Step | Command | What happens |
| --- | --- | --- |
| 1. Clarify | [`/aio:grill`](.claude/commands/aio/grill.md) | Interrogate an idea, a corpus use case, or an existing issue against the [Definition of Ready](docs/process/definition-of-ready.md). Gaps → commented by name + `status:needs-refinement`; met → `status:ready-for-proposal`. |
| 2. Propose | [`/aio:propose`](.claude/commands/aio/propose.md) | OpenSpec change on a fresh-based branch whose name ends with the change slug; opens a **draft PR**; sets `status:ready-for-implementation` **and the hold**, in one edit. **HITL #1** — the spec is reviewed before code exists. |
| 3. Validate | *(human)* | Correct the spec on the draft PR, then **remove the hold**. That is the whole act — no label to set; the next state is already there. |
| 4. Implement | [`/aio:implement`](.claude/commands/aio/implement.md) | `status:in-progress` **before** the first commit; code on the **same** branch and PR; marks the PR ready; sets `status:code-review` **and the hold**. **HITL #2** — code and observed behaviour. |
| 4b. Approve | *(human)* | **Remove the hold.** That is what lets `/aio:sync` run. |
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
on `ready-for-implementation`. The two **human review stages** on the one PR are marked by the hold
rather than by a state — see below. Exactly one `status:*` label per issue — the commands remove the
old one as they add the new.

`proposal-review` is set by no command since the hold replaced it. It stays among the nine, and any
issue already carrying it keeps it; whether the lifecycle should shrink is a separate decision.

The labels were provisioned **once**, manually, with `gh label create`. No committed script
recreates them, and no command invents a missing one — it stops and says which label is absent.
If a repo ever needs re-provisioning, that is a deliberate manual operation.

## The hold

An issue may carry a **hold** — the label named by `holdLabel` in
[`.claude/workflow.json`](.claude/workflow.json) — meaning *a person must act before anything else
does*. It is the same reserved constant the product uses on a Story, compared case-insensitively,
and it is never renamed per-repository.

The hold is **not** a `status:*` label. The two answer different questions: the status says where
the work is, the hold says whether anyone may take it further. A held issue still carries exactly
one of the nine.

| Command | Behaviour while held |
| --- | --- |
| `/aio:propose` | **Refuses** — before any branch or PR exists. |
| `/aio:implement` | **Refuses**, and checks the hold *before* the WIP gate, so a held issue consumes no slot and never shows up among the issues holding the cap. |
| `/aio:sync` | **Refuses** — nothing merged, archived, or written to the retro log. |
| `/aio:grill` | Runs. It still evaluates and comments, but sets no status — a hold blocks advancing, not talking. |
| `/aio:status` | Runs, and reports the hold first. It refuses nothing; it is read-only. |
| `/aio:refine` | Runs, unchanged. It is post-merge and gates nothing. |
| `/aio:ship` | **Refuses** at the start — its entry gate is `/aio:propose`'s. Mid-run it *applies* the hold to halt, and removes one never. |

**Clearing the hold is the approval.** At both review stages you remove that one label and nothing
else — the state the next command needs is already in place. `/aio:propose` sets
`status:ready-for-implementation` up front, so an issue reads that state while its spec is still
unreviewed; the hold is what makes it safe, and nothing can advance while it is on.

**No command, script, or workflow in this repository ever removes the hold.** Clearing it is a
person's act, always. That is the entire mechanism: it is what makes a hold trustworthy, and an
automation that could undo it would put you back to choosing among nine labels.

Like the nine, the hold label is provisioned **once**, by hand. A command that needs it and finds
it missing stops and says so.

If you use a GitHub Project, it is a **label-filtered saved view**. Nothing reconciles it: editing
a card's status field changes no lifecycle state. Change the label.

Bulk-seeding issues from [`docs/product/v1/`](docs/product/v1/) is fine — each seeded issue
still passes the grill individually to reach ready.

## Solo review path (DEC-016)

GitHub forbids approving your own pull request, so `/aio:sync` does **not** gate on a formal PR
approval. For a solo maintainer the recorded review is the **cleared hold plus the PR checklist**:
removing the hold at HITL #1 records the spec review, and removing it at HITL #2 — with the
Definition-of-Done boxes ticked — records the code review. Both are timestamped in the issue's
event log, which is a better record than the label transitions they replace. State the go-ahead
explicitly when syncing. When a second committer joins, this reverts to real approvals —
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

## The unattended route (DEC-068)

[`/aio:ship <issue>`](.claude/commands/aio/ship.md) carries a `ready-for-proposal` issue to `main` in
one run — propose, implement, sync — with **no review stage**. What it gives up is stated plainly:
**nobody reads the spec or the diff**, and CI is the only reviewer between a generated change and
`main`.

- **Your invocation is the authorisation.** It replaces the solo path's in-session go-ahead above,
  because an unattended run has nobody to ask. Nothing else authorises the merge, so taking this route
  is always a deliberate, attributable act.
- **No hold is applied, so none is ever cleared.** The three staged commands each carry an unattended
  clause: the status advances without the hold, and sync answers its three questions from the
  invocation. The hold pauses work *between* commands, and this route has no between — so the rule
  that nothing removes a hold survives untouched.
- **Any refusal becomes a halt**: the hold goes on, a comment says why, the `status:*` label stays put.
  You clear the hold and resume with the ordinary command for that label.
- **Every other gate is unchanged** — the status gates, the WIP cap, CI green before the close-out
  commit, the commitlint check on the squash message.
- **The record says it was unattended.** The PR body and the retro entry both say no human read the
  spec or the diff, and the retro's reflections are marked unconfirmed. That is what keeps the two
  routes distinguishable in the log, and this decision measurable.
- **No ADR is written unattended.** Shipping code nobody read is authorised; deciding architecture
  nobody read is not. A structural finding becomes a tracked issue for a person.

The staged route stays the default. This one is for work whose shape you already accepted at the
grill — see [ADR-0027](docs/adr/0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md)
for the evidence, the accepted risks, and the signals that would narrow it again.

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

## Local habitats (#250)

`aspire run` starts the **dev loop** by default: demo seeder, local secret store, Local locus
available, in-process Run execution. To rehearse the **server shape** — what an operator's
`docker compose up` composes: pods, Local locus declared out, no seeder — switch the habitat
parameter and run again:

```bash
dotnet user-secrets set Parameters:habitat server --project src/root/AiOrchestrator.AppHost
```

(`local` or unset switches back; any other value refuses at startup naming both.) Publishing
ignores the parameter — the compose artifact is always the server shape.

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
