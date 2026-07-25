# Design system — canonical

**This directory is the only place design values are authored.** Everything else is generated
from it or reads it. If another file ever declares itself a source of design truth, that is a
defect: delete it, or make it a generated artifact of this layer.

## The four layers, one direction

```
L1  docs/design-system/            CANONICAL — humans edit here, nothing overrides it
      tokens/*.css  ui-kit/*.css  README.md
        │  node .claude/skills/aio-design/scripts/sync-design-tokens.mjs --write
        ▼
L2  /DESIGN.md                     GENERATED — the agent-facing design contract
        ▼
L3  src/frontend/shared/design/tokens.ts   GENERATED — typed names for TS code
        ▼
L4  .claude/skills/aio-design/     PROCEDURAL — a router; contains NO values
```

**Conflicts are always resolved by regenerating downward.** Never edit a lower layer to match
reality — that is how a project ends up with four competing token files and no owner.

## What is here

| File                    | Owns                                                                                |
| ----------------------- | ----------------------------------------------------------------------------------- |
| `tokens/colors.css`     | Brand, backgrounds, surfaces, foregrounds, borders, semantic families — both themes |
| `tokens/typography.css` | Font stacks, the type scale, weights, line heights, letter spacing                  |
| `tokens/spacing.css`    | The spacing scale                                                                   |
| `tokens/radius.css`     | Corner radii                                                                        |
| `tokens/elevation.css`  | Two shadows and the one focus ring — both themes                                    |
| `tokens/motion.css`     | Durations and easing, plus the reduced-motion override                              |
| `tokens/layout.css`     | Shell dimensions and the reading measure                                            |
| `ui-kit/base.css`       | Element defaults — what unstyled HTML should look like here                         |
| `ui-kit/components.css` | The components the app actually renders                                             |

## Colour, and why the greys are not grey

Colour is OKLCH throughout, on **one brand/neutral hue (258)**. The neutrals carry that hue at
very low chroma, which is what makes them read as part of the palette rather than as dead grey.
Semantic families each ship a triple — base for text and icons, `-soft` for fills, `-border` for
outlines — so a status treatment is three consistent decisions rather than three guesses.

**Both themes are first-class.** `:root` is light, `[data-theme="dark"]` overrides the same
variable names, and a `prefers-color-scheme` block picks the initial value. Components must never
branch on the theme — they consume variables whose values the theme swaps. This is also why a raw
hex is a lint failure: a literal cannot theme.

## Scales are closed

Type, spacing, radius, elevation and motion are short, named scales. A value outside a scale is a
design decision, not a convenience — extend the scale here rather than writing a one-off literal
in a component. The type scale is chosen to stay legible on the fallback font stack; nothing
depends on Geist's specific metrics.

## Content fundamentals

Copy is part of the design system. All user-facing text lives in the typed i18n catalogue
(`src/frontend/shared/i18n/`) in **English** (DEC-021) — the lint gate enforces _where_ it lives,
and this section governs _how it reads_.

**Voice.** Plain, precise, unhurried. This is an operations tool used by colleagues who are
scanning, so we favour the shortest wording that is still unambiguous. No exclamation marks, no
apologies, no personality. Address the user as _you_; never refer to the product as _we_.

**Vocabulary is locked** (DEC-005) and the interface must use it exactly: **Agent** (never "pod",
"worker" or "bot"), **Connector**, **Automation**, **Run**, **Plan**. Run states are proper
nouns in the UI: Queued, Planning, AwaitingApproval, Executing, Succeeded, Failed, Cancelled.

**Labels and buttons.** Sentence case, never Title Case. Buttons are verb-first and name the
outcome — "Create project", "Approve plan", "Cancel run" — never "OK", "Submit" or "Yes".

**The four state patterns**, so every list and form handles them the same way:

| State        | Pattern                                                                | Example                                                  |
| ------------ | ---------------------------------------------------------------------- | -------------------------------------------------------- |
| Empty        | State the absence, then the next action                                | "No projects yet. Create one to connect a backlog."      |
| Loading      | Present participle, no ellipsis theatre                                | "Loading projects…"                                      |
| Error        | What failed, in the user's terms; never a stack trace or an error code | "Could not load projects."                               |
| Confirmation | Name the exact object and the consequence                              | "Cancel run for story #42? The Agent stops immediately." |

