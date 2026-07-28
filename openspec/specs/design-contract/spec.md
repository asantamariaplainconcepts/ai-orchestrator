# design-contract Specification

## Purpose
TBD - created by archiving change design-system. Update Purpose after archive.
## Requirements
### Requirement: derivation is strictly one-way

The design system SHALL have four layers with a single direction of derivation: canonical
`docs/design-system/` → generated `DESIGN.md` → generated runtime token adapter → the value-free
design skill. A conflict between layers SHALL be resolved by regenerating downward. A lower layer
SHALL NOT be edited to match reality.

#### Scenario: a generated file disagrees with the canonical tokens

- **WHEN** `DESIGN.md` or the adapter no longer matches `docs/design-system/`
- **THEN** they are regenerated; the canonical tokens are not edited to match them

### Requirement: DESIGN.md is the agent-facing contract at a predictable path

`DESIGN.md` SHALL live at the repository root and SHALL contain a generated token block plus
value-free prose on applying the system. It is the one file an agent reads before UI work, and it
SHALL be guaranteed current by the drift gate.

#### Scenario: an agent starts UI work

- **WHEN** any agent begins a frontend task
- **THEN** `DESIGN.md` at the repo root tells it the tokens and the rules, without hunting

### Requirement: generated files declare themselves

Every generated artifact SHALL carry a header stating that it is generated, that it must not be
edited by hand, and the exact command that regenerates it.

#### Scenario: someone opens the adapter to change a value

- **WHEN** a contributor opens the generated token adapter
- **THEN** its first lines tell them where to edit instead and how to regenerate

### Requirement: the generator is dependency-free and runs the same everywhere

Generation SHALL be a single script requiring no package installation beyond the runtime already
present, and SHALL support a `--check` mode that reports drift without writing.

#### Scenario: check mode in CI

- **WHEN** the generator runs with `--check` and the working tree is current
- **THEN** it exits zero and writes nothing

### Requirement: the runtime adapter binds names, not copied values

The generated TypeScript adapter SHALL expose token **names** bound to their CSS variable
references, so that changing a token's value in the canonical layer cannot leave the adapter
stale. Copying literal colour or size values into TypeScript SHALL NOT occur.

#### Scenario: a token value changes

- **WHEN** a colour's value changes in the canonical CSS
- **THEN** the running application reflects it with no change to the adapter's contents

### Requirement: the design skill contains no values

The design skill SHALL be a procedural router — read `DESIGN.md` first, compose kit components
rather than inlining styles, resolve copy through the i18n catalogue, run the validator — and
SHALL contain no literal token values, so it cannot drift.

#### Scenario: skill hygiene

- **WHEN** the design skill's files are scanned for literal colour or size values
- **THEN** none are found

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

#### Scenario: a shell component needs a colour or size

- **WHEN** any of these components is styled
- **THEN** it uses an existing canonical token, and the validator finds no raw hex or pixel
  values in the kit

#### Scenario: no brand assets

- **WHEN** the shell renders the product identity area
- **THEN** it is text from the copy catalogue — no logo, wordmark, or brand imagery exists in
  the repository (DEC-021)

### Requirement: the visual source of truth is the Platform theme

App code SHALL take every visual decision from the Platform theme's tokens — through Tailwind
utilities or shadcn components used directly, never through a wrapper library. Raw hex colours,
raw pixel values and non-approved fonts SHALL NOT appear in app code, enforced in CI as before.
During migration, a screen SHALL be styled by exactly one system — the legacy kit or the theme —
and the shell SHALL fold usably below the theme's medium breakpoint on every page.

#### Scenario: a migrated screen

- **WHEN** a migrated screen's styles are inspected
- **THEN** every value resolves to the theme, and no kit class remains on it

#### Scenario: an unmigrated screen

- **WHEN** a screen not yet migrated renders
- **THEN** it renders exactly as before adoption

#### Scenario: small screens fold

- **WHEN** any page renders below the medium breakpoint
- **THEN** navigation and the inbox count remain reachable and nothing overflows

