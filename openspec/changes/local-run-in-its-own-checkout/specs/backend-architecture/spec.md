## ADDED Requirements

### Requirement: feature state is composed from configuration alone

The Server SHALL compose `Microsoft.FeatureManagement` so that a feature's state is read from
`IConfiguration` and from nothing else. No Azure App Configuration client, endpoint or credential
SHALL be required to start, in any habitat — DEC-049 holds that a stranger with Docker can still run
this, and a managed configuration service would put a cloud dependency in the start path of a
self-hosted install.

A habitat that declares no features SHALL start exactly as it does today: composing the feature
manager is not itself a behaviour change, and nothing in this change consumes it.

This requirement is recorded as a seam with no consumer, knowingly and against
[RULE-007](../../../../docs/product/v1/08-backlog-shaping-rules.md)'s speculative-abstraction
anti-pattern. The owner decided (#331) that the plumbing lands here so the follow-on capability —
choosing a Run's isolation substrate per Automation — arrives against composition that already
exists. The reason is written down here rather than left to be re-derived, because the next reader
will otherwise correctly identify it as an abstraction nobody asked for.

#### Scenario: the feature manager resolves in every habitat

- **WHEN** the Server starts in the dev loop, in a compose self-host install, or in a deployment
- **THEN** `IVariantFeatureManager` resolves from the container, and no Azure App Configuration
  connection is attempted

#### Scenario: no declared features changes nothing

- **WHEN** the Server starts with no `FeatureManagement` section in configuration
- **THEN** startup succeeds and no behaviour observable to any existing scenario differs

#### Scenario: a declared feature is readable from configuration

- **WHEN** configuration declares a feature and the feature manager is asked for its state
- **THEN** the answer reflects the configured value, resolved without any external service
