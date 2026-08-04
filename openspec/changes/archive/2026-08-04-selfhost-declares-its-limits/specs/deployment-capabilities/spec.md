# deployment-capabilities — delta for selfhost-declares-its-limits

## ADDED Requirements

### Requirement: the Local locus is declared, and its absence carries the reason

The capabilities answer SHALL state whether a folder on the operator's machine is reachable from
the process that would work in it, and where it is not, SHALL carry the reason as a sentence —
the same pattern the store remedy follows.

The fact SHALL follow the habitat's own **declaration** (its composition sets the reason), never
an inference from the runtime environment (ADR-0010): a container the operator deliberately
mounted is reachable, and only the composition knows.

#### Scenario: compose self-host withholds the Local locus with its reason

- **WHEN** the capabilities are read on a deployment whose composition declares the Local locus
  unavailable
- **THEN** the answer says the local folder cannot be used here and carries the declared reason

#### Scenario: the dev loop keeps the locus

- **WHEN** the capabilities are read on a self-host deployment whose composition declares nothing
- **THEN** the local folder is offered exactly as before this change
