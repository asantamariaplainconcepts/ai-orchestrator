# connector-configuration

## MODIFIED Requirements

### Requirement: an Admin configures a Connector by supplying the token itself

An Admin SHALL be able to configure a Connector by supplying the access token directly, without
having created a secret beforehand. The product SHALL derive the secret's name from the project,
SHALL store the value in the habitat's secret store, and SHALL NOT ask the Admin to choose a name.
Supplying a token and naming an existing secret SHALL both remain available; both together SHALL be
refused naming the conflict.

Whether **neither** may be supplied SHALL depend on whether the project already has a Connector.
Configuring a project that has none SHALL still require one of the two, because there is nothing to
verify against. Reconfiguring a project that has one SHALL accept neither, and SHALL then resolve the
credential by that Connector's own stored secret name — so an Admin SHALL NOT have to re-supply a
credential the product already holds in order to change coordinates or settings.

The reuse path SHALL re-verify the resolved credential against the live vendor exactly as any other
configuration does, because an edit may change what the credential is being asked to read. It SHALL
NOT re-store the value, SHALL NOT return it, and SHALL NOT display it.

Reconfiguring with a **different vendor** and no new credential SHALL be refused naming why: the stored
credential belongs to the previous vendor's secret name and cannot vouch for the new one.

Storing SHALL require a caller holding the Admin role, and so SHALL the reuse path — editing
configuration behind a stored credential SHALL NOT be less protected than pasting one. A habitat whose
store cannot accept a value SHALL refuse the storing path with a reason naming what to do instead, and
the naming path SHALL continue to work there.

The Connector SHALL be persisted only after the stored value has verified against the live
vendor, so a Connector that exists is still one that works (UC-004). Supplying a new token for a
Connector that already has one SHALL replace the stored value, and subsequent Runs SHALL use the
new one without a restart.

#### Scenario: connecting without a pre-existing secret

- **WHEN** an Admin supplies coordinates and a token for a project with no Connector
- **THEN** the Connector is configured, the token is in the habitat's secret store under a name
  the product chose, and no part of the token appears in the response

#### Scenario: rotation

- **WHEN** an Admin supplies a new token for a project that already has a Connector
- **THEN** the stored value is replaced under the same name, and the next Run uses the new value

#### Scenario: the operator brings their own secret

- **WHEN** an Admin names an existing secret instead of supplying a token
- **THEN** the Connector is configured exactly as it was before this capability existed

#### Scenario: neither or both

- **WHEN** a request carries a token and a secret name together, or carries neither for a project that
  has no Connector
- **THEN** it is refused with a message naming what conflicts or what is missing

#### Scenario: a habitat that cannot store

- **WHEN** an Admin supplies a token in a habitat whose secret store cannot accept values
- **THEN** the request is refused with a reason naming what to do instead, and naming an existing
  secret still configures a Connector there

#### Scenario: a caller who is not an Admin

- **WHEN** a caller without the Admin role supplies a token
- **THEN** the request is refused and nothing is stored

#### Scenario: the token does not work

- **WHEN** the supplied token fails verification against the vendor
- **THEN** no Connector is configured, and the failure names the vendor's reason

#### Scenario: editing a setting without re-supplying the credential

- **WHEN** an Admin reconfigures an existing Connector — changing a setting or the coordinates — and
  supplies no credential
- **THEN** the stored credential is resolved by that Connector's secret name, re-verified against the
  vendor, and the configuration is saved without the value being re-stored, returned or shown

#### Scenario: an edit the stored credential cannot serve

- **WHEN** an Admin changes the owner or repository to one the stored credential cannot read
- **THEN** the refusal names the vendor's own reason and nothing is saved

#### Scenario: switching vendor without a new credential

- **WHEN** an Admin reconfigures an existing Connector to a different vendor and supplies no credential
- **THEN** it is refused naming why, because the stored credential belongs to the previous vendor

#### Scenario: reuse is not a way around the role check

- **WHEN** a caller without the Admin role reconfigures an existing Connector supplying no credential
- **THEN** the request is refused and nothing is changed
