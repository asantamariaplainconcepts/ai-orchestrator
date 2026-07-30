# backend-architecture

## MODIFIED Requirements

### Requirement: the caller's identity is composed by the host, per habitat

The system SHALL expose the current caller as a principal carrying an id, a display name and a
role, resolved through one seam whose implementation the host composes. There SHALL be no state
in which a caller has no principal, and no consumer of the seam SHALL branch on whether identity
is configured. On a machine-local deployment the principal SHALL be the machine's owner, holding
the Admin role, requiring no configuration. The host SHALL refuse to start when the local-owner
identity is configured together with provisioned infrastructure or a publicly reachable address,
naming which it found. A hosted deployment with neither the local owner nor an identity provider
SHALL announce that state at startup.

When identity-provider configuration is present, a hosted deployment SHALL compose the provider
instead of the announced stopgap, and the principal SHALL be the signed-in user: the provider's
stable object id as the id, the name claim as the display name. Composition SHALL key on the
presence of that configuration, never on an environment name. Until per-project roles land
(UC-002), every signed-in user SHALL hold the Admin role — a stated interim rule, not an
omission.

The machine-local mode and the provider mode SHALL coexist as alternatives: adding the provider
SHALL NOT change what a clean local checkout does, and a deployment with no provider
configuration SHALL keep the announced stopgap, because the self-host habitat has no tenant to
sign into.

#### Scenario: a clean local start has an owner

- **WHEN** the system starts from a clean checkout with no identity configuration
- **THEN** every request runs as the local owner, holding the Admin role

#### Scenario: the local owner cannot reach provisioned infrastructure

- **WHEN** the local-owner identity is configured alongside provisioned infrastructure or a
  publicly reachable address
- **THEN** the host refuses to start and states which condition it found

#### Scenario: an unauthenticated hosted deployment says so

- **WHEN** a hosted deployment starts with neither the local owner nor an identity provider
- **THEN** it announces that it authenticates nobody, naming the open decision

#### Scenario: attribution names somebody

- **WHEN** work is recorded on a machine-local deployment
- **THEN** it is attributed to the local owner rather than to nothing

#### Scenario: a provider-configured deployment authenticates

- **WHEN** identity-provider configuration is present and a signed-in user calls the API
- **THEN** the principal is that user — their stable id and display name — and the startup
  warning about authenticating nobody does not fire

#### Scenario: the provider does not change the local mode

- **WHEN** the local-owner mode starts on a machine with no provider configuration
- **THEN** it behaves exactly as before the provider existed
