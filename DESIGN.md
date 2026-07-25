---
# GENERATED token block — derived from docs/design-system/tokens/*.css (canonical).
# Do not edit by hand. Regenerate:
#   node .claude/skills/aio-design/scripts/sync-design-tokens.mjs --write
colors:
  brand: "oklch(45% 0.13 258)"
  brand-hover: "oklch(40% 0.13 258)"
  brand-soft: "oklch(96% 0.02 258)"
  brand-border: "oklch(88% 0.04 258)"
  brand-on: "#fff"
  bg: "oklch(98.5% 0.003 250)"
  bg-muted: "oklch(96.5% 0.004 250)"
  bg-subtle: "oklch(96.5% 0.004 250)"
  surface: "#fff"
  surface-2: "oklch(98.5% 0.003 250)"
  surface-3: "oklch(97% 0.005 250)"
  fg: "oklch(22% 0.02 258)"
  fg-2: "oklch(36% 0.02 258)"
  fg-muted: "oklch(52% 0.015 258)"
  fg-subtle: "oklch(65% 0.012 258)"
  border: "oklch(91% 0.005 258)"
  border-strong: "oklch(85% 0.008 258)"
  ok: "oklch(52% 0.13 155)"
  ok-soft: "oklch(95% 0.04 155)"
  ok-border: "oklch(85% 0.07 155)"
  warn: "oklch(62% 0.13 75)"
  warn-text: "oklch(42% 0.13 75)"
  warn-soft: "oklch(95% 0.05 75)"
  warn-border: "oklch(85% 0.08 75)"
  danger: "oklch(52% 0.18 25)"
  danger-soft: "oklch(96% 0.04 25)"
  danger-border: "oklch(86% 0.07 25)"
  info: "oklch(52% 0.13 230)"
  info-soft: "oklch(95% 0.04 230)"
  info-border: "oklch(85% 0.05 230)"
  violet: "oklch(50% 0.16 305)"
  violet-soft: "oklch(95% 0.04 305)"
  violet-border: "oklch(85% 0.06 305)"
elevation:
  sh-1: "0 1px 0 #1418240a, 0 1px 2px #1418240a"
  sh-2: "0 4px 12px #1418240f, 0 1px 2px #1418240a"
  sh-focus: "0 0 0 3px oklch(55% 0.14 258 / 0.22)"
layout:
  sidebar-w-expanded: "280px"
  sidebar-w-collapsed: "64px"
  sidebar-w: "var(--sidebar-w-expanded)"
  header-h: "64px"
  measure: "72ch"
motion:
  t-fast: "0.12s"
  t-base: "0.18s"
  ease: "cubic-bezier(0.2, 0, 0.2, 1)"
radius:
  r-1: "4px"
  r-2: "6px"
  r-3: "8px"
  r-4: "10px"
  r-5: "12px"
  r-full: "999px"
spacing:
  sp-1: "4px"
  sp-2: "8px"
  sp-3: "12px"
  sp-4: "14px"
  sp-5: "16px"
  sp-6: "20px"
  sp-7: "24px"
  sp-8: "32px"
typography:
  font-sans: "'Geist', ui-sans-serif, system-ui, -apple-system, 'Segoe UI', sans-serif"
  font-mono: "'Geist Mono', ui-monospace, 'SF Mono', 'JetBrains Mono', monospace"
  fs-11: "11px"
  fs-12: "12px"
  fs-13: "13px"
  fs-14: "14px"
  fs-16: "16px"
  fs-20: "20px"
  fs-26: "26px"
  fw-regular: "400"
  fw-medium: "500"
  fw-semibold: "600"
  lh-tight: "1.25"
  lh-normal: "1.6"
  ls-heading: "-0.01em"
  ls-normal: "0"
---

# DESIGN.md — the design contract

<!-- The frontmatter above is generated. The prose below is written by hand and
     deliberately contains no values: values live in the canonical layer. -->

Read this before any UI work. The canonical system is
[`docs/design-system/`](docs/design-system/README.md) — this file is derived from it and
is guaranteed current by the CI drift gate.

## Rules

1. **Compose the kit, do not inline styles.** Use the classes in
   `docs/design-system/ui-kit/`. If the kit lacks what a screen needs, add it there —
   not in the screen.
2. **Every value is a token.** No raw hex, no raw pixel value where a scale exists, no
   font outside the approved stack. A literal cannot theme, which is why this is a
   failing check rather than a preference.
3. **Never branch on the theme.** Consume the variable; the theme swaps its value.
   Both light and dark are first-class and both must be checked.
4. **All user-facing copy comes from the typed i18n catalogue** and follows the content
   fundamentals (voice, sentence-case labels, verb-first buttons, the four state
   patterns) in the canonical README. Hardcoded JSX text fails lint.
5. **Use the locked vocabulary exactly** — Agent, Connector, Automation, Run, Plan.
6. **Every interactive element shows the shared focus treatment.** Do not remove an
   outline without replacing it.

## Before you push

```bash
bash .claude/skills/aio-design/scripts/validate-design-system.sh
```

It runs the same three stages CI runs: adherence, drift, and skill hygiene.
