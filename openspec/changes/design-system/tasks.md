# Tasks — design-system

L1 first, then the generator, then what it generates, then the gate. Building the gate before the
thing it guards would mean writing a check with nothing to check.

## 1. Canonical layer (L1)

- [ ] 1.1 `docs/design-system/tokens/`: `colors.css` (OKLCH, brand + neutrals on one hue, semantic
      families each with base/`-soft`/`-border`, both themes — `:root` light, `[data-theme=dark]`
      override, `prefers-color-scheme` default), plus `typography.css`, `spacing.css`,
      `radius.css`, `elevation.css` (shadows + focus ring), `motion.css`, `layout.css`.
- [ ] 1.2 `docs/design-system/ui-kit/`: layout shell, button, input, card, table, badge, and the
      empty/loading/error states the Projects screen renders. Token composition only; shared focus
      treatment on every interactive element.
- [ ] 1.3 `docs/design-system/README.md`: the four layers and the one-way rule, **content
      fundamentals** (voice, register, sentence-case labels, verb-first buttons, empty/error/
      confirmation patterns, English per DEC-021, DEC-005 vocabulary used exactly), and
      **provenance** — derived from the Atlas style reference, no brand assets, decisions win over
      the reference with IDs cited.
- [ ] 1.4 Self-host the Geist and Geist Mono webfonts (OFL) under the frontend, or fall back
      cleanly. Verify the kit is legible with the fallback stack — the type scale must not depend
      on Geist's metrics.

## 2. Generator (L1 → L2, L3)

- [ ] 2.1 `.claude/skills/aio-design/scripts/sync-design-tokens.mjs` — dependency-free, parses the
      canonical CSS, supports `--write` and `--check`.
- [ ] 2.2 Generate `DESIGN.md` (L2): token block + value-free prose on applying the system, header
      declaring it generated with the regeneration command.
- [ ] 2.3 Generate `src/frontend/shared/design/tokens.ts` (L3): token **names bound to
      `var(--…)`**, never copied literals, same generated header.
- [ ] 2.4 Verify: `--check` on a clean tree exits 0 silently; touching a canonical token makes it
      exit non-zero and name the regeneration command.

## 3. Design skill (L4)

- [ ] 3.1 `.claude/skills/aio-design/SKILL.md` — a router: `DESIGN.md` first, compose kit
      components, copy through the i18n catalogue, run the validator before pushing. Reviewed
      against `writing-great-skills`. **Zero literal values.**

## 4. The gate

- [ ] 4.1 `.claude/skills/aio-design/scripts/validate-design-system.sh` — three stages: adherence
      (raw hex, raw px where a token exists, non-approved fonts, hardcoded JSX copy), drift
      (generator `--check`), skill hygiene (no literals in the skill). Scoped to
      `src/frontend/**` source; excludes generated files, vendored assets, and
      `docs/design-system/` itself.
- [ ] 4.2 Wire it into the lint lane's frontend job. Stage 1 absorbs the existing i18n rule rather
      than duplicating it.
- [ ] 4.3 Verify by probe, each reverted: a raw hex fails; a raw px where a token exists fails; a
      hardcoded JSX string fails; an un-regenerated token change fails drift; a literal pasted
      into the skill fails hygiene. A clean tree passes all three.

## 5. Adopt on the one screen that exists

- [ ] 5.1 Restyle the Projects screen with the kit — shell, form, table/list, and its empty,
      loading and error states. No new capability, no new copy beyond what the catalogue holds.
- [ ] 5.2 Verify in a browser at both themes: layout intact, contrast AA, focus visible on every
      interactive element.

## 6. Close-out

- [ ] 6.1 `AGENTS.md`: add the design row pointing at `DESIGN.md` and the canonical system.
- [ ] 6.2 Verify sweep: `pnpm build` succeeds, the validator passes, `openspec validate` green,
      CI green on the PR.
