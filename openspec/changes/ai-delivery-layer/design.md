# Design — ai-delivery-layer

## Verified reality (checked, not assumed)

- `gh` 2.x authenticated as `asantamariaplainconcepts`; the repo is public, so branch protection
  and rulesets are available on the free plan.
- **No `status:*` labels exist yet.** Phase 3 creates them. Consequence for this change: commands
  must fail loudly on a missing label rather than silently proceeding or creating one.
- **The solo path is real, not theoretical** (DEC-016): GitHub forbids self-approval, so
  `/aio:sync` cannot gate on "PR approved". It gates on the label transition and a green check
  rollup instead.
- OpenSpec 1.6.0 is installed and the archive mechanics work end to end — exercised for real by
  the Phase 1 sync, not inferred.

## Decisions

### D1 — Three tiers, and skills never call skills

```
AGENTS.md (tool-neutral router)
   └── /aio:*   commands   ← orchestration + gates (the public API)
         └── skills       ← atomic, one responsibility, never call each other
               └── tools  ← OpenSpec CLI, gh, git, telemetry scripts
```

**Rationale:** composition lives in exactly one place. Changing the workflow means editing a
command, never untangling skill-to-skill coupling. **Rejected:** letting skills invoke skills —
it reads as convenient and produces a graph nobody can reason about at the moment a gate needs to
change.

### D2 — Gates are refusals, and they point forward

A command that cannot run says which command unblocks it (`not ready-for-proposal → run
/aio:grill 12`), never a bare refusal. Lifecycle gates never warn-and-continue; advisory checks
(branch-footprint overlap) may warn without blocking. The distinction is explicit per gate in the
spec, so nobody has to guess which kind they are looking at.

### D3 — Attribution joins on `session.id`, and nothing else

DEC-022, inherited from the kit's ADR-0008. `OTEL_RESOURCE_ATTRIBUTES` is read once at process
start, so the single session that runs grill → propose → implement → sync **can never tag
itself**; branch-name heuristics break the moment a branch is named differently from its change.
A SessionStart hook appends `{ts, session_id, cwd, branch, change, project}` to `sessions.jsonl`
and reporting joins on `session.id`.

Two consequences we accept rather than paper over: the Collector stamps `project=` server-side
(the desktop client drops resource attributes), and **human time will still under-record**,
because steering happens in chat. The PR "Time invested" section stays the human-effort record —
telemetry supplements it, never replaces it.

### D4 — Tunable process values have exactly one home

The WIP limit (DEC-017: 2) lived in six places in the source project. Here it lives in
`.claude/workflow.json`, and every command reads it. This is doc 05's known gap #6, implemented
rather than inherited.

### D5 — Known gaps ship as implemented guards, not as a list

All seven of doc 05's, plus ours. Each becomes a testable requirement rather than advice:

| Gap | Where it lands |
|---|---|
| Worktree preflight | every mutating command asserts `git rev-parse --show-toplevel` first |
| Branch-name normalization | `/aio:propose` requires the branch to end with the change slug |
| Fresh-base check | `/aio:propose` verifies the base is current `origin/main` and targets the real default branch |
| ADR numbering against `origin/main` | `write-adr` allocates there; `/aio:sync` re-verifies |
| Pipe discipline | gating shell steps set `pipefail` or check exit codes explicitly |
| Single source for tunables | D4 |
| Overlap check re-run at sync, widened to `code-review` PRs | `/aio:sync` precondition |
| **Squash message validated before merge (ours)** | `/aio:sync` lints the subject and body it is about to use |

### D6 — Commands are Markdown, deliberately

They are prompts, not scripts: an agent reads and executes them. That is what makes them portable
across the three runtimes in DEC-018 and reviewable as text in a PR. **Rejected:** implementing
the gates as shell scripts the agent shells out to — it would make them non-portable and would
hide the workflow from review behind an opaque call.

**The honest limitation this creates:** a Markdown gate is enforced by the agent's compliance, not
by the machine. Real enforcement lives in CI and in the analyzers — the places a human cannot
skip. Commands make the right path the easy path and make deviation visible; they are not a
substitute for the machine gates, and this change does not pretend otherwise. Where a gate can
also be a CI check, it should eventually become one.

### D7 — Vendored work keeps its licence

`writing-great-skills` is MIT (Matt Pocock). It travels with its `NOTICE`, and our adaptations are
recorded in that file. The repo is public, which makes this a legal requirement, not a courtesy.

## Risks

- **Skill/command sprawl.** Mitigation: every skill we author is reviewed against
  `writing-great-skills`, and the spec caps this change at the skills the six commands actually
  need. Speculative skills are out of scope by the same rule that kept `Backlog` and `Agents` from
  being scaffolded empty in Phase 1.
- **Commands referencing Phase 3 artifacts that do not exist yet.** Mitigation: they reference by
  path and fail loudly. The Phase 3 change is what turns those failures green; until then the
  failure is accurate.
