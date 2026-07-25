# Design — ceremonies

## Verified reality (checked, not assumed)

- **Zero `status:*` labels exist** on the repository today (`gh label list` → none). Every
  lifecycle transition in the `/aio:*` commands is currently a loud failure, exactly as Phase 2
  designed it.
- **`docs/process/retro-log.md` exists with three entries** and is append-only. This change
  specifies its format; it does not touch its content.
- **Issue and PR templates already exist** from Phase 1, with the "Time invested" section intact.
- The repository is **public** on a personal account, so rulesets are available on the free plan
  — but **none is configured**. That is a decision this change records rather than assumes (D3).

## Decisions

### D1 — Labels are a one-time bootstrap, and the board is a view

The nine labels are created once with `gh label create` and never by committed automation. The
`status:*` label is the **sole** lifecycle state: GitHub Projects, if used at all, are
label-filtered saved views, and a manual edit to a board field is inert because nothing
reconciles it.

**Rejected: a board-sync automation.** The source project built one and tore it out a day later.
It requires a privileged token to maintain derived state that the label already expresses, and
the failure mode — a board and a label disagreeing about the truth — is worse than having no
board at all.

### D2 — The Definition of Ready cites the RULE catalog; it does not restate it

`docs/process/definition-of-ready.md` binds each requirement to `RULE-001..007` in
`docs/product/mvp/08-backlog-shaping-rules.md`, which stays the authority for issue shape. This
is the same single-source discipline the skills already follow: `grill-to-ready` reads the DoR
rather than embedding a rubric, and the DoR cites the rules rather than copying them. A rubric
change touches exactly one file.

### D3 — No branch protection, recorded as a decision

`/aio:sync` pushes a `[skip ci]` close-out commit whose SHA therefore has **no check runs**. A
required-status-check rule on `main` would deadlock precisely there: the merge would wait forever
for a check that can never run. Combined with the solo path (DEC-016 — GitHub forbids
self-approval, so a required-approval rule is equally fatal), the choice is **no branch
protection**, and the gates live in the commands and CI instead.

This is a real trade-off, not a free win: nothing at the platform level stops a direct push to
`main`. It is acceptable for a solo maintainer and would need revisiting the moment a second
committer appears — at which point the `[skip ci]` mechanism must be revisited *with* it, because
the two are coupled. Recorded here so a future reader finds the reasoning rather than the gap.

### D4 — ONBOARDING.md stays under 40 lines, structurally

The limit is a forcing function, not a style preference: it stays short only because every fact
lives somewhere else and onboarding links to it. If it grows past 40 lines, the correct fix is
almost always to move a fact into `ARCHITECTURE.md`, the corpus, or `CONTRIBUTING.md` — not to
raise the limit. The spec states the limit so the pressure is visible at review time.

### D5 — The two overdue ADRs ship in this change, written from evidence

Both graduate at their second occurrence, and both are drawn from what actually happened rather
than from principle:

- **ADR-0001 — verify claims by exercising them.** Four instances across two changes: the host
  was assumed to have an HTTP endpoint (it had no launch profile), assumed to apply migrations
  (nothing did), health was assumed to mean "can serve" (it checked only liveness), and the E2E
  log watch was assumed to work because it compiled (it watched the wrong key and returned
  silence).
- **ADR-0002 — a test tier that provisions its own preconditions hides their absence.** The
  functional fixture migrated privately, concealing that the application never did; and an
  all-sequential suite structurally could not observe a concurrency bug that only parallel
  traffic produces.

Both carry a **consequence with teeth** — a check that would have caught the instance — so they
graduate toward gates rather than remaining advice, which is the point of the ADR loop.

## Risks

- **Ceremony documents drifting from the commands they describe.** `CONTRIBUTING.md` narrates the
  same loop the `/aio:*` commands enforce, so a command change can silently orphan the prose.
  Mitigation: the docs link to the command files rather than restating gate mechanics, and the
  spec requires the lifecycle to be stated in exactly one place.
- **Writing the ADRs as a formality.** An ADR that only restates the retro adds nothing.
  Mitigation: each one names the specific instances that produced it and the check that would
  have caught them.
