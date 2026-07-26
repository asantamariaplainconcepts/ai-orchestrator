# Tasks — automation-configuration

Domain and its one hard rule first, then the slices, then the screen. Claims that depend on
verification stay hypotheses until run (ADR-0005); acceptance asserts artifacts (ADR-0004).

## 1. Domain

- [x] 1.1 `Automation` in the Projects module: `internal sealed`, trigger label + optional state,
      action, runtime, `requiresApproval`, timeout. `Enabled` exists from the start — BR-003
      reads it, even though #15 owns toggling it.
- [x] 1.2 The action catalogue (DEC-026) and runtime enum. **Runtime has exactly one value**
      (Claude Code); opencode waits on OPN-004 (RULE-006). The enum exists so #30 adds a value,
      not a column.
- [x] 1.3 `Overlaps(other)` as pure domain logic — the D1 table, all four rows, no database.
- [x] 1.4 Unit-test the table exhaustively, including the asymmetry: a broad trigger blocks a
      later narrow one **and** the reverse, since "some Story could match both" is symmetric.

## 2. Persistence

- [x] 2.1 EF configuration + migration in the `projects` schema. Timeout stored as an interval;
      the action and runtime as their enum names, **not ordinals** — #7 proved a stored ordinal
      reads back as "0" when a projection is translated to SQL rather than evaluated in .NET.
- [x] 2.2 Verify: the migration touches only `projects`.

## 3. Slices

- [x] 3.1 `CreateAutomation`: validator, handler, overlap rejection returning a domain error that
      **names the conflicting Automation** — "invalid" tells an Admin nothing actionable.
- [x] 3.2 `ListAutomations` for a Project.
- [x] 3.3 Functional tests against real containers: create; reject an exact duplicate; allow the
      same label with different states; reject the subsumption case both ways round; confirm no
      response field carries a credential (BR-010).

## 4. The screen

- [x] 4.0 Route through the `aio-design` skill: read `DESIGN.md`, compose kit classes, copy
      through the i18n catalogue, run the validator before pushing.
- [x] 4.1 An Automations section on the project page: the list, and a form to add one.
      **The kit grew by one component:** it had no boolean control, and a bare native checkbox
      reads as unfinished beside tokenised inputs. `.checkbox`/`.field-inline` added to the
      canonical layer and regenerated rather than styled inline.
- [x] 4.2 Copy that says which actions cannot execute yet (design D3) — the whole point of
      shipping the catalogue whole is that the interface is honest about it.
- [x] 4.3 The overlap rejection surfaces the conflicting Automation's name, not a generic error.
- [x] 4.4 Verify: `pnpm lint`, `pnpm typecheck`, design validator; both themes; keyboard focus
      visible on every control.

## 5. Close-out

- [x] 5.1 The Projects module `context.md` gains Automations and the D1 rule — a reader who does
      not know why a save was refused will otherwise "fix" the check.
- [x] 5.2 Record the D4 race (two concurrent creates can both pass the overlap check) where the
      next person will meet it.
- [ ] 5.3 Full verify sweep; CI green.
