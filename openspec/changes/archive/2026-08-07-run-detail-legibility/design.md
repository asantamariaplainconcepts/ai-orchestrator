## Context

`RunScreen.tsx` lays the detail out as `lg:grid-cols-[minmax(0,1fr)_280px]` with `RunChanges`
mounted in the 280px rail; the failure reason is a rail row and the failure's decisions are header
actions. Frontend only, Platform theme (DEC-051), copy through the typed catalogue (DEC-021).
Governing artifacts: `DESIGN.md`, `docs/design-system/`, the aio-design validator.

## Goals / Non-Goals

**Goals:** the diff readable at body width with per-file navigation; a failure answerable where it
is stated; no blank holes; a phone that never scrolls sideways.

**Non-Goals:** review actions on the diff; API changes; syntax highlighting; touching the
transcript's follow logic.

## Decisions

### D1 — The body column swaps content, the rail keeps anchors

Changes joins Plan/Output in the main column; the rail's CHANGES card becomes summary-only (PR,
files, ±) with an anchor link (`#run-changes`) that scrolls to the block. One diff on the page,
one summary pointing at it.

### D2 — The banner is the failure's whole answer

One banner component at the top of a failed Run: full reason, `Run again` (story Runs only — a
change Run's re-run shape is #275 follow-up territory and stays out), `Dismiss`, and the remedy
link. The header's failure actions are removed with it — the requirement pins "nowhere else".

*Remedy map, explicit and short:* reason contains "Credential could not be resolved" →
`/projects/:id?tab=settings`; reason contains the prompt-read refusal ("could not be read" /
"prompt") → `?tab=automations`. Matching is against the executor's own stable sentences (the
closed refusal set), listed in one place; anything else → no link.

### D3 — Diff rendering grows in place

`RunChanges` gains: line numbers computed from hunk headers (`@@ -a,b +c,d @@` — data already in
the patch), a sticky per-file header (`position: sticky` inside the scroll container), per-file
collapse (second+ collapsed at `< md` by default), and the mobile wrap: `whitespace-pre-wrap
break-words` with the ± marker in an absolute gutter column and `direction`-safe left truncation
for paths (`truncate` + `dir="rtl"` span trick avoided — plain `truncate` on a reversed-padding
container; implementation detail, tokens only).

### D4 — Pagination by hunk lines

"Show N more lines" reveals the remainder of a file's patch in chunks (a page ≈ 40 lines) rather
than rendering thousand-line patches eagerly — the vendor's 200k patch limit already bounds the
worst case, but a phone should not pay it by default.

## Risks / Trade-offs

- **E2E/functional tests pinned to the old placement.** → Surveyed before coding; updated not
  weakened (the turn-6 discipline).
- **The remedy map could rot when executor messages change.** → It matches the executor's closed
  refusal sentences and lives in one named constant beside a comment pointing at RunExecutor; a
  changed message degrades to banner-without-link, never a wrong link.
- **Sticky headers inside cards can fight the page scroll.** → Sticky within the file card's own
  scroll context only; verified in the browser at both breakpoints.

## Migration Plan

None. Rollback is the previous bundle.

## Open Questions

None.
