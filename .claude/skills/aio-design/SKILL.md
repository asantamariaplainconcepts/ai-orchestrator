---
name: aio-design
description: Route any UI work through the design system — read DESIGN.md, compose kit components, resolve copy through i18n, validate before pushing. Use when building or changing a screen, component, or user-facing copy.
---

Route UI work through the design system — one responsibility. This skill contains **no design
values**: values live in the canonical layer, so this file can never drift from it.

## Steps

1. **Read the contract.** Open `DESIGN.md` at the repository root. It carries the current token
   vocabulary and the rules. Do not proceed from memory of a previous session.
   - Done when: the tokens and rules for this change are in hand.
2. **Find the component before writing one.** Look in `docs/design-system/ui-kit/` for a class
   that already does this. If nothing fits, add it **there** and regenerate — never inline styles
   in a screen, and never add a component ahead of a screen that needs it.
   - Done when: every visual element the change renders maps to a kit class.
3. **Resolve copy through the catalogue.** Every user-facing string comes from
   `src/frontend/shared/i18n/`, follows the content fundamentals in the canonical README (voice,
   sentence-case labels, verb-first buttons, the four state patterns), and uses the locked
   vocabulary exactly — Agent, Connector, Automation, Run, Plan.
   - Done when: no literal user-facing text exists in the change and every new key is in the
     catalogue.
4. **Cover all four states.** Any view that loads data renders empty, loading, error and success
   — each following its documented pattern.
   - Done when: all four are implemented, not just the happy path.
5. **Check both themes and keyboard focus.** Light and dark are equally first-class; every
   interactive element shows the shared focus treatment.
   - Done when: the change has been seen in both themes and tabbed through.
6. **Validate before pushing.** Run
   `bash .claude/skills/aio-design/scripts/validate-design-system.sh` — the same three stages CI
   runs: adherence, drift, skill hygiene.
   - Done when: the validator exits zero.

## If a token or component is missing

Add it to the canonical layer (`docs/design-system/`), regenerate with
`node .claude/skills/aio-design/scripts/sync-design-tokens.mjs --write`, and commit the canonical
change together with the regenerated files. Derivation is one-way: never edit `DESIGN.md` or
`tokens.ts` to match a screen.

## Do not

- Put any literal colour, size, or duration in a screen, in this skill, or in a generated file.
- Branch on the theme in a component — consume the variable and let the theme swap its value.
- Remove a focus outline without replacing it.
- Add a component library or a second token system.
- Edit a generated file by hand.
