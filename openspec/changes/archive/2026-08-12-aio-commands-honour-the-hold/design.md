## Context

The `/aio:*` commands are documentation the agent executes: markdown under `.claude/commands/aio/`
orchestrating single-responsibility skills under `.claude/skills/`. There is no application code in
this change — the "implementation" is command text, one JSON key, and the docs a reviewer reads.

Today `/aio:propose` ends at `status:proposal-review` and waits for a human to *set*
`status:ready-for-implementation`; `/aio:implement` ends at `status:code-review` and waits for a human
to run `/aio:sync`. Both hand-offs are "pick the right label from nine", and both stall. Issue #323
replaces both with "remove one label".

The hold's name and meaning come from [#321](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/321)
(`hold-replaces-the-plan-gate`, `status:in-progress`): the reserved label `hitl`, compared
case-insensitively (DEC-056), a fixed constant no Project may rename, meaning *a person must act
before anything else does*. This change takes that definition as given and applies it to the
repository's own loop, which #321's `RunCreator` never sees.

**Verified against reality before writing this design** (ADR-0006 discipline — a config existing is
not evidence it works):

- `.claude/workflow.json` today holds `wipLimit`, `squashBodyMaxLineLength`, `lifecycleLabels`
  (the nine) and `specLessLaneLabel`. It has no `holdLabel`.
- `gh label list` on this repository returns **no `hitl` label**. It has to be provisioned by hand
  before any of this runs — `issue-lifecycle` forbids a command creating it.
- `read-issue` already fetches `labels` in its single `gh issue view` call, so returning the hold
  costs no extra call.
- `src/modules/Projects/AiOrchestrator.Modules.Projects/Starter/workflow/{grill,implement,propose,
  refine,status,sync}.md` are **byte-identical** to their `.claude/commands/aio/` counterparts
  (verified with `diff` on all six). They are `EmbeddedResource`s in
  `AiOrchestrator.Modules.Projects.csproj`, shipped as the spec-first starter tier — the loop this
  product installs into other people's repositories.
- **No test gates that identity.** `StarterCatalogue_Should_Constraint.cs` checks frontmatter,
  bodies, path collisions and wiring; nothing compares the two copies. Drift is possible today and
  would not be caught.
- The starter tier's `prerequisites` block seeds `definition-of-ready.md`, `backlog-shaping-rules.md`,
  `retro-log.md`, `product-context.md` and an `openspec` config. It does **not** seed
  `.claude/workflow.json`.

## Goals / Non-Goals

**Goals:**

- A held issue cannot be advanced by any `/aio:*` command, by accident or by a command run out of
  order — and the refusal names the hold and who clears it.
- A reviewer's entire act at each human stage is removing one label.
- The hold's name lives in exactly one file.
- `/aio:status` becomes more useful under a hold, not less: it is where a confused reviewer looks.
- The starter catalogue keeps telling the truth about the loop this repository runs.

**Non-Goals:**

- Retiring `status:proposal-review` from the nine states. AC 11 leaves it unset by any command, hence
  unused; whether the lifecycle should shrink is a separate decision and a separate issue.
