# backend-architecture

## ADDED Requirements

### Requirement: the caller's identity is composed by the host, per habitat

The system SHALL expose the current caller as a principal carrying an id, a display name and a
role, resolved through one seam whose implementation the host composes. There SHALL be no state
in which a caller has no principal, and no consumer of the seam SHALL branch on whether identity
is configured. On a machine-local deployment the principal SHALL be the machine's owner, holding
the Admin role, requiring no configuration. The host SHALL refuse to start when the local-owner
identity is configured together with a production environment or a publicly reachable address,
naming which it found. A hosted deployment with neither the local owner nor an identity provider
SHALL announce that state at startup.

#### Scenario: a clean local start has an owner

- **WHEN** the system starts from a clean checkout with no identity configuration
- **THEN** every request runs as the local owner, holding the Admin role

#### Scenario: the local owner cannot reach production

- **WHEN** the local-owner identity is configured alongside a production environment or a
  publicly reachable address
- **THEN** the host refuses to start and states which condition it found

#### Scenario: an unauthenticated hosted deployment says so

- **WHEN** a hosted deployment starts with neither the local owner nor an identity provider
- **THEN** it announces that it authenticates nobody, naming the open decision

#### Scenario: attribution names somebody

- **WHEN** work is recorded on a machine-local deployment
- **THEN** it is attributed to the local owner rather than to nothing
