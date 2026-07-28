# Design system — canonical

**This directory is the only place design values are authored.** Everything else is generated
from it or reads it. If another file ever declares itself a source of design truth, that is a
defect: delete it, or make it a generated artifact of this layer.

> **Retiring (DEC-051).** The product is migrating to the Platform theme
> (`@plainconceptsplatform/ui-theme`: shadcn/ui + Tailwind v4), one screen per change. During
> the migration both systems load side by side and a screen is styled by exactly one of them —
> new screens use the theme, and this kit keeps its authority over the screens still on it.
> Two token names carry an `atlas-` prefix (`--atlas-border`, `--atlas-info`) because the theme
> declares the bare names on `:root`. This directory is deleted when the last kit screen falls.

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

## Reference observations

The reference application was inspected in use and its components **measured**, not eyeballed.
Most of what was recorded here is now **implemented in the kit** (the atlas-shell-adoption
change): the shell (sidebar, nav items, section labels, user card, top bar, breadcrumbs), the
dense table, status pills, and stat cards. The kit is the source of truth for those; this section
keeps only what remains unbuilt, plus the divergences worth remembering.

Still recorded, not implemented — no screen needs them yet:

- **Table toolbar** — column-visibility and export controls above data tables. Their actions have
  no backend; adding the chrome first would be a lever wired to nothing.
- **Stat cards as filters** — the reference's cards double as filters (clicking one narrows the
  table). Ours display only; filtering arrives when a screen needs it.
- **Collapsed sidebar** — `--sidebar-w-collapsed` exists; the collapse interaction is its own
  item.

**Measured divergences from the reference** (accessibility overrides the reference — the same
precedent as the dark brand in Provenance):

- **Active nav text is `--brand-text`, not `--brand`.** The two coincide in light mode. In dark
  they cannot: the dark `--brand` was lowered for AA against white button text, which leaves it
  at 3.55:1 *as* text on the dark `--brand-soft`. `--brand-text` (dark `oklch(78% 0.09 258)`)
  measures 6.74:1. One hue, two jobs, two tokens.
- **Section labels are `--fg-muted`, not the reference's `--fg-subtle`** — measured 2.75:1 on the
  dark sidebar surface, versus 4.79:1 for `--fg-muted`.
- **`--ls-caps` (0.06em)** joined the type scale for uppercase micro-labels; the scale had no wide
  tracking step because no uppercase label existed before the sidebar did.
- The reference's off-scale values (a ~13.5px nav label, a small global negative tracking) were
  snapped to the scale rather than reproduced. A closed scale is worth more than a pixel of
  fidelity.

## Changing something

1. Edit the token or component **here**.
2. Regenerate: `node .claude/skills/aio-design/scripts/sync-design-tokens.mjs --write`
3. Run the validator: `bash .claude/skills/aio-design/scripts/validate-design-system.sh`
4. Commit L1 and the regenerated L2/L3 together — CI fails on drift between them.
