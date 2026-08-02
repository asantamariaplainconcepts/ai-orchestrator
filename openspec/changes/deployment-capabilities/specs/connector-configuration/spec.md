# connector-configuration — delta for deployment-capabilities

## ADDED Requirements

### Requirement: a credential path is offered only where it can succeed

The Connector form SHALL offer pasting a token in every deployment, because the backlog is remote
wherever the code lives: reading Stories, verifying the Connector and writing labels need a vendor
credential in every posture. Only a Local Run's workspace skips one, and that is git rather than
the backlog.

Naming an existing secret SHALL be offered **only** where the deployment composes a secret store
to name one in. Where it does not, the control SHALL be absent rather than disabled — an option
that cannot succeed is not a choice.

Which paths to offer SHALL come from the deployment's own capabilities read, never from a client
re-deriving them, and never from provoking a refusal.

#### Scenario: the token is offered everywhere

- **WHEN** the Connector form renders in any deployment
- **THEN** pasting a token is available

#### Scenario: naming a secret needs a store to name one in

- **WHEN** the deployment composes no secret store
- **THEN** the form offers no way to name an existing secret

#### Scenario: both paths where a store exists

- **WHEN** the deployment composes a secret store
- **THEN** both pasting and naming are available, as before
