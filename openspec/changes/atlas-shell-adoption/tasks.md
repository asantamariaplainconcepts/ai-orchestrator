# Tasks — atlas-shell-adoption

Kit first (canonical, regenerate), then the shell, then the screens, then the data patterns —
each step verified in both themes before the next builds on it.

## 1. Kit growth (canonical layer)

- [x] 1.0 Route the whole change through the `aio-design` skill: read `DESIGN.md`, compose from
      tokens only, run the validator before every push.
- [x] 1.1 Shell components in `docs/design-system/ui-kit/components.css`, values verbatim from
      the README's reference observations: `.shell` (sidebar + content grid on `--sidebar-w`),
      `.sidebar`, `.sidebar-brand`, `.nav-section` label, `.nav-item` (+ active =
      `--brand-soft`/`--brand`/medium), `.user-card`, `.topbar`, `.breadcrumbs`, `.page-title`.
- [x] 1.2 Data patterns: `.table` (dense rows, muted header, `--fs-13`), `.table-num`
      (right-aligned, tabular figures), `.pill` + semantic variants (soft fill, leading dot),
      `.stat-card` (+ semantic left-border variants).
- [x] 1.3 Regenerate `DESIGN.md` and the runtime adapter; verify the drift gate passes and **no
      new token** was needed (the README records the palette as sufficient — prove it).

## 2. The shell in the app

- [x] 2.1 `shared/ui/AppShell.tsx`: sidebar (product name from the copy catalogue — text only,
      DEC-021), nav sections/items from the route table, user-card placeholder (no invented
      identity; a labelled placeholder until #12), top bar with breadcrumbs + page title +
      theme toggle relocated.
- [x] 2.2 Both screens recomposed inside it; the project page's back-link becomes sidebar
      navigation + breadcrumbs. Feature components keep zero declared styles.
- [x] 2.3 Verify: both themes, keyboard focus visible on every interactive element including
      nav items; contrast of the active nav treatment **measured** (ADR-0004 discipline — with
      the oklch-aware measurement, not the rgb parser that lied last time).

## 3. Backlog data patterns

- [x] 3.1 Stories as `.table`: vendor id numeric-right, title, labels as pills, state as a
      status pill with leading dot; `—` via `.empty-value` for absent fields.
- [x] 3.2 Stat-card row above the table, every value computed from the live response only:
      story count, open count, trigger-labelled count, connector health. A fact the response
      cannot yield is not shown.
- [x] 3.3 The three absences (no Connector / no Stories / poll failed) keep distinct copy and
      treatment inside the new layout.

## 4. Verify

- [x] 4.1 `pnpm lint`, `pnpm typecheck`, design validator — all pass; zero styles declared in
      feature components (grep gate).
- [x] 4.2 E2E suite passes against the new shell; any assertion that keyed to removed layout is
      rewritten as an unfakeable assertion (role/body, not status/position) per ADR-0004.
- [x] 4.3 Screenshot pass in both themes across both screens (light+dark × Projects+project
      page), checked against the recorded observations.

## 5. Close-out

- [x] 5.1 Design README: move adopted items out of "screens we have not built yet" — the
      observations become implemented kit, and the section keeps only what remains unbuilt
      (toolbar, collapsed sidebar).
- [x] 5.2 Full verify sweep; CI green including E2E.
