# Tasks — design-system

L1 first, then the generator, then what it generates, then the gate. Building the gate before the
thing it guards would mean writing a check with nothing to check.

## 1. Canonical layer (L1)

- [x] 1.1 `docs/design-system/tokens/`: `colors.css` (OKLCH, one brand/neutral hue, semantic
      families as base/`-soft`/`-border`, both themes plus a `prefers-color-scheme` default),
      `typography.css`, `spacing.css`, `radius.css`, `elevation.css`, `motion.css` (with a
      `prefers-reduced-motion` override), `layout.css`.
- [x] 1.2 `docs/design-system/ui-kit/`: `base.css` (element defaults, one shared focus treatment)
      and `components.css` (shell, card, button, input, list, badge, and the empty/loading/error
      states). Token composition only — not one literal value in either file.
- [x] 1.3 `docs/design-system/README.md`: the four layers and the one-way rule, the file map,
      **content fundamentals** (voice, locked vocabulary, sentence-case labels, verb-first
      buttons, the four state patterns, identifiers in monospace), and **provenance**.
- [x] 1.4 Fonts: Geist named first with the full system fallback stack. **Not self-hosted** — the
      network blocks font downloads in this environment, and the type scale deliberately does not
      depend on Geist's metrics, so the fallback renders correctly (verified in a browser).
      Self-hosting remains available later with no token change.

## 2. Generator (L1 → L2, L3)

- [x] 2.1 `sync-design-tokens.mjs` — dependency-free, `--write` and `--check`.
- [x] 2.2 `DESIGN.md` (L2) generated: token frontmatter + value-free prose, header naming the
      regeneration command.
- [x] 2.3 `tokens.ts` (L3) generated: token **names bound to `var(--…)`**, never copied values.
- [x] 2.4 Verify: `--check` exits 0 on a clean tree; changing a canonical token makes it exit
      non-zero and print the regeneration command.

## 3. Design skill (L4)

- [x] 3.1 `.claude/skills/aio-design/SKILL.md` — a router: read `DESIGN.md`, find the kit
      component before writing one, resolve copy through the catalogue, cover all four states,
      check both themes and focus, validate before pushing. Zero literal values.

## 4. The gate

- [x] 4.1 `validate-design-system.sh` — adherence (raw hex, raw px, unapproved fonts, hardcoded
      copy via ESLint so the verdict is identical), drift (`--check`), skill hygiene. Scoped to
      `src/frontend/**`; excludes the generated adapter, `node_modules`, and `dist`. `0px`/`1px`
      are allowed — hairlines and zero offsets have no token and never will.
- [x] 4.2 Wired into the lint lane as a step of the existing frontend job.
- [x] 4.3 **Verified by probe, each reverted:** raw hex → FAIL; raw 37px → FAIL; `Comic Sans MS`
      → FAIL; canonical token changed without regenerating → FAIL (drift); `oklch(...)` pasted
      into the skill → FAIL (hygiene). Clean tree → all three stages pass.

## 5. Adopt on the one screen that exists

- [x] 5.1 Projects screen restyled with the kit: shell with header and theme toggle, card-based
      form, list with monospace ids, and all four states. New copy added to the catalogue
      following the documented patterns.
- [x] 5.2 Verified in a real browser at both themes — layout intact, keyboard focus ring clearly
      visible, and contrast measured rather than eyeballed (below).

## 6. Close-out

- [x] 6.1 `AGENTS.md` gained the design row pointing at `DESIGN.md`.
- [x] 6.2 Verify sweep: validator green, `format:check`/`lint`/`typecheck`/`build` green,
      19 backend tests pass, `openspec validate` green.

### What verification found (and what it changed)

Three defects, none of which a visual check would have caught:

1. **The reference's dark brand fails WCAG AA.** White button text on the reference's dark
   `--brand` measures **3.98:1**, under the 4.5:1 this system requires for body-sized text. Our
   dark brand is `oklch(54% …)` instead, measuring **5.10:1**; light was already fine at 7.55:1.
   Recorded in the README as an explicit, reasoned deviation from the reference.
2. **Prettier and the generator both claimed the generated adapter.** Two formatters on one file
   guarantees drift: whichever ran last "won" and the other reported a violation. The generated
   adapter is now in `.prettierignore` — derivation is one-way, so the generator owns it.
3. **A formatter broke the parser.** Prettier wrapped the long font-stack declarations across
   lines, and the line-based token parser silently dropped them — the font tokens vanished from
   the generated output with no error. The parser now splits on declarations rather than lines.
   The drift stage is what surfaced it, which is the gate doing precisely its job.

My first contrast measurement was also silently wrong: the computed values are `oklch()`, and the
naive parser read them as `rgb()`, reporting a meaningless `1.00` for everything. Resolving
colours through a canvas fixed it. Worth noting because a measurement that returns a plausible
number is more dangerous than one that errors.
