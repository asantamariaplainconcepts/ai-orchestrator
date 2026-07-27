# agent-execution

## ADDED Requirements

### Requirement: the Automation's runtime decides which agent executes

Run execution SHALL select the `IAgentRuntime` implementation — and its credential secret
name, which MAY be absent — from the Automation's `Runtime` value through a selector seam.
A runtime whose credential name is absent SHALL execute with no resolved credential (free
providers); a runtime naming a credential keeps the resolve-by-name path (BR-010). Adding a
runtime SHALL be a composition change, never an executor edit.

#### Scenario: two runtimes, two paths

- **WHEN** two Automations differ only in runtime and their Runs execute
- **THEN** each Run is executed by its runtime's implementation

#### Scenario: a free-model runtime needs no vault entry

- **WHEN** an OpenCode-runtime Run executes with no credential secret configured
- **THEN** the Run proceeds — no vault lookup occurs and no failure is manufactured

### Requirement: opencode usage comes from the observed event stream

The opencode implementation SHALL invoke the pinned CLI headless with JSON event output,
aggregate usage from `step_finish` events (tokens and cost) and take the reply from `text`
events (OPN-004's closure). Unknown event types SHALL be skipped; a stream with no
`step_finish` SHALL yield unknown usage (BR-011); a non-zero exit or empty stream SHALL fail
the Run with the raw output as evidence.

#### Scenario: a free-model run reports its usage

- **WHEN** an opencode Run completes normally
- **THEN** the Run records the summed tokens and cost (zero cost for free models) and the
  reply text as its log

#### Scenario: shape drift degrades to honesty

- **WHEN** the event stream carries no readable step_finish
- **THEN** the Run's usage reads unknown and its outcome is decided by the exit code alone
