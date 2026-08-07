## MODIFIED Requirements

### Requirement: a Run's file changes are reviewable beside its Plan

The Run detail view SHALL show the files the Run's change touched — path, status, added and
removed counts, and the unified patch rendered with added and removed lines visually
distinguished using design-system tokens. The read SHALL be live through the Connector at the
Run's linked change (BR-008). A Run with no pull request, a change touching no files, and a
failed read SHALL be three distinct messages. A file whose patch is omitted SHALL state the
reason and link to the vendor.

The changes SHALL occupy the detail's **main column**, never a side rail: a diff needs the width
the body has, with line numbers, and each file's header SHALL stay visible while its hunks scroll
and SHALL collapse on demand. The rail SHALL carry only the change's summary — its number, file
count and ± — anchoring to the block, so the two never show two diffs.

At a narrow viewport the diff SHALL wrap rather than scroll sideways, with the added/removed
marker in a fixed gutter so a wrapped line keeps its meaning; long paths SHALL truncate from the
left so the file name stays visible; files beyond the first SHALL arrive collapsed; and a hunk
longer than a screen SHALL paginate behind an explicit control naming how many lines remain.

#### Scenario: the reviewer sees what the Agent did

- **WHEN** a Member opens a Run whose pull request changed files
- **THEN** each file is listed with its status and counts, and its diff renders with added and
  removed lines distinguishable

#### Scenario: the diff has the width of the body

- **WHEN** the detail renders at a wide viewport
- **THEN** the changes occupy the main column with line numbers, each file's header stays visible
  while its hunks scroll, each file can collapse, and the rail shows only the summary that
  anchors to the block

#### Scenario: a phone reads the diff without sideways scroll

- **WHEN** the detail renders at a narrow viewport
- **THEN** diff lines wrap with the marker in a fixed gutter, paths truncate from the left, files
  beyond the first arrive collapsed, and long hunks offer "show more" instead of endless scroll

#### Scenario: no pull request yet

- **WHEN** the Run has produced no pull request
- **THEN** the section says so — distinctly from a change that touched no files

#### Scenario: an unshowable file is explained, not hidden

- **WHEN** a changed file is binary or its patch is too large
- **THEN** the file appears with a stated reason and a link to the vendor, and the other files
  still render

## ADDED Requirements

### Requirement: a failure arrives with its remedy and its decisions

A failed Run's detail SHALL open with a banner above the content carrying the full failure
reason, and the failure's two decisions — re-run and dismiss (#145) — SHALL live inside that
banner and nowhere else on the page: the decisions belong beside the reason they answer, not in a
header far from the why.

A cause with a known remedy surface SHALL link to it from the banner: an unresolved secret or
credential links to the project's Connector settings, and an unreadable prompt file links to the
Automations tab. A cause with no mapped surface SHALL show the banner without a link — a guessed
remedy is worse than none. The mapping is a stated, short list, never an inference from message
text beyond the causes it names.

A Run section with nothing to show — a Plan that never existed, output that never arrived — SHALL
collapse to a single line stating its empty state rather than holding blank space.

#### Scenario: the failure is answerable where it is stated

- **WHEN** a Member opens a failed Run
- **THEN** the banner shows the full reason with re-run and dismiss inside it, and those controls
  appear nowhere else on the page

#### Scenario: a mapped cause links its remedy

- **WHEN** the failure is an unresolved secret or credential
- **THEN** the banner links to the project's Connector settings

#### Scenario: an unmapped cause gets no invented link

- **WHEN** the failure matches no mapped cause
- **THEN** the banner shows the reason and the decisions, and no remedy link

#### Scenario: empty sections take one line

- **WHEN** a Run has no Plan or produced no output
- **THEN** each such section renders as a single line stating so
