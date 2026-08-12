## 1. Record the decision first

- [x] 1.1 Write `docs/adr/0027-<slug>.md` from `docs/adr/template.md` — a change may reach `main`
      unreviewed on one explicit invocation: the context (two hold-marked review stages), the
      decision, why the staged path stays the default, why the hold-clearing invariant is preserved
      rather than excepted, its evidence, and its check. Allocate the number against `origin/main`
      (`decision-records` — *numbers are allocated against origin/main*).
- [x] 1.2 Add **DEC-068** to `docs/product/mvp/10-locked-mvp-decisions.md` in the file's existing
      style, one entry, citing ADR-0027 and naming what it authorises. Never edit an existing entry.
- [x] 1.3 Verify no `OPN-*` entry needs opening or closing for this change, and that
      `docs/product/mvp/07-open-decisions.md` is left untouched.

## 2. The unattended clause in each staged command

- [x] 2.1 `.claude/commands/aio/propose.md` — add the unattended-mode clause to step 7 and to the
      guardrails: in unattended mode the status advances **without** the hold, and every refusal
      becomes a halt that applies the hold and comments the reason. Change nothing about a direct
      invocation.
- [x] 2.2 `.claude/commands/aio/implement.md` — same clause: `status:code-review` set without the
      hold; the WIP cap and its refusal enforced unchanged, but as a halt that applies the hold and
      comments the issues holding the cap; the overlap check stays advisory.
- [x] 2.3 `.claude/commands/aio/sync.md` — clause for its three human touchpoints: the `/aio:ship`
      invocation is DEC-016's recorded go-ahead; the retro reflection points (step 4.2) and the
      squash subject (step 8) are derived and used without being presented for confirmation, with the
      retro entry marking its reflections unconfirmed. Every gate and ordering in steps 3–11 applies
      unchanged; each refusal becomes a halt that applies the hold.
- [x] 2.4 Confirm all three still read the hold's name from `holdLabel` in `.claude/workflow.json`
      and that none of the three files contains the literal label.

## 3. The command itself

- [ ] 3.1 Create `.claude/commands/aio/ship.md` with the standard front matter (name, description,
      category, tags) and its input contract (the issue number; ask if omitted).
- [ ] 3.2 Write its steps as **invocations, not restatements**: preflight and gate, then
      `/aio:propose` → `/aio:implement` → `/aio:sync`, each in unattended mode, naming only what
      unattended mode changes. It must state no gate, ordering or guarantee of its own.
- [ ] 3.3 Write its halt contract: on any staged refusal — CI red or pending, the WIP cap, or a
      question the issue and its spec do not answer — apply the hold, comment the specific reason,
      leave the `status:*` label untouched, and stop with no further mutation.
- [ ] 3.4 Write its record contract: the PR body and the retro entry each state that the change
      landed with no human reading the spec or the diff, and name `/aio:ship`.
- [ ] 3.5 Write its guardrails, including the two that must be unmissable: it never removes the hold,
      and it never widens a staged command's gate.

## 4. Documentation

- [ ] 4.1 `CONTRIBUTING.md` — add the unattended route: the loop diagram gains it, a section states
      DEC-068/ADR-0027, what authorises the merge, that a halt applies the hold and hands back, and
      plainly what the route gives up. Link `.claude/commands/aio/ship.md` rather than restating it.
- [ ] 4.2 `AGENTS.md` — add `/aio:ship` to the command list and to the hold's section, stating that
      it applies no hold on its happy path and removes none ever.
- [ ] 4.3 Re-read both against `contributor-docs` — *one canonical quick-start* and *CONTRIBUTING
      links rather than duplicates* — and remove any mechanics that belong in the command file.

## 5. Verification

- [ ] 5.1 `npx --yes @fission-ai/openspec@1.6.0 validate --changes` passes (the CI gate for this
      change's only touched paths).
- [ ] 5.2 `rg -i 'hitl' -g '!.claude/workflow.json' .claude docs CONTRIBUTING.md AGENTS.md` returns
      nothing — the hold's name still has exactly one home.
- [ ] 5.3 `rg -n 'remove-label' .claude` shows no command or skill removing the hold label,
      `ship.md` included.
- [ ] 5.4 Walk each of the 15 acceptance criteria on
      [#343](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/343) against the
      written command file and mark which are verified by reading and which will first be exercised
      by a real unattended run.
- [ ] 5.5 Diff-read the three staged command files to confirm each change is purely additive — a
      direct `/aio:propose`, `/aio:implement` or `/aio:sync` behaves exactly as it did.
- [ ] 5.6 State explicitly, in the PR, that this change lands through the **staged** path: a human
      reads the ADR that authorises the unattended one. `/aio:ship` is not exercised end to end by
      this change, and the criteria that need a real run say so.
