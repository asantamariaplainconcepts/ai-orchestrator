# run-dispatch — delta for docker-run-pods

## ADDED Requirements

### Requirement: a habitat can execute each Run in its own container

Where the habitat names a pod image, a dispatched Run SHALL execute in its own container from
that image, started for exactly that Run and exiting with it. The durable half SHALL remain the
outbox: the substrate changes where execution happens, never what survives a crash (BR-004).

Selection SHALL follow configuration presence (ADR-0010): the image named selects the pod
launcher; nothing named keeps in-process execution. The docker socket SHALL be the operator's
explicit grant — the generated compose SHALL NOT mount it by default, and SHALL carry the
warning that it is root-equivalent beside the place it is granted.

#### Scenario: a Run executes in its own pod

- **WHEN** a Run is dispatched in a habitat naming a pod image with the socket granted
- **THEN** a container from that image executes exactly that Run and exits, and the Run reaches
  a terminal state with its log

#### Scenario: the missing socket refuses by name

- **WHEN** a Run is dispatched where the image is named but docker cannot be reached
- **THEN** the Run fails naming what is missing — never a hang, and never a silent fallback to
  in-process execution

#### Scenario: nothing configured, nothing changes

- **WHEN** no pod image is named
- **THEN** dispatch and execution behave exactly as before this change, in every habitat

### Requirement: the per-Run entry mode tells the truth with its exit code

The worker image SHALL accept one Run id and execute exactly that Run, exiting 0 when execution
completed — the Run's state is the truth, and a failed Run is a completed execution. A non-zero
exit SHALL mean execution could not happen at all, and nothing SHALL retry it (BR-004).

#### Scenario: a failed Run is a completed execution

- **WHEN** the pod's Run ends in failure
- **THEN** the container exits 0 and the Run's state carries the failure

### Requirement: concurrent pods are bounded, and delayed is not dropped

Concurrent pod executions SHALL be bounded by a configurable cap defaulting to 2. A Run
dispatched past the cap SHALL wait and then execute — delayed, never dropped.

#### Scenario: the third Run waits for a slot

- **WHEN** three Runs dispatch with the cap at 2
- **THEN** at most two containers run concurrently and the third executes when a slot frees

### Requirement: the host's sessions enter the pod by deliberate default

Where the pod substrate is active, the host's agent-CLI configuration SHALL be provided to the
pod by default and the operator SHALL be able to turn it off; the transcript SHALL name the
credential source either way. The mechanism SHALL be fixed by observing a real CLI in a pod —
recorded, not assumed — and the consequence SHALL be stated where the option lives: pod Runs act
and bill as those sessions.

#### Scenario: the default carries the session and says so

- **WHEN** a pod Run executes with the default in place
- **THEN** the CLI in the pod can use the host's session, and the transcript names it as the
  credential source
