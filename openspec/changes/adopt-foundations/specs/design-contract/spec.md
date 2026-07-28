# design-contract

## ADDED Requirements

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
