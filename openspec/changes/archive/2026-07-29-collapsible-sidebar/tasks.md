# Tasks — collapsible-sidebar

- [x] 1.1 The shell's grid reads `var(--sidebar-w-expanded)` / `var(--sidebar-w-collapsed)` instead of
      the hard-coded `16rem`, which also corrects the 24px drift from the canonical 280px (design D1).
- [x] 2.1 One shared remembered-preference hook: lazy read, defensive write, default on refusal
      (design D3).
- [x] 2.2 `ProjectScreen`'s list⇄board toggle moves onto the hook, behaviour unchanged.
- [x] 3.1 `AppShell` gains the collapse control from the medium breakpoint up, and the shell's width
      follows the state (design D2).
- [x] 3.2 Collapsed renders an icon rail: every destination one click away, the inbox count visible on
      its icon.
- [x] 4.1 Collapsed entries carry an accessible name and a hover title (design D4).
- [x] 5.1 The orphaned rows-or-shape preference comment in `AutomationsSection.tsx` is deleted.
- [x] 6.1 i18n keys for the collapse and expand controls.
- [x] 7.1 E2E: collapsing gives the content the width and every destination stays reachable; the inbox
      count is visible while collapsed; the choice survives a reload; below the breakpoint the control is
      absent.
- [x] 8.1 Verified in both themes and by keyboard; the design validator passes.
- [ ] 9.1 CI green; evidence on #126.
