# run-orchestration — delta

## ADDED Requirements

### Requirement: a Run records where it executes, chosen at creation

Every Run SHALL carry an execution locus — `Pod` or `Local` — fixed at creation and exposed on
the runs read model. `Run now` MAY name a locus explicitly; when absent, and always for
matching-created Runs, the project default applies: `Local` for a LocalFolder code source, `Pod`
otherwise. A `Local` locus on a project whose code source is not a folder SHALL be refused, as
SHALL `Pod` on a LocalFolder project — an Agent pod cannot see the host's disk. BR-001, BR-002
and BR-013 apply identically to both loci.

#### Scenario: the default follows the code source

- **WHEN** matching creates a Run for a project whose Connector's code source is a local folder
- **THEN** the Run records `locus=Local` and the read model exposes it

#### Scenario: an impossible locus is refused

- **WHEN** Run now names `pod` for a LocalFolder project
- **THEN** creation is refused with a sentence naming the constraint, and no Run exists

### Requirement: a Local dispatch requires a clean working tree (BR-016)

Dispatching a Run with locus `Local` SHALL verify the configured folder's working tree is clean
and refuse before any write when it is not — the refusal names the folder and says why, in the
same pre-write pattern as BR-001. The rule is recorded as BR-016 in
`docs/product/mvp/05-business-rules.md` as part of this change.

#### Scenario: dirty tree refuses the dispatch

- **WHEN** Run now targets a Local project whose folder has uncommitted changes
- **THEN** the answer is a refusal naming the folder, and no Run was created
