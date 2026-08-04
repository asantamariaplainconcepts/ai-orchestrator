# local-code-source — delta for selfhost-declares-its-limits

## ADDED Requirements

### Requirement: a habitat that cannot reach the folder refuses by name

Where the habitat declares the Local locus unavailable, naming a `LocalFolder` code source SHALL
be refused at save, and a Run SHALL NOT resolve to the Local locus — both refusals carrying the
declared reason verbatim, never a path error from inside a container.

The refusal SHALL exist at the API, not only in the portal: a Connector stored before the
declaration, or a request made around the portal, meets the same sentence.

#### Scenario: saving a LocalFolder Connector in a declaring habitat

- **WHEN** an Admin submits a Connector with the `LocalFolder` code source where the habitat
  declares the locus unavailable
- **THEN** the save is refused with the declared reason, and nothing is stored

#### Scenario: a pre-existing LocalFolder Connector cannot produce a Local Run

- **WHEN** a Run would resolve to the Local locus in a declaring habitat
- **THEN** the Run is refused with the declared reason — it does not fail later on a container
  path

#### Scenario: the portal never offers what the habitat withheld

- **WHEN** the code-source section renders in a declaring habitat
- **THEN** the local-folder option is not offered and the reason is shown in its place
