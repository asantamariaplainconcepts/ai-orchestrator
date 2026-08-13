## 1. Record and close the open decision

- [x] 1.1 Add **OPN-008** to `docs/product/mvp/07-open-decisions.md`, naming what it blocks.
- [x] 1.2 Close it in the same change and move it to the closed list, per the OPN-002/005/006/007
      convention.

## 2. The ADR

- [x] 2.1 Write `docs/adr/0029-*.md` evaluating all four options, each with its blast radius on
      `IRunTerminalHost`, on `run.attach` and on ADR-0021's deployed refusal.
- [x] 2.2 State which option won and **what it costs**, not only what it buys.
- [x] 2.3 Engage with DEC-065 explicitly — it permits the session and was decided with the sandbox in
      frame, so "we already allow this" is not an argument that survives unexamined.
- [x] 2.4 State the bound in words an implementation can be checked against, never "appropriately
      restricted".

## 3. The locked decision

- [x] 3.1 Land `DEC-070` in `docs/product/mvp/10-locked-mvp-decisions.md`, appended, never edited in
      place.
- [x] 3.2 State the habitat split and what `run.attach` grants in each.
- [x] 3.3 State whether the sandbox-shaped type names must change, and which.
- [x] 3.4 Record that this decision was taken unattended, and on whose authorisation.

## 4. The spec delta

- [x] 4.1 In `specs/agent-sandboxing/spec.md`, separate the requirements that bind a **sandbox** from
      those that bind **any terminal**.
- [x] 4.2 State the deployed refusal is unchanged.

## 5. Verification

- [x] 5.1 `openspec validate --strict` for this change.
- [x] 5.2 No code changes — `git diff --name-only` touches only `docs/`, `openspec/`.
