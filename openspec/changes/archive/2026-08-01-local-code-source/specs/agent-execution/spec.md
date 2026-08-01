# agent-execution — delta

## ADDED Requirements

### Requirement: the executor selects the workspace per Run by locus

`RunExecutor` SHALL obtain the Run's workspace through the existing `ICodeWorkspace` seam,
selected by the Run's locus: `Pod` keeps today's fresh-clone workspace unchanged; `Local` uses
the folder workspace. The queue message, dispatch worker and Aspire wiring are unchanged — locus
is a workspace decision inside the worker, never a routing decision (design D1).

#### Scenario: a Pod run is byte-for-byte today's behaviour

- **WHEN** a Run with locus `Pod` executes
- **THEN** the workspace is a fresh shallow clone and every existing agent-execution requirement
  holds without modification

#### Scenario: audit fields extend for Local (BR-014)

- **WHEN** a Local Run reaches a terminal state
- **THEN** the Run row carries its locus, working folder and branch name alongside every
  existing audit field, and none of the existing fields changed meaning
