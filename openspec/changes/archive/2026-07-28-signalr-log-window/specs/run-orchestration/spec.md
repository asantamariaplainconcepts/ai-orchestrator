# run-orchestration

## ADDED Requirements

### Requirement: a Run's output reaches watchers as it is recorded

While a Run executes, newly recorded output SHALL reach every open viewer of that Run in under
one second, without the viewer polling. Delivery SHALL be best-effort: when it is unavailable the
page SHALL fall back to the existing periodic read, and no output SHALL be lost either way,
because the durable record is unchanged. The agent's execution SHALL NOT depend on delivery in
any way — a Run behaves identically whether or not anybody is watching, and whether or not the
delivery path works.

#### Scenario: a line appears while the Run executes

- **WHEN** the runtime emits a line and a viewer has the Run open
- **THEN** the line is rendered in under one second, without the viewer having requested it

#### Scenario: two viewers see one Run

- **WHEN** two viewers have the same Run open
- **THEN** both receive every line, and the work the portal does per line does not grow with the
  number of viewers

#### Scenario: delivery is unavailable

- **WHEN** the live path cannot be established or is lost
- **THEN** the page falls back to the periodic read and the full output remains available

#### Scenario: the Run does not care

- **WHEN** the live path is broken or nobody is watching
- **THEN** the Run executes and records its output exactly as it otherwise would
