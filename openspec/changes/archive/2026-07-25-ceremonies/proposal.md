# Proposal: ceremonies

## Why

The `/aio:*` commands exist but several of them reference documents that do not: `/aio:grill`
stops because there is no Definition of Ready to read, and every status transition stops because
the nine labels have never been created. That was deliberate — Phase 2 chose loud failure over
invented artifacts — and this change is what turns those refusals green.

There is a second, sharper reason. **Two ADRs are overdue.** Both patterns reached their second
occurrence during the last change, and the retro named them in full precisely so they could not
quietly slip: the source project let patterns recur ten or more times while waiting for a tidier
moment to write them down. Writing them is this change's *first* task, not its last.

## What Changes

Four new capabilities (delta specs under `specs/`):

1. **issue-lifecycle** — the nine `status:*` labels as the sole lifecycle state, their legal
   transitions, and the rule that boards are label-filtered views that nothing reconciles. The
   labels themselves are a one-time `gh label create` bootstrap, not automation.
2. **definition-of-ready** — `docs/process/definition-of-ready.md`, built on this corpus's
   `RULE-001..007` (not a parallel checklist), including the open-decision gate that blocks work
   depending on an `OPN-*`.
3. **decision-records** — `docs/adr/` with the template, the immutability rule (supersede, never
   edit an accepted ADR), numbering allocated against `origin/main`, and the **graduation rule at
   the second occurrence**. Ships with the two overdue ADRs already written.
4. **contributor-docs** — `CONTRIBUTING.md` (the loop for humans, the solo path from DEC-016, the
   spec-less lane from DEC-025) and `ONBOARDING.md` (≤ 40 lines — it stays short because the docs
   architecture carries the weight).

## Out of scope (deliberate)

- **Issue and PR templates** — they already landed in Phase 1, including the "Time invested"
  section. This change verifies they still match the lifecycle rather than rewriting them.
- **The retro log** — already exists and has three entries. Its *format* is spec'd here; its
  content is not touched.
- The design system (Phase 4) and running a real feature through the loop (Phase 5).

## Impact

- New: `docs/process/definition-of-ready.md`, `docs/adr/` (template + `0001`, `0002`),
  `CONTRIBUTING.md`, `ONBOARDING.md`.
- Modified: `AGENTS.md`'s lookup table loses its "*lands in bootstrap Phase 3*" markers, because
  the documents now exist.
- One-time repository operation: nine `gh label create` calls. This is state outside the repo, so
  it is performed once with confirmation and recorded here — no committed script recreates it.
- Affected specs: four ADDED. No existing capability is modified.
- **After this change the `/aio:*` commands stop failing on missing artifacts** — which makes
  Phase 5's "run a real feature guided only by the commands' refusals" possible for the first time.
