# frontend-architecture

## ADDED Requirements

### Requirement: the project page separates operating from configuring

The project page SHALL present its content as tabs — operate, runs, automations, settings —
where the operate tab carries the daily surface (attention, pulse, backlog with per-row
actions) and configuration forms are visible only on their own tabs or behind an explicit
action. The landing tab SHALL be derived from the project's state: configured projects open on
operate, unconfigured ones on settings with the connector form open. The active tab SHALL be
addressable in the URL and survive a refresh. Below the medium breakpoint the tabs SHALL
remain reachable and every action available on desktop SHALL remain available.

#### Scenario: a configured project opens on the work

- **WHEN** a configured project's page opens without a tab in the URL
- **THEN** the first screenful is the operate tab and no configuration form is visible

#### Scenario: an unconfigured project opens on setup

- **WHEN** a project without a connector opens
- **THEN** the settings tab is active with the connector form open

#### Scenario: a tab survives refresh

- **WHEN** a tab is addressed in the URL and the page reloads
- **THEN** the same tab is active

#### Scenario: small screens keep every action

- **WHEN** the page renders below the medium breakpoint
- **THEN** the tabs remain reachable and every desktop action remains reachable
