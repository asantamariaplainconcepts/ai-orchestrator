# design-contract

## ADDED Requirements

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
