## Context

The Automations tab grew one card at a time — the catalogue and workflow (#136, DEC-053), the
guided form (#231), the setup card (#229, #233, #262), the scratchpad (#189), the starter prompts
(#190) — and nobody ever decided which of them an Admin came for. The result is four stacked
full-width cards with the daily surface third, and an edit form that mounts inline above the
catalogue, so opening one moves every other thing on the page.

This is a **frontend-only presentation change**. No endpoint, request shape, response shape or
schema changes, so none of the backend conventions (vertical slices, CQS, contracts-only surface)
are engaged and none is deviated from. The API already returns everything the new presentation
needs: `workflowMembers()` derives the rail's relation tag from the Automations list the tab already
holds, so nothing new is fetched.

**Governing design-system artifacts:** `DESIGN.md` (the contract and generated token block),
`docs/design-system/` (canonical), and the `aio-design` skill's three-stage validator. The screen is
on the Platform theme since DEC-051, so it composes shadcn primitives from `src/frontend/shared/ui/`
unwrapped and takes every value through a Tailwind utility backed by a theme token. All copy resolves
through the typed catalogue in `src/frontend/shared/i18n/` (DEC-021).

## Goals / Non-Goals

**Goals:**

- Editing never moves the page — opening, dismissing and saving all leave scroll where it was.
- The tab's reading order matches how often each thing is read: flow, then catalogue, then tools.
- A catalogue entry says how it relates to the flow, from the same derivation the picture uses.
- Destructive and state-changing controls belong to the Automation, not to a row: delete and
  disable move into the panel, and delete asks twice.
- No capability becomes width-dependent.

**Non-Goals:**

- Changing what an Automation is, or any API it is configured through.
- Changing the workflow's graph derivation, its edges, or its drag semantics (#137, #165, #232).
- Fixing the setup card's dead Build button on a repository with no prompt files — a defect
  introduced by #269, filed separately, out of scope here.
- Fixing the zero-height connector rule between two workflow steps — pre-existing, filed separately.
- Deciding which of the spec-first loop's edges should be automatic (#269 scoped that out).
- Relocating `StarterPromptsSection`.

## Decisions

### D1 — One panel component, two containers, chosen by a media query

`ResponsiveDialog` in `shared/ui/responsive-dialog.tsx` renders a centred `Dialog` at pointer widths
and a bottom `Sheet` below `md`, mounting exactly one of them. Both are already the same radix
primitive in this repo's shadcn set, so focus handling, Esc and the overlay behave identically and
only position and sizing differ.

*Rejected — one Dialog restyled into a sheet with `max-md:` utilities.* It needs no JavaScript, but
the base `DialogContent` hardcodes `top-1/2 left-1/2 -translate-*`, so the sheet variant depends on
which of two same-property rules the generated CSS emits last. Correctness resting on utility
emission order is the kind of thing that survives review and breaks on a Tailwind upgrade.

*Rejected — render both and hide one with CSS.* Duplicates the form in the DOM, so duplicate `id`
attributes and two elements answering every selector. The E2E suite locates fields by `#trigger-label`.

Consequence, accepted: crossing the breakpoint with the panel open remounts the body. State lives in
the caller, so nothing is lost — the fields refill from the same state.

### D2 — The form stays inline JSX in a value, not a new component

The panel takes the existing `<form>` as `{form}`, built in `AutomationsSection` beside the fifteen
pieces of state it reads. Extracting a component would mean threading fifteen props or a context for
one caller.

The footer submits from **outside** the `<form>` via `form="automation-form"`, so the body scrolls
while Save stays put. Requires the id to be stable, which is why it is a module constant.

*Rejected — footer inside the form.* Save then scrolls out of reach on a long form, which is the
problem the panel exists to fix, one level down.

### D3 — The rail's relation tag is derived, never stored

`workflowMembers(automations)` in `workflowGraph.ts` returns the ids `workflowChains()` draws. The
rail asks that set; the canvas draws the same chains. One implementation, so a row cannot claim
membership the picture does not show.

*Rejected — a flag on the Automation.* It could disagree with the edges, and DEC-053 already settled
that membership is derived and not stored.

### D4 — Delete and disable move into the panel; the row is a single control

A catalogue entry is one `<button>` spanning the row, named for the Automation it edits. Delete and
disable move to the panel footer, delete behind a two-step confirm.

*Rejected — a `⋯` menu per row.* Keeps the actions on the row at the cost of a button nested inside
the row button (invalid HTML) or a flex sibling that re-introduces the mis-aim target beside Edit.

*Rejected — `AlertDialog` for the confirm.* A dialog stacked on a dialog for one destructive verb;
the two-step button is what the design review specified and it needs no second overlay.

Consequence, accepted: enable/disable for a standalone Automation is now two clicks (row → footer)
rather than one. ADR-0006 asks that a capability be reachable, not that it be at zero cost.

### D5 — Compact nodes keep the DOM shape the canvas tests measure

A node becomes one row: trigger, Gate chip, prompt file, state, approval toggle, Edit. The wrapper
holding a node and its connector keeps `[node, connector]` as its two children, and the chain keeps
`max-w-[520px]`, because `VerticalWorkflowCanvas_Should_Constraint` measures both geometrically. The
Gate chip stays ahead of the approval toggle in DOM order — the test locates
`[title='A person approves the plan']` and asserts the first match reads `Approval`.

The action and runtime leave the node: with one action in the catalogue they were the same word on
every node, and the runtime is readable in the panel that can change it.

### D6 — Below `xl` the rail keeps only what the flow does not already show

In-workflow entries are hidden below `xl` (`hidden xl:block`) rather than removed, and the group
heading switches from *Catalogue* to *Standalone*. The whole rail is hidden when nothing is
standalone. One list, one source of truth, no duplicated rows.

### D7 — First-run setup is inline; every other tool opens over the tab

While `automations.length === 0`, `WorkflowSetupSection` renders inline and the toolbar drops its
setup action, so one surface is never reachable two ways at once. The tool panels neutralise the
components' own `Card` chrome with a scoped `[&_[data-slot=card]]` rule at the call site, leaving
both components untouched — what changed is where they live, not what they are.

## Risks / Trade-offs

- **A dialog is portalled outside `<main>`, so assertions scoped to `main` stop seeing the form.** →
  Two E2E cases updated to scope to the dialog rather than to `main`. Found by reading the suite
  before changing the code, not by a red run.
- **The footer's `form=` association is invisible to a reader of either fragment.** → The id is a
  named module constant with the reason in its doc comment.
- **`ResponsiveDialog` is a new shared component, and a seam with no consumer is an anti-pattern
  (RULE-007).** → Three consumers land with it: the edit panel, the setup tool, the scratchpad tool.
- **Enable/disable costs one more click.** → Accepted; stated in D4.
- **Moving the derived summary to the tab header means the canvas alone no longer states it.** →
  The spec delta moves the requirement to the tab rather than dropping it.
- **The tab renders `WorkflowSetupSection` inline on a first run, which is exactly the state where
  #269's Build button is disabled on a repository with no prompt files.** → Out of scope and filed
  separately; recorded here because this change makes an existing defect easier to meet, and
  discovering that from the issue tracker is better than discovering it from a user.

## Migration Plan

None required. No schema, no data, no contract: the change is deployed by shipping the frontend
bundle, and rollback is the previous bundle. The removed copy key (`automations.new.close`) is
internal to the frontend catalogue.

## Open Questions

None blocking. One recorded observation: `automation-configuration` still described the workflow
chain as a horizontally-scrolling single row at wide viewports, which #232 replaced with one top-down
layout at every width. Since this change modifies that same requirement, and a MODIFIED block
replaces the requirement wholesale at archive time, copying the stale sentence forward would have
re-asserted something already false — so the delta corrects it to match shipped behaviour and the
E2E suite. That is a spec-to-code reconciliation, not a behaviour change.
