## ADDED Requirements

### Requirement: the executor and the sandbox share a machine, and the spec says so

Where agents execute in sandboxes, the sandbox SHALL obtain the Run's workspace from the
executing process's own filesystem, and the executor and the sandbox host SHALL therefore run on
the same machine. A habitat that places them apart is not supported, and nothing in the product
attempts to bridge them.

This is stated because it is the fact that decides where a habitat can put things, and it was
until now only implied — by the executor preparing a local directory and the sandbox mounting
that path. A reader choosing between a VM, a Kubernetes node and a managed job cannot discover
it from the specs, and would design against it.

A change that makes the workspace reach the sandbox some other way — a clone the sandbox
performs itself, a shared volume, a transport — SHALL modify this requirement rather than
leaving it standing beside a contradicting implementation.

#### Scenario: a habitat separates them

- **WHEN** a deployment places the executor on one machine and the sandbox host on another
- **THEN** it is unsupported: the sandbox has no way to obtain the workspace, and no component
  attempts to transfer it

#### Scenario: the constraint is discoverable

- **WHEN** somebody chooses a habitat for agent execution
- **THEN** the locality contract is readable in this capability's spec rather than inferable
  only from the executor's implementation
