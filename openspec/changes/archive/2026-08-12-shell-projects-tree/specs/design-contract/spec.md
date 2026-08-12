## MODIFIED Requirements

### Requirement: the kit provides the application shell and data patterns

The UI kit SHALL provide, composed exclusively from existing canonical tokens: an application
shell of persistent sidebar (`--sidebar-w-expanded` wide, `--surface` background, product name at
the top, user card pinned at the bottom) and top bar (breadcrumb row and page-title row); nav
items whose active state is a `--brand-soft` fill with `--brand` text at medium weight; uppercase
section labels (`--fs-11`, semibold, `--fg-subtle`); a dense table treatment (muted header,
right-aligned numeric columns with tabular figures, `—` for absent values); a status pill with a
leading dot in the semantic families; and a stat card with a coloured left border in a semantic
colour. Values SHALL be the measured ones recorded in the canonical README's reference
observations; introducing a new token for these components requires amending the canonical layer
first and recording why the recorded mapping was insufficient.

The kit SHALL additionally provide **one shared chip for a Run's state**, in `shared/ui/`, and every
surface that shows a Run's state SHALL render through it. It SHALL follow the vocabulary rule the
locus chip already establishes (`src/frontend/shared/ui/locus.tsx`): a **glyph beside a word**,
never colour alone. Its labels SHALL resolve through the typed i18n catalogue — an internal state
identifier SHALL NOT reach the screen as user-facing copy.

The chip SHALL give **distinct states distinct treatments**. States that mean different things to a
reader SHALL NOT share one appearance; in particular `Executing` — work happening now — SHALL NOT
render identically to `Succeeded` — work finished. A state vocabulary that collapses "is running"
into "is done" misinforms every surface whose purpose is to show what is live.

A state treatment defined locally inside a feature component SHALL NOT exist once this chip does,
for the reason the gate chip's own docstring gives: two chips that merely look alike drift the first
time one is restyled, and the design gate cannot catch it because the tokens are right in both.

#### Scenario: a shell component needs a colour or size

- **WHEN** any of these components is styled
- **THEN** it uses an existing canonical token, and the validator finds no raw hex or pixel
  values in the kit

#### Scenario: no brand assets

- **WHEN** the shell renders the product identity area
- **THEN** it is text from the copy catalogue — no logo, wordmark, or brand imagery exists in
  the repository (DEC-021)

#### Scenario: one state, one appearance everywhere

- **WHEN** the same Run state is rendered on the Run detail, in the project's Runs list, and in the
  sidebar tree
- **THEN** all three render through the one shared chip and are indistinguishable from each other

#### Scenario: running does not look finished

- **WHEN** an `Executing` Run and a `Succeeded` Run are rendered side by side
- **THEN** their chips differ, in glyph and word, not only in colour

#### Scenario: state is never colour alone

- **WHEN** a state chip is rendered
- **THEN** it carries a glyph and a word, so the state survives greyscale and colour-blindness

#### Scenario: no local state treatments remain

- **WHEN** the frontend is searched for state-to-appearance mappings defined inside feature
  components
- **THEN** none is found — the shared chip is the only one
