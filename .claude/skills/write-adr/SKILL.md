---
name: write-adr
description: Write a new Architecture Decision Record (ADR) in docs/ using the project's template. Use when the user wants to record, document, or capture an architectural or technical decision — phrases like "write an ADR", "document this decision", "create an ADR for X", or "we need an ADR".
license: MIT
metadata:
  author: ai-orchestrator
  version: "1.0"
---

Create a new Architecture Decision Record from `docs/template.md` and fill it in
with the decision the user describes.

An ADR captures a single, significant, hard-to-reverse decision: *what* was
decided, *why*, the *alternatives* rejected, and the *consequences* accepted. It
is a dated, immutable historical record — once Accepted, an ADR is not edited;
it is superseded by a new one.

## When to write one

Write an ADR for decisions that are costly to reverse or that future
contributors will ask "why?" about: choosing a framework or datastore, a module
boundary, an auth strategy, a cross-cutting convention, an API contract style.
Do **not** write one for routine, easily-reversed choices (variable names, a
local refactor, a dependency bump).

## Steps

1. **Confirm there is a decision to record.** If the user's request is vague,
   ask the user to clarify: what was decided, what problem it
   solves, and what alternatives were weighed. Do not invent context — an ADR
   built on guesses is worse than none.

2. **Pick the next number.** List existing ADRs to find the highest number:
   ```bash
   ls docs/adr/ 2>/dev/null
   ```
   Use the next zero-padded integer (`0001`, `0002`, …). If `docs/adr/` does not
   exist yet, create it and start at `0001`.

3. **Derive the filename:** `docs/adr/NNNN-kebab-case-title.md`
   (e.g. `docs/adr/0003-use-central-package-management.md`).

4. **Copy the template and fill every section.** Read `docs/template.md` and use
   it verbatim as the structure. Replace `NNNN`, the title, and every
   placeholder. Set **Date** to today and **Status** to `Proposed` unless the
   user says the decision is already accepted. Write the **Context** in neutral
   terms, the **Decision** in active voice ("We will …"), and always populate
   **Alternatives considered** with at least one rejected option and its reason —
   that section is the whole point of an ADR.

5. **Link it.** Cross-reference related ADRs, the OpenSpec change or spec that
   prompted it, and any issue/PR under **References**. If this ADR supersedes an
   older one, set the old ADR's Status to `Superseded by ADR-NNNN`.

6. **Report** the path of the new file and a one-line summary of the decision
   recorded. Do not commit unless the user asks.

## Quality bar

- One decision per ADR. If the user describes several, write several.
- Keep it tight — context and decision should each be a few short paragraphs,
  not an essay.
- Be honest about trade-offs in **Consequences**; an ADR with no negatives is a
  red flag.
- Never edit an Accepted ADR to change the decision — supersede it instead.
