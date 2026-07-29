# Proposal: collapsible-sidebar

## Why

Issue #126 (ACT-002; UC-026; no new business rules). The sidebar takes a fixed 280px on every screen,
and it costs most where space matters most: the board's columns, the canvas's chains, a Run's log.

**Three of the issue's premises did not survive being checked, and two of them change the work:**

- **The token is real, and the shell has never used it.** The issue is right that
  `--sidebar-w-collapsed: 64px` has sat unused — it and `--sidebar-w-expanded: 280px` are defined in the
  canonical layer, `docs/design-system/tokens/layout.css`. What no one had noticed is the consequence:
  the shell hard-codes `md:grid-cols-[16rem_1fr]`, and **16rem is 256px**, so the rendered sidebar has
  disagreed with its own canonical token by 24px for as long as both have existed. Wiring the shell to
  the variable is what makes the collapse possible *and* closes that drift (design D1).
- **The remembered-preference mechanism exists once, not twice.** `aio:backlog-view` in `ProjectScreen`
  is the only `localStorage` key in the app. The issue's plan to "extract it and move the existing two
  onto it" therefore has one call site to move, not two — the extraction is still right, for the
  repository's own reason (the second occurrence is when a pattern graduates), but it is a smaller change
  than the issue describes (design D3).
- **Where the miscount came from is worth naming:** `AutomationsSection.tsx` carries a comment saying "a
  genuine preference like the board's, remembered the same way… whether a reader wants rows or a shape"
  above code that remembers nothing. The rows-or-shape toggle was removed when #136 made the catalogue
  and the canvas two sections side by side; the comment survived and now describes a behaviour that does
  not exist. It gets deleted here.

## What changes

- **A collapse control from the medium breakpoint up** (design D2). Below it the shell already folds
  into the sheet menu and nothing changes.
- **Collapsed is a 64px icon rail, not a hidden panel**, with the width defined as a real CSS variable
  and the token entries wired to it (design D1). Every destination stays one click away and the inbox
  count stays visible on its icon — that is what makes this a rail rather than a hidden panel, and it is
  the same reasoning the design contract already applies at phone width.
- **The choice is remembered**, through **one shared hook** that `ProjectScreen`'s view toggle also moves
  onto, with its behaviour unchanged (design D3).
- **Collapsed entries name themselves** to assistive technology and on hover, because the label is no
  longer on screen (design D4).
- **The orphaned comment goes.**

## Impact

- Specs: `frontend-architecture` — one MODIFIED requirement (the shell's sidebar may be collapsed to a
  rail, carrying its two existing scenarios).
- Code: `AppShell`, one new shared preference hook, `ProjectScreen` moved onto it, the CSS variables
  defined, `tokens.ts` entries made real, and one stale comment removed.
- No backend change, no API change, no schema change.

## Out of scope

- Resizing the sidebar to arbitrary widths, and collapsing anything else.
- Changing what the sidebar contains.
- Giving the other unused token entries meaning — only the two this change needs are wired up, and a
  sweep for the rest is its own item.
