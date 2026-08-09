## MODIFIED Requirements

### Requirement: the executor and the sandbox share a machine, and the spec says so

Where agents execute in sandboxes **that the executing process creates on its own machine**, the
sandbox SHALL obtain the Run's workspace from that process's filesystem, and the executor and the
sandbox host SHALL therefore run on the same machine. A habitat that places them apart on such a
substrate is not supported, and nothing in the product attempts to bridge them.

This is stated because it is the fact that decides where a habitat can put things, and it was
until now only implied — by the executor preparing a local directory and the sandbox mounting
that path. A reader choosing between a VM, a Kubernetes node and a managed job cannot discover
it from the specs, and would design against it.

**The qualifier is new, and it is a correction rather than a widening.** The requirement was
written from one substrate — Docker Sandboxes, where `/run/sandbox/source` was observed to be a
read-only virtiofs mount of a host directory, same inode — and then stated about sandboxes in
general. That is a claim wider than its measurement (ADR-0018). Whether a substrate that creates
sandboxes **remotely**, over an API rather than a local socket, imposes the same locality is
**not verified**: it is the open question `spike-azure-container-apps-sandboxes` exists to answer,
and until that spike reports, no design may assume either answer.

A change that makes the workspace reach the sandbox some other way — a clone the sandbox
performs itself, a shared volume, a transport — SHALL modify this requirement rather than
leaving it standing beside a contradicting implementation.

#### Scenario: a habitat separates them on a locally-created substrate

- **WHEN** a deployment places the executor on one machine and a locally-created sandbox host on
  another
- **THEN** it is unsupported: the sandbox has no way to obtain the workspace, and no component
  attempts to transfer it

#### Scenario: the constraint is discoverable

- **WHEN** somebody chooses a habitat for agent execution
- **THEN** the locality contract is readable in this capability's spec rather than inferable
  only from the executor's implementation

#### Scenario: a remotely-created substrate is an open question, not an assumption

- **WHEN** somebody designs against a substrate whose sandboxes are created over an API rather
  than on the executing machine
- **THEN** the spec tells them the locality answer for that substrate is unverified, so the
  design waits for evidence rather than inheriting a verdict measured elsewhere
