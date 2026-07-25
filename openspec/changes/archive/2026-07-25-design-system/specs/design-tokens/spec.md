# design-tokens

## ADDED Requirements

### Requirement: one canonical home for design values

`docs/design-system/` SHALL be the only place design values are authored: token CSS, the UI kit,
and the content fundamentals. No other file SHALL declare itself a source of design truth, and
any file derived from it SHALL say so in its header.

#### Scenario: a competing token file

- **WHEN** a second file defines design values independently of `docs/design-system/`
- **THEN** that is a defect, resolved by deleting it or making it a generated artifact of L1

### Requirement: every colour is a themeable token

Colour SHALL be expressed as CSS custom properties in OKLCH. The palette SHALL provide, at
minimum: brand (base, hover, soft, border, on-brand), backgrounds (`bg`, `bg-muted`,
`bg-subtle`), surfaces (`surface`, `surface-2`, `surface-3`), foregrounds (`fg`, `fg-2`,
`fg-muted`, `fg-subtle`), borders (`border`, `border-strong`), and semantic families for success,
warning, danger and info — each with a base, a `-soft` fill and a `-border`.

#### Scenario: a raw colour cannot theme

- **WHEN** a component needs a colour
- **THEN** it references a variable, because a literal value cannot change with the theme

### Requirement: light and dark are both first-class

`:root` SHALL define the light theme with `color-scheme: light`; `[data-theme="dark"]` SHALL
override the same variable names with `color-scheme: dark`. Component styles SHALL NOT branch on
the theme — they consume variables whose values the theme swaps. The initial theme SHALL follow
`prefers-color-scheme` and be overridable by setting the attribute.

#### Scenario: switching theme restyles the app

- **WHEN** `data-theme` changes on the document element
- **THEN** the whole interface restyles with no component re-render logic and no per-component
  theme conditionals

#### Scenario: both themes are legible

- **WHEN** any kit component renders in either theme
- **THEN** body text meets at least WCAG AA contrast against its background

### Requirement: explicit, small scales

Typography, spacing, radius, shadow, motion and layout SHALL each be a named, closed scale — a
value outside the scale is a design decision, not a convenience. The scales SHALL cover: a type
scale with a monospace family for identifiers and code, a spacing scale, a radius scale, at least
two elevation shadows plus a focus ring, at least two transition durations, and the layout
constants the shell needs.

#### Scenario: an arbitrary value

- **WHEN** a component needs a size not on a scale
- **THEN** either an existing step is used, or the scale is extended in L1 — never a one-off
  literal in the component

### Requirement: one UI kit, composed only from tokens

There SHALL be exactly one UI kit (one experience, DEC-021), and its components SHALL be composed
solely from tokens. The kit SHALL cover only what the application actually renders; speculative
components SHALL NOT be added ahead of a screen that needs them.

#### Scenario: focus is consistent and visible

- **WHEN** any interactive component receives keyboard focus
- **THEN** it shows the shared focus treatment, so focus is never invisible or per-component

### Requirement: content fundamentals are part of the system

The canonical README SHALL define voice and register, label and button-copy style, and the
patterns for empty, loading, error and confirmation copy, in English (DEC-021). It SHALL require
the product's coined vocabulary (DEC-005: Agent, Connector, Automation, Run, Plan) to be used
exactly in the interface.

#### Scenario: copy is designed, not improvised

- **WHEN** a new screen needs an empty state
- **THEN** its wording follows the documented pattern rather than being invented per screen

#### Scenario: vocabulary drift

- **WHEN** interface copy calls an Agent a "worker" or a "pod"
- **THEN** that contradicts DEC-005 and is a defect

### Requirement: provenance is recorded

The canonical README SHALL record that token values derive from the Atlas style reference
(DEC-021), that no brand assets are included, and where the product's own decisions override the
reference.

#### Scenario: the reference and a locked decision disagree

- **WHEN** the visual reference implies something a `DEC-*` forbids
- **THEN** the decision wins and the README cites its ID
