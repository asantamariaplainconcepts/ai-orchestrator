---
name: grill-to-ready
description: Interrogate a raw idea or a markdown source (e.g. a use case in docs/product/mvp/) into a Definition-of-Ready issue draft. Use when a work item needs clarifying before it becomes a GitHub issue.
---

Turn a fuzzy input, or an existing issue's current state, into a Definition-of-Ready evaluation by grilling the human — one responsibility. Do not create the issue, post comments, or set labels (that is `create-github-issue` / `set-issue-status` / the orchestrating command).

## Mode: raw input (new issue)

1. **Read the source.** If given a path/section (e.g. `docs/product/mvp/04-mvp-use-cases.md#UC-xxx`), read it. If given an inline idea, use that. Read the grounding docs: `docs/product/mvp/02-domain-glossary.md`, `05-business-rules.md`, `10-locked-mvp-decisions.md`, `03-bounded-contexts.md`.
   - Done when: the source and the relevant glossary/rules/decisions are in hand.
2. **Grill.** Ask the human focused questions ("grill me about this topic") to fill every gap the Definition of Ready requires — see `docs/process/definition-of-ready.md` (built on `08-backlog-shaping-rules.md`: RULE-001 fields, RULE-002 slicing, RULE-003 traceability, RULE-006 open decisions). Ask one cluster at a time; stop asking once a field is satisfied.
   - Done when: every DoR field has an answer, or an open decision is identified as blocking.
3. **Handle open decisions.** If the item depends on a still-open decision (e.g. OPN-004), draft it as a blocking **decision-closure** item instead of assuming an answer.
   - Done when: no DoR field silently assumes an unresolved decision.
4. **Emit the draft.** Produce the issue body: title (RULE-001 style — capability-oriented, imperative; RULE-007 lists the anti-patterns to reject), description, main actor, priority, dependencies, deterministic acceptance criteria (given/when/then), business-rule IDs, affected UC IDs, out-of-scope, and the change/spec-ID field.
   - Done when: the draft satisfies every DoR field and cites its UC/BR/actor IDs; hand it back for `create-github-issue`.

## Mode: existing issue (challenge, don't draft)

Used when `/ds:grill` is given an issue number instead of raw input. The orchestrating command supplies the issue's current title/body (via `read-issue`); this mode evaluates rather than interrogates from scratch.

1. **Evaluate.** Check the issue's current title/body against every Definition-of-Ready field (same rubric as above — do not re-derive it here). No back-and-forth grilling; this is a one-shot gap check against what's already written.
   - Done when: every DoR field is judged met or unmet.
2. **Return the result.** If any field is unmet, return the specific list of missing/incomplete fields (concrete enough for the command to post verbatim as a comment). If every field is met, return a readiness confirmation. Either way, do not post the comment or touch labels yourself.
   - Done when: the command has either a gap list or a readiness confirmation to act on.

## Do not

- Create/edit GitHub state — that's other skills or the orchestrating command (comments, labels, issue creation).
- Invent acceptance criteria the human didn't confirm.
- Duplicate the DoR rubric here — read it from `docs/process/definition-of-ready.md`.
- In existing-issue mode, interrogate the human like the raw-input mode does — report gaps, don't fill them in yourself.