- The product-side hold (#321) — `RunCreator`, Automations, marks.
- Any automation that clears a hold.
- Renaming the label per-repository.
- Adding the missing drift gate between `.claude/commands/aio/` and the starter copies (see Open
  Questions).

## Decisions

### D1 — `holdLabel` is a string in `.claude/workflow.json`, not a new file

`workflow.json` already exists as "the single home for tunable process values" and is already read by
`/aio:implement` for `wipLimit`. Adding one key keeps that promise literal and gives the AC 1 check a
trivial form: grep the repository for the literal `hitl` outside `workflow.json` and expect nothing.

*Rejected:* a `.claude/labels.json`, or reusing `lifecycleLabels`. The first splits a home that is
already one file; the second is worse than cosmetic — it would make the hold *look* like a lifecycle
label to every reader and to `set-issue-status`, which is exactly the confusion AC 3 forbids.

### D2 — the hold is checked in `read-issue`, enforced in the commands

`read-issue` returns `held: true|false` in its structured result and stays read-only. Each mutating
command decides what to do with it. This keeps the skill's one responsibility intact and lets the
three refusals differ in wording and in *where* they sit in the step order — which matters, because
AC 6 is specifically about ordering.

*Rejected:* refusing inside `read-issue`. A read-only skill that can abort a command is no longer
read-only, and `/aio:status` and `/aio:grill` legitimately need to read a held issue and continue.

### D3 — the hold is checked before the WIP gate in `/aio:implement`, and first in all three

`/aio:implement` gains the hold check as **step 2.5**, between `read-issue` and the WIP gate. A held
issue must not consume a WIP slot, must not appear in the list of issues holding the cap, and must
not produce a refusal that blames the cap for a hold. In `/aio:propose` and `/aio:sync` the hold check
is likewise the first gate after `read-issue`, so the reported reason is always the true one.

The ordering is normative, not stylistic: a hold-refusal reported as a WIP-refusal sends the reviewer
to `/aio:sync` on an unrelated issue.

### D4 — `/aio:propose` sets `status:ready-for-implementation` + the hold, in one advance

The alternative — keep setting `status:proposal-review` and have the reviewer clear the hold *and*
move the state — reintroduces the two-act problem this change exists to remove. Setting the
destination state up front and holding it means the reviewer's removal is self-evidently the
approval, and `/aio:implement` finds its gate already satisfied (AC 12).

The visible cost: an issue reads `ready-for-implementation` while its spec is still unreviewed. The
hold is what makes that honest — the state says where the work is, the hold says nobody may take it
further. This is the same split #321's spec draws for Stories, deliberately.

`set-issue-status` performs the status transition; the hold is applied in the **same**
`gh issue edit` invocation, so a held issue is never briefly unheld at
`status:ready-for-implementation`. This ordering is the one genuinely race-sensitive detail in the
change.

### D5 — the unaffected commands say so out loud

`/aio:grill`, `/aio:status` and `/aio:refine` each gain an explicit sentence about the hold, even
though their behaviour is unchanged (ACs 8, 9, 10). Silence in a command file reads as an oversight to
the next person editing it, and the next person would "fix" it by adding a refusal — turning a hold
into a gag.

### D6 — the six starter copies are mirrored in the same change

They are byte-identical today and nothing enforces it. Editing `.claude/commands/aio/*.md` without
editing `src/modules/Projects/.../Starter/workflow/*.md` would ship a catalogue that installs a loop
this repository no longer runs, with no test to catch it.

This makes the change touch `src/` — which issue #323's dependency note did not anticipate when it
said "surfaces do not overlap — `.claude/` and `docs/` here, `src/` there". **RULE-004 still holds:**
#321's declared surfaces are `automation-configuration`, `default-automations`, `run-orchestration`
and the new `story-hold` — the Automations and Runs modules — and its branch currently touches only
its own `openspec/changes/` bundle. The two changes share no file. The note's *reasoning* was wrong;
its *conclusion* survives, and this design records the difference rather than repeating the claim.

### D7 — the hold label is provisioned by a human, and the change says so

`issue-lifecycle` forbids automation creating lifecycle labels, and AC 2 extends that to the hold.
The one-time `gh label create` is a task in `tasks.md` marked as a human bootstrap act, alongside
`BOOTSTRAP-CHECKLIST.md`. Until it exists, every command that needs it stops and reports it missing —
which is the designed behaviour, not a failure.

## Risks / Trade-offs

- **The change lands before #321 and the two definitions drift** → both read the same constant with
  the same case-insensitive comparison, and this change cites #321 as the definition's home rather
  than restating it. If #321's spec changes the name during its review, `holdLabel` is one edit.
- **`status:ready-for-implementation` on an unreviewed spec misleads anyone reading labels alone**
  → `/aio:status` reports the hold first (AC 9), `CONTRIBUTING.md`'s lifecycle diagram is updated to
  show the hold at both review stages, and the state is meaningless to advance while held.
- **`status:proposal-review` becomes an orphan** → named as out of scope in the proposal and in the
  issue. It stays in `lifecycleLabels` and in the nine; nothing sets it; retiring it is a later
  decision. Leaving a stale-looking state is the lesser harm against widening this change.
- **A reviewer removes the hold before actually reviewing** → unchanged from today, where they could
  set the next label without reading. The mechanism reduces the cost of doing it right; it cannot
  compel attention.
- **The starter copies drift again on the next command edit** → real, and pre-existing. Mitigated
  here only by doing the mirror; the durable fix is a drift gate, deliberately not in this change.
- **An installed starter workflow has no `.claude/workflow.json`** → also pre-existing (`wipLimit` is
  already read from a file the tier never seeds), and made one degree more acute by `holdLabel`. See
  Open Questions; not silently fixed here.

## Migration Plan

1. A human creates the hold label once (`gh label create`), per D7.
2. `holdLabel` lands in `.claude/workflow.json`; `read-issue` starts returning the hold.
3. Command files gain their checks and their applications, mirrored into the starter tier.
4. Docs — `CONTRIBUTING.md`, `AGENTS.md`, `docs/process/`, `BOOTSTRAP-CHECKLIST.md` — describe the
   two review stages as holds.
5. `docs/product/mvp/09-foundation-vs-product-split.md` gains the `/aio:*` Foundation row (AC 14).

**In-flight issues:** any issue already at `status:proposal-review` when this lands carries no hold
and will not be advanced by the new `/aio:implement` gate, because `proposal-review` is not its
gating state — behaviour identical to today. Moving such an issue on is the existing manual act. No
back-fill of holds onto open issues is performed, and none is needed.

**Rollback:** revert the commit. The hold label may stay on the repository harmlessly; with no
command reading it, it becomes an inert label again.

## Open Questions

1. **Should `.claude/workflow.json` become a starter prerequisite?** The spec-first tier installs
   prompts that read `wipLimit` — and now `holdLabel` — from a file it does not seed. Adding it to the
   manifest's `prerequisites` block is a small edit with a real behavioural consequence for adopting
   repositories, and it is not in #323's acceptance criteria. Raised for the reviewer; out of scope
   unless they say otherwise.
2. **Should a drift gate bind `.claude/commands/aio/*.md` to the starter copies?** Today nothing
   does, and this change is the second occasion where the mirror is a manual discipline. A test
   asserting byte-equality is cheap; it is also scope this issue did not ask for.
