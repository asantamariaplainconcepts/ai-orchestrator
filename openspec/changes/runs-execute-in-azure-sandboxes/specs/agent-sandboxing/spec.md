## ADDED Requirements

### Requirement: a sandbox may be created where the executing process is not

A sandbox launcher MAY create its sandboxes on a machine other than the one executing Runs, over an
authenticated API rather than a local socket. Where it does, the Run's workspace SHALL be sent to
the sandbox rather than mounted from the executing process's filesystem, and the executor and the
sandbox SHALL NOT be required to share a machine.

Such a launcher SHALL NOT require any host-level grant on the executing machine — no socket, no
privileged mount — because a grant that is root-equivalent on the host is the cost this substrate
exists to remove.

Where a launcher's command surface bounds how long a single execution may take, that bound SHALL be
absorbed by the launcher and SHALL NOT reach the executor: a Run may last as long as BR-005 allows,
and its output SHALL still be observable while it executes (UC-027).

#### Scenario: a Run executes where the executor is not

- **WHEN** a Run is dispatched in a habitat whose launcher creates sandboxes remotely
- **THEN** it executes in a sandbox on another machine, receives its workspace, and reaches a
  terminal state — and no socket or privileged grant exists on the executing machine

#### Scenario: an execution longer than the launcher's own limit still completes

- **WHEN** an agent runs for longer than a single command against that launcher may take
- **THEN** the Run completes anyway, its output appears while it works, and nothing about the
  limit is visible to the executor

### Requirement: a remotely-created sandbox declares what its platform's defaults get wrong

Where a habitat's sandboxes are created on a platform whose defaults do not suit a Run, the habitat
SHALL declare the corrections rather than inherit them, and composition SHALL refuse a habitat that
leaves them undeclared.

Two are known and SHALL be declared: **automatic suspension SHALL be disabled**, because a platform
that measures idleness by requests from outside will suspend a sandbox whose agent is thinking; and
**egress SHALL be denied by default with an explicit allow list**, because a sandbox created without
a policy may have unrestricted outbound access whatever the platform's documentation says.

A denied request SHALL be refused and SHALL be recordable, so a habitat can show what its agents
tried to reach.

#### Scenario: a thinking agent is not suspended

- **WHEN** an agent runs for several minutes producing no output and receiving no calls
- **THEN** its sandbox is still running and the Run continues

#### Scenario: an undeclared habitat refuses to start

- **WHEN** a habitat names a remote sandbox launcher without declaring suspension and egress
- **THEN** composition refuses, naming what is missing — never a deployment whose agents run
  unrestricted because a default was assumed

#### Scenario: the deny side denies

- **WHEN** an agent reaches for a host outside the allow list
- **THEN** the request is refused and the refusal is recordable, while an allowed host succeeds

### Requirement: a project's credentials stay a project's, whatever the platform scopes them to

Where a substrate scopes credentials to a container broader than a Project, the habitat SHALL give
each Project its own such container, so that a Run bills and acts as its own Project's identity
(#244) rather than a shared one.

Credential values SHALL NOT enter the sandbox where the platform can attach them at its boundary
instead, and the Run's transcript SHALL name that source (BR-010).

#### Scenario: two projects, two identities

- **WHEN** Runs of two different Projects execute on this substrate
- **THEN** each authenticates with its own Project's credential, and neither can use the other's

#### Scenario: the value never lands inside

- **WHEN** an agent executes with a credential attached at the boundary
- **THEN** no credential value is readable inside the sandbox, and the transcript names the
  injection as the source
