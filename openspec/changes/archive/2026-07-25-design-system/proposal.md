# Proposal: design-system

## Why

The frontend has no visual language at all — the Projects screen is unstyled HTML. Before the
first real screen is built (Phase 5), the source of truth for visuals has to exist, because the
failure the source project hit was not ugly UI: it was **four overlapping design artifacts with no
ownership hierarchy**, discovered mid-implementation, forcing a change to be refocused from
"build the frontend" to "establish governance". Establishing ownership *before* implementing
against anything is the whole point of doing this now.

## The reference, and what we take from it

DEC-021 names the Atlas Plain Concepts app as the style reference. Atlas turns out to publish a
complete, coherent token system in its stylesheet: **OKLCH throughout, one brand/neutral hue
(258), named semantic hues, explicit type/radius/spacing scales, and paired light and dark
themes** selected by `[data-theme]`. Its components are composed purely from those tokens.

We adopt **the system's structure and its token values** — which is exactly what DEC-021 permits
("tokens: spacing, type scale, color feel are derived"). We take **no brand assets**: no logo, no
wordmark, no gradient artwork. Provenance is recorded in the canonical README: values derived from
the publicly served stylesheet of an internal product of the same company, style only.

Two consequences worth stating up front:

- **We ship light *and* dark**, because Atlas does and the pairing is what makes the token set
  coherent. Dark is not an afterthought bolted on later.
- **Geist is the typeface**, with the same system fallback stack. It is open-source (OFL), so
  vendoring or CDN-free self-hosting is available to us.

## What Changes

Three new capabilities (delta specs under `specs/`):

1. **design-tokens** — `docs/design-system/` as the canonical layer (L1): token CSS for colour,
   typography, spacing, radius, shadow, motion and layout, in both themes; one UI kit (we have a
   single experience, DEC-021); and **content fundamentals** — voice, register, button-label
   style, empty/error/confirmation copy patterns — so copy is designed rather than improvised.
2. **design-contract** — the strict one-way derivation: L1 → generated `DESIGN.md` (L2, what
   agents read) → generated `tokens.ts` (L3, what the SPA imports) → the value-free design skill
   (L4). A dependency-free generator produces L2 and L3, both marked generated with their
   regeneration command in the header. **Conflicts are always resolved by regenerating downward.**
3. **design-adherence** — the three-stage validator in the CI lint lane: adherence (no raw hex, no
   raw px where a token exists, no non-approved fonts, no hardcoded user-facing JSX copy), drift
   (regenerate L2/L3 in `--check` mode and fail on any mismatch), and skill hygiene (the design
   skill must contain no literal token values, so it cannot drift).

## Out of scope (deliberate)

- **The docs-hygiene CI gate and the telemetry port collision.** The last retro asked for a link
  check and an `ONBOARDING.md` line-count assertion in the lint lane, and the telemetry gap is a
  standing defect. Both are pure infra/tooling with no spec delta, which is precisely what the
  **spec-less lane** (DEC-025) exists for. They go through it as one small change after this —
  which also exercises that lane for the first time, before Phase 5 depends on it.
- **Restyling existing screens beyond the Projects exemplar.** The kit is proved on the one screen
  that exists; the rest arrive with their features.
- Any product capability, and any change to the backend.

## Impact

- New: `docs/design-system/**`, `DESIGN.md`, `src/frontend/shared/design/tokens.ts`,
  `.claude/skills/aio-design/`, the generator and validator scripts.
- Modified: `.github/workflows/lint.yml` (validator step); the Projects screen adopts the kit;
  `AGENTS.md` gains the design row.
- Affected specs: three ADDED. `frontend-architecture` is **not** modified — its token-only
  styling and i18n requirements already anticipated this; this change supplies what they assumed.
- The existing i18n lint rule stays exactly as it is and becomes stage 1 of the validator rather
  than being replaced.