**Numbers and identifiers.** Ids, branch names and story keys are monospace (`.mono`) with
tabular figures so a column of them aligns and can be compared by eye. Numeric columns are
right-aligned. Timestamps are relative for recency ("2 minutes ago") and absolute once older than
a day.

**Absent values are an em dash (`—`), never blank and never "N/A".** A blank cell is ambiguous
between "there is no value" and "we failed to load it"; the dash says the first explicitly.

## Provenance

Token values derive from the **Atlas Plain Concepts** style reference, as authorised by DEC-021 —
its published stylesheet was inspected and its structure and values adopted: the OKLCH approach,
the single brand/neutral hue, the semantic triples, both themes, and the type/spacing/radius
scales. Atlas is an internal product of the same company and serves this stylesheet publicly.

**No brand assets are included** — no logo, no wordmark, no gradient artwork, no imagery. This is
a style reference, not a brand transfer.

**Where the reference and a locked decision disagree, the decision wins**, and this file cites it:

- **DEC-021** — one experience (desktop-first web), so there is **one** UI kit, not a kit per
  experience; and product copy is English, where the reference's own product is not our concern.
- **DEC-005** — the product's coined vocabulary governs interface copy regardless of what any
  reference calls similar concepts.
- **Accessibility overrides the reference's dark brand.** The reference uses a lighter brand in
  dark mode; measured against white button text that yields **3.98:1**, below the 4.5:1 WCAG AA
  threshold this system requires for body-sized text. Our dark `--brand` is therefore
  `oklch(54% …)` rather than `oklch(60% …)`, measuring **5.10:1**. Light mode needed no change
  (7.55:1). Verified by measuring computed values in a browser, not by eye.

## Reference observations, for screens we have not built yet

The reference application was inspected in use and its components **measured**, not eyeballed.
These patterns are **recorded, not implemented** — the kit deliberately contains only what our app
renders today, and adding components ahead of a screen is speculative surface. When the screen
arrives, start here rather than re-deciding. Values below are expressed in our tokens:

- **Sidebar** — `--sidebar-w-expanded` wide, `--surface` background, product mark at the top and a
  user card pinned at the bottom.
- **Nav item** — `--sp-2`/`--sp-3` padding, `--r-2` radius, regular weight. **Active state is
  `--brand-soft` fill with `--brand` text and medium weight** — a tint, not a heavy highlight.
  (Measured exactly: the reference's active background and text are the same two values our
  `--brand-soft` and `--brand` already hold.)
- **Section label** — `--fs-11`, semibold, uppercase, roughly `0.06em` tracking, `--fg-subtle`.
- **User card** — `--bg-muted` fill, `1px --border`, `--r-3`, `--sp-2` padding.
- **Top bar** — `--surface`, `--sp-4` block / `--sp-7` inline padding.
- **Breadcrumbs** — `--fs-12`, `--fg-muted`, small gap; then the page title row beneath.
- **Filter/stat cards** — a row of cards each with a **coloured left border** in a semantic colour,
  label left and large count right. They double as filters: clicking one narrows the table.
- **Table** — dense rows, muted header, numeric columns right-aligned with tabular figures, status
  as a soft pill with a leading dot, `—` for absent values, and a toolbar above carrying
  column-visibility and export controls.

**The palette needed nothing added.** Every treatment maps to a token already here — `--brand-soft`
for active nav, `--ok-soft` for status pills, `--violet` for percentage emphasis, the semantic
families for card borders. That is a signal the palette is sufficient, not merely pretty.

**Where we will diverge:** the reference uses a few off-scale values (a ~13.5px nav label, a small
global negative tracking). We snap to the scale — `--fs-13` — rather than reproducing them. A
closed scale is worth more than a pixel of fidelity.

## Changing something

1. Edit the token or component **here**.
2. Regenerate: `node .claude/skills/aio-design/scripts/sync-design-tokens.mjs --write`
3. Run the validator: `bash .claude/skills/aio-design/scripts/validate-design-system.sh`
4. Commit L1 and the regenerated L2/L3 together — CI fails on drift between them.
