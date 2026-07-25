# Design — design-system

## Verified reality (checked, not assumed)

Atlas was inspected directly rather than described from memory — its stylesheet was fetched and
its computed values read from the live page:

- **Colour is OKLCH throughout**, on one brand/neutral hue (`258`) with named semantic hues:
  `155` ok, `75` warn, `52` orange, `95` yellow, `25` danger, `230` info, `305` violet. Each
  semantic colour ships as a triple — base, `-soft` fill, `-border`.
- **Both themes exist.** `:root` is light with `color-scheme: light`; `[data-theme=dark]`
  overrides the same variable names with `color-scheme: dark`. Component rules never branch on
  theme — they consume variables, and the theme swaps the values.
- **Scales are explicit and small:** type `11/12/13/14/16/20/26`, radius `4/6/8/10/12`, spacing
  `4/8/12/14/16/20/24/32`, two shadows plus a focus ring, two transition durations
  (`.12s` / `.18s`), and layout constants (sidebar 280/64, header 64).
- **Typeface** is Geist with a system fallback stack, Geist Mono for code.
- **Components are pure token composition** — e.g. `background: var(--bg-muted); border: 1px solid
  var(--border); border-radius: var(--r-3)`. Focus is `outline: 2px solid var(--brand);
  outline-offset: 1px`, consistently.

The frontend today has **no styling whatsoever** and one screen (Projects), so there is nothing to
migrate and no competing artifact to reconcile — the situation the source project wished it had.

## Decisions

### D1 — Adopt Atlas's token *values*, not just its vibe

DEC-021 authorises deriving tokens (spacing, type scale, colour feel) and forbids brand assets.
Taking the actual values gives real visual consistency with a product our users already use, and
the values are colour coordinates and pixel scales, not creative assets. **We take:** the variable
names, the OKLCH values, both themes, the scales. **We do not take:** logo, wordmark, gradient
artwork, or any imagery. Provenance is stated in the canonical README rather than left implicit.

**Rejected: inventing a palette "inspired by" Atlas.** It would drift from the reference
immediately, and it would throw away a system that is demonstrably coherent — the neutrals share
the brand hue at low chroma, which is why the greys look deliberate rather than dead.

### D2 — Both themes, selected by `[data-theme]` with a media-query default

Same mechanism as the reference. `:root` carries light, `[data-theme=dark]` overrides, and a
`prefers-color-scheme` block sets the initial value so a first visit respects the OS. The theme is
one attribute on `<html>`; no component may branch on it.

**Consequence accepted:** every colour must be a variable. A raw hex cannot theme, which is
exactly why the adherence stage forbids raw hex — the gate and the theming requirement are the
same rule seen from two sides.

### D3 — CSS custom properties are canonical; `tokens.ts` is generated and subordinate

L1 is CSS because that is where a browser resolves themes at runtime. `tokens.ts` exists so
TypeScript code can reference token *names* with autocomplete and type safety, and it is
**generated, never authored**. It exports names bound to `var(--…)` references rather than copied
literal values, so a token change cannot leave the adapter stale in substance — only its list of
names can drift, which is what the drift stage checks.

**Rejected: authoring `tokens.ts` by hand** (the source project's incident: a `tokens.ts` header
declaring itself a competing source of truth). **Rejected: a CSS-in-JS runtime** — it would put
values in JavaScript, defeating both theming and the gate.

### D4 — The UI kit is plain CSS classes over tokens

One experience (DEC-021) means one kit. Components are CSS classes composed from variables, in the
Atlas idiom, imported by React components — no component library dependency, no styling
abstraction layer. The kit covers what the app actually needs now: layout shell, button, input,
card, table, badge, and the empty/loading/error states the Projects screen already renders.

**Rejected: adding a component library** (shadcn, MUI, …) — it would introduce its own token
system as a second source of truth, which is the exact failure mode this change exists to prevent.
**Rejected: speculative components** — same rule that kept `Backlog` and `Agents` from being
scaffolded empty in Phase 1.

### D5 — The validator is one script, three stages, run in the lint lane

Adherence, drift, skill hygiene — as a single shell script so it runs identically locally and in
CI. It is a **new step in the existing lint lane**, not a new lane: the frontend lint job already
enforces the i18n rule, and stage 1 absorbs it rather than duplicating it.

Expect false positives; the reference project hit them (`#123` issue references matching a hex
pattern). The patterns are scoped to `src/frontend/**` source files, exclude comments and
generated files, and the script is runnable locally so a misfire is discovered before pushing
rather than in review.

### D6 — Content fundamentals live in the canonical README, not in people's heads

Voice and register, sentence case for labels, verb-first buttons, what an empty state says, how an
error is phrased, and the fact that copy is English in the typed catalogue (DEC-021). Without this
the i18n gate enforces only *where* copy lives, never *how it reads* — and the product's own
vocabulary (Agent, Connector, Automation, Run, Plan — DEC-005) has to be used consistently in the
interface, not just in the code.

## Risks

- **Geist availability.** It is OFL-licensed, so self-hosting is permitted; the fallback stack
  degrades to the system UI font. The kit must be legible on the fallback — the type scale does
  not depend on Geist's metrics.
- **A gate that cries wolf gets disabled.** Mitigated by scoping patterns tightly, excluding
  generated files, and making the validator locally runnable. If a stage misfires more than it
  catches, the correct response is to fix the pattern, not to weaken the rule to advisory.
- **Dark mode doubles the review surface** for every future screen. Accepted: the alternative —
  adding a second theme after screens exist — is strictly harder.
