# run-orchestration

## ADDED Requirements

### Requirement: a Run's cost is readable, and unknown is distinguishable from free

The runs API SHALL expose each Run's input tokens, output tokens and cost, nullable so that a
runtime which reported nothing yields null (BR-011, DEC-038). The portal SHALL render a
reported cost as an amount — including `0.00` for a free model — and an absent one as the
design system's empty value with the word unknown. The two SHALL NOT render alike. A project
SHALL show the summed cost of its Runs that reported, together with how many Runs are excluded
as unknown, so the total is never quietly understated.

#### Scenario: a Run that reported

- **WHEN** a Run's runtime reported usage
- **THEN** its cost and token counts are shown, and a zero cost is shown as zero

#### Scenario: a Run that did not report

- **WHEN** a Run's runtime reported no usage
- **THEN** its cost reads as unknown — not as zero

#### Scenario: the project total is honest about what it left out

- **WHEN** a project has Runs both with and without reported usage
- **THEN** the total sums only the reported ones and states how many were excluded
