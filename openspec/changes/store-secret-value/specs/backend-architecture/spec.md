# backend-architecture

## MODIFIED Requirements

### Requirement: secret resolution is composed by the host, per environment

The host's composition root SHALL select the `ISecretResolver` implementation by configuration:
when `Secrets:KeyVaultUri` is present it SHALL register the Key Vault resolver over Aspire's
Key Vault client integration (authenticating via `DefaultAzureCredential` — managed identity
when deployed, the developer's az login locally); otherwise the configuration-backed resolver
serves development and tests. Modules SHALL remain unaware of the backing store: no module
references a cloud SDK, and no call site changes when the implementation swaps.

The host SHALL compose the storing abstraction the same way and in the same place: Key Vault
where a vault is configured, an encrypted local store where the habitat has none, and — where
neither can accept a value — an implementation that refuses with a reason naming what to do
instead. A habitat SHALL never be left with a store that appears to write and does not. The
encrypted local store SHALL use the framework's own data-protection implementation, with its key
material persisted outside the application database.

#### Scenario: the same build runs in dev and in Azure

- **WHEN** the identical Server build starts with and without `Secrets:KeyVaultUri`
- **THEN** secret names resolve from Key Vault in the first case and from configuration in the
  second, with no code difference and no module involvement

#### Scenario: a secret is rotated in the vault

- **WHEN** a secret's value changes in Key Vault
- **THEN** the next resolution returns the new value without an application restart, because
  resolution happens per read (the seam's original contract)

#### Scenario: a habitat with no vault stores locally

- **WHEN** the system starts with no `Secrets:KeyVaultUri` in a habitat configured to store
  locally
- **THEN** values supplied through the product are written encrypted, with the key held outside
  the application database

#### Scenario: a habitat that cannot store says so

- **WHEN** the system starts where no store can accept a value
- **THEN** attempts to store are refused with a reason naming what to do instead, rather than
  appearing to succeed
