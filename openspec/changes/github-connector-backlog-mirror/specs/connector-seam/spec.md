# connector-seam

## ADDED Requirements

### Requirement: vendors are reached through one abstraction

All vendor access SHALL go through a single connector abstraction exposing verification and Story
retrieval. Vendor SDK types SHALL NOT appear outside the implementation that owns them, so a
second vendor can be added without touching any caller.

#### Scenario: adding a vendor

- **WHEN** a second vendor implementation is added
- **THEN** the polling loop, the mirror, and the API are unchanged

#### Scenario: no SDK leakage

- **WHEN** code outside the GitHub implementation is inspected
- **THEN** no GitHub SDK type appears in a signature, a domain type, or an API contract

### Requirement: the seam is normalised, not lowest-common-denominator

The abstraction SHALL express Stories in the product's own vocabulary — id, title, state, labels
— rather than mirroring any one vendor's schema. Vendor-specific mapping SHALL live in that
vendor's implementation.

#### Scenario: vendors disagree about vocabulary

- **WHEN** a vendor calls its concepts something other than Story, state or label
- **THEN** the mapping happens inside that vendor's implementation, and the rest of the system
  sees the product's vocabulary (DEC-005)

### Requirement: the Backlog module owns its data and references projects by identity

The Backlog module SHALL own the Connector and Story data and SHALL reference a Project by its
identifier only. It SHALL NOT reference another module's implementation assembly.

#### Scenario: module boundaries hold

- **WHEN** the solution is built and the architecture tests run
- **THEN** no cross-module implementation reference exists, and no `.Contracts` assembly is
  required for this change

#### Scenario: schema ownership

- **WHEN** the Backlog module's migrations run
- **THEN** its tables land in its own schema and no other module's schema is altered

### Requirement: vendor calls are conditional where the vendor supports it

Where a vendor supports conditional requests, the connector SHALL use them so that unchanged data
does not consume rate limit.

#### Scenario: nothing changed since the last poll

- **WHEN** a poll runs against a repository whose Stories have not changed
- **THEN** the request is conditional, and an unchanged response leaves the mirror untouched
