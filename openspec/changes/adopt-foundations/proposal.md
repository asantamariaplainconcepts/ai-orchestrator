# Proposal: adopt-foundations

## Why

Issue #107, owner decision of 2026-07-28: this product's frontend follows
[PlainConceptsPlatform/Foundations](https://github.com/PlainConceptsPlatform/Foundations) — the
shared Platform base: `@plainconceptsplatform/ui-theme` (tokens, public npm, pure CSS),
shadcn/ui used directly, Tailwind v4, Lucide — **on the existing Vite app**. Two things made
this adoptable rather than a rewrite: the theme is CSS with no Next.js requirement, and shadcn
works under Vite. Recorded as DEC-051, revising DEC-021's aesthetic half and DEC-009's styling
half; Vite, VSA slices and typed i18n stand.

## What Changes

- Tailwind v4 + the theme wired into the Vite build; Outfit loaded Vite-style; Lucide.
- **The shell rebuilt on the new base, responsive included** — sidebar on desktop, folded top
  bar under `md`, Inbox badge preserved. The old chain's "kit learns small screens" lands here
  for free.
- **The projects list migrated as the pattern-proof**: the smallest real screen, rebuilt on
  unwrapped shadcn primitives, defining the conventions every later migration follows.
- **Coexistence**: the kit's CSS stays loaded for unmigrated screens; each later slice migrates
  what it touches; the kit dies by replacement.
- **The design gates re-point**: theme.css becomes the token source; raw-hex/raw-px rules keep
  applying to app code; the kit's DESIGN.md generator retires with its last screen.

## Impact

- Affected specs: `design-contract` (the source of visual truth changes).
- Touched: frontend build, shell, projects screen, design validator config, DEC-051, docs.
- Out of scope: Next.js/inversify/Biome; migrating further screens; #109/#110.
