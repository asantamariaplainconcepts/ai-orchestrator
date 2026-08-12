# ADR-0026: A retro finding that names a fix becomes a tracked issue before the change syncs

- **Status:** Accepted
- **Date:** 2026-08-12
- **Deciders:** repository maintainer (solo, DEC-003)
- **Tags:** process, retros, workflow

## Context

`docs/process/retro-log.md` is append-only, and every entry ends with a **one change next time**.
The log is written at `/aio:sync` and read by nobody — not by a command, not by a gate, and not by
the next change, which starts from an issue and a spec.

Two findings recorded in the retro for `local-run-in-its-own-checkout` (#331) were paid for again by
the very next change, `local-run-checkout-is-ready-to-build` (#332), which merged the same day:

1. **`OTEL_EXPORTER_OTLP_ENDPOINT` is unset**, so Claude Code's exports go to the OTLP default port
   rather than this project's collector. #331's Time line named the failing check by name and
   reported its figures as "a floor, not the cost". #332 got **zero** datapoints — its session was
   mapped in `sessions.jsonl` and had no matching rows in `usage.jsonl` at all. The measurement is
   unrecoverable; nothing reconstructs telemetry that was never written.
2. **`Terraform_Should_NeverConfigureTheLocalOwner` cannot find the repository root in a git
   worktree** — it walks up looking for a `.git` *directory*, and in a worktree `.git` is a file.
   #331's own "one change next time" described the remedy exactly: *"a one-line fix and an issue,
   not a decision."* #332's full-suite run was red on that test, and the time went on establishing
   that it was pre-existing rather than newly broken.

Neither became an issue. Both were correctly diagnosed, written down in the place the process
provides for writing things down, and that changed nothing about what happened next. `AGENTS.md`
already carries the instruction the first would have needed — *"Check it works before starting a
change, not at the retro"* — which is evidence that prose guidance was not the missing part.

This is the **second occurrence**, which is this repository's graduation rule for turning a retro
observation into a decision. It is also two independent instances inside a single change, which is
what distinguishes it from bad luck.

## Decision

We will treat a retro finding that names a fix as **incomplete until it is a tracked issue**.

At `/aio:sync`, when the retro entry's *what didn't* or *one change next time* names a concrete
remedy — a file to change, a check to fix, a setting to set — the same command creates a GitHub
issue for it and the retro entry links that issue by number. The entry may then say what it always
said; what changes is that the remedy now exists somewhere the workflow actually looks.

A finding with no concrete remedy — an observation about how the work felt, a lesson that shapes
future judgement rather than future code — creates no issue. The test is whether a reader could act
on it without re-deriving it, not whether it is important.

The issue is created **before** the close-out commit, so it rides the same review as the retro entry
that references it and cannot be lost to a merge that happens anyway.

## Consequences

- **Positive:** a named defect leaves the retro with an owner-addressable artifact instead of a
  paragraph. The next change meets it in the backlog, which it reads, rather than in the log, which
  it does not.
- **Positive:** the retro log stops being the only record of known-broken things, which it was never
  designed to be — it is a history, not a queue.
- **Negative:** more issues, some of them small, and a backlog that grows faster than it is worked.
  A one-line fix with an issue is more ceremony than a one-line fix without one; the two changes
  above are the argument that the ceremony is cheaper than the rediscovery.
- **Negative:** it puts a judgement call inside `/aio:sync` — "does this finding name a remedy?" —
  and a command that guesses wrong either spams the backlog or silently drops the finding. The
  proposing step presents the call to a human like every other retro decision.
- **Neutral:** the two findings that prompted this ADR are themselves the first application; both
  become issues as part of #332's sync.
- **Neutral:** `/aio:refine` appends post-merge findings and is subject to the same rule, since a
  finding discovered later is not a finding that matters less.

## Alternatives considered

- **Leave it as prose in `AGENTS.md` / the retro instructions** — rejected because that is the
  status quo and it is what produced two repeat occurrences in consecutive changes. The instruction
  to check telemetry before starting already existed in exactly that form.
- **Have `/aio:propose` or `/aio:implement` read the retro log and surface open findings** — rejected
  because it makes an append-only history into an implicit work queue, and every future change pays
  to re-read a log that grows without bound. An issue is the artifact the workflow already has for
  "something to do".
- **Make `verify-telemetry.mjs` a hard preflight gate on the mutating commands** — rejected as the
  *general* remedy, because it fixes one instance rather than the class, and a gate that blocks work
  on a broken local collector would refuse changes that are otherwise ready. It remains a reasonable
  thing to do for telemetry specifically, and is exactly the kind of remedy the issue this ADR
  mandates would carry.
- **Require the fix itself before sync, not an issue** — rejected because it couples an unrelated
  repair to a change that is ready to merge, which is how a small fix becomes a large diff and a
  review about two things.

## References

- Retro entries: `docs/process/retro-log.md` — `local-run-in-its-own-checkout` (#331) and
  `local-run-checkout-is-ready-to-build` (#332)
- Related: [ADR-0025](0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) —
  human time is recorded by a person; this ADR is about what happens to the *defect* that made the
  agent figure unmeasurable
- Related: [ADR-0011](0011-a-worktree-session-carries-its-own-telemetry.md) — a worktree session
  carries its own telemetry
- Workflow commands: `.claude/commands/aio/sync.md`, `.claude/skills/retro-entry/SKILL.md`
