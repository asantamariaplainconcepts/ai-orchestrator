# connector-configuration — delta for deployment-capabilities

## ADDED Requirements

### Requirement: a credential path is offered only where it can succeed

The Connector form SHALL offer a vendor credential in every deployment, because the backlog is
remote wherever the code lives: reading Stories, verifying the Connector and writing labels need
one in every posture. Only a Local Run's workspace skips a vendor credential, and that is git
rather than the backlog.

**Which of the two ways to supply it are offered SHALL follow what the deployment can do, not what
posture it is in.** Naming an existing secret SHALL always be available: a resolver is composed in
every habitat. Pasting a token SHALL be offered only where the deployment composes a secret store
that accepts writes — without one, every paste ends in the store's own refusal, and offering a
control whose only outcome is that refusal is the failure this rule removes.

Where pasting is unavailable, naming SHALL be the credential field rather than a secondary
control, and the form SHALL state the remedy the unavailable store already names, so an operator
learns how to gain the option rather than only that they lack it.

The condition SHALL come from the deployment's capabilities read — never from a client re-deriving
it from a posture, and never from provoking a refusal.

#### Scenario: a credential is always askable

- **WHEN** the Connector form renders in any deployment
- **THEN** at least one way to supply a vendor credential is offered

#### Scenario: pasting needs somewhere to put it

- **WHEN** the deployment composes no secret store that accepts writes
- **THEN** pasting is not offered, naming is the credential field, and the store's own remedy is
  stated

#### Scenario: both ways where a store exists

- **WHEN** the deployment composes a store that accepts writes
- **THEN** pasting leads and naming is available beside it, as before

#### Scenario: the posture does not decide this

- **WHEN** a self-host deployment composes a writable store
- **THEN** pasting is offered, exactly as it is in a cloud deployment
