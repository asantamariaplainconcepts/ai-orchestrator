# Proposal: atlas-shell-adoption

## Why

Issue #32. The portal works but reads as a prototype: a headerbar, cards, and a back-link where
an internal product of this family has a persistent sidebar, a top bar with breadcrumbs, and
dense data surfaces. Phase 4 anticipated exactly this moment — the reference application was
inspected in use and its shell **measured**, with every value recorded in our own tokens under
"Reference observations, for screens we have not built yet" in `docs/design-system/README.md`.
That section exists so this change starts from recorded measurements instead of re-deciding; the
`--sidebar-w-*` and `--header-h` tokens have been waiting in `layout.css` since then.

The kit deliberately contained only what the app rendered (no speculative surface). Both screens
now exist and the owner has asked for the adoption, so the components stop being speculative:
this change moves the recorded patterns from prose into the kit, and the two screens onto them.

DEC-021 continues to govern: style only — no logo, no wordmark, no gradient artwork, no imagery.

## What changes

- **Kit growth (canonical layer, then regenerate):** sidebar + nav item + section label + user
  card, top bar with breadcrumbs and page-title row, dense table treatment, status pill with
  leading dot, stat/filter card with coloured left border. All values are the recorded ones,
  already expressed in existing tokens; **no new token is expected** — the README records that
  every observed treatment mapped onto the palette as it stands.
- **Both screens adopt the shell:** Projects and the project page render inside the
  sidebar+top-bar shell; the back-link becomes sidebar navigation; breadcrumbs carry location.
- **Backlog data patterns:** the Stories list becomes the recorded table treatment; a stat-card
  row above it shows **only facts already in the live response** (story count, open count,
  trigger-labelled count, connector health). No invented metrics.
- **Out of scope** (recorded in #32): collapsed-sidebar behaviour, table toolbar
  (column-visibility/export — their actions have no backend), real user identity in the user
  card (placeholder until #12), any new screen.

## Impact

- `docs/design-system/ui-kit/components.css` grows; `DESIGN.md` and the runtime adapter are
  regenerated (one-way derivation, ADR-0003).
- `src/frontend`: shell components in `shared/ui/`, both feature screens recomposed; routes,
  hooks and the HTTP client are untouched.
- E2E: existing journeys must keep passing; assertions keyed to roles/headings survive the
  recomposition, and any that keyed to layout are updated to unfakeable assertions (ADR-0004).
- Specs: `design-contract` (kit scope requirement) and `frontend-architecture` (shell
  requirement) get deltas; tokens spec is untouched — no token changes.
