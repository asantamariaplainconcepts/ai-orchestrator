# backend-architecture

## ADDED Requirements

### Requirement: secret resolution is composed by the host, per environment

The host's composition root SHALL select the `ISecretResolver` implementation by configuration:
when `Secrets:KeyVaultUri` is present it SHALL register the Key Vault resolver over Aspire's
Key Vault client integration (authenticating via `DefaultAzureCredential` — managed identity
when deployed, the developer's az login locally); otherwise the configuration-backed resolver
serves development and tests. Modules SHALL remain unaware of the backing store: no module
references a cloud SDK, and no call site changes when the implementation swaps.

#### Scenario: the same build runs in dev and in Azure

- **WHEN** the identical Server build starts with and without `Secrets:KeyVaultUri`
- **THEN** secret names resolve from Key Vault in the first case and from configuration in the
  second, with no code difference and no module involvement

#### Scenario: a secret is rotated in the vault

- **WHEN** a secret's value changes in Key Vault
- **THEN** the next resolution returns the new value without an application restart, because
  resolution happens per read (the seam's original contract)
