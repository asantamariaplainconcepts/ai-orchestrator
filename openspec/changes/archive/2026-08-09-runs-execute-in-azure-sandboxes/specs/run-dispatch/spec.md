## REMOVED Requirements

### Requirement: a habitat can execute each Run in its own container

**Reason:** the substrate it describes is retired. It executed each Run in a container from a pod
image launched over the docker socket — a grant this same requirement called root-equivalent on the
host, into a container sharing that host's kernel. Both costs are removed by the remotely-created
sandbox substrate that replaces it, which needs no host grant at all.

**Migration:** a habitat naming a pod image is refused at composition, naming the substrate that
replaced it. A machine somebody owns keeps in-process execution, which was always the answer there;
a deployment uses the sandbox launcher. No deployment carries both, which is the state composition
already refuses.

### Requirement: the per-Run entry mode tells the truth with its exit code

**Reason:** it described the retired pod image's entry point — a container that accepts one Run id
and exits with the execution. No container carries a Run any more; the worker executes through the
launcher seam, and a sandbox's lifecycle is the host's to manage, not an entry script's.

**Migration:** none needed. The property it protected — a failed Run is a completed execution, and
nothing retries (BR-004) — lives where it always really lived, in the executor's terminal-state
handling, which is unchanged.

### Requirement: concurrent pods are bounded, and delayed is not dropped

**Reason:** the pods it bounds no longer exist. Concurrency is still governed — the per-project
cap on Run creation is untouched — but a per-machine container-slot cap is a property of a
substrate that was retired.

**Migration:** a deployment tuning `Dispatch:MaxConcurrentPods` removes the key; it is refused
along with the rest of the retired substrate's configuration.

### Requirement: the pod host is observable where it runs

**Reason:** with no pod host, the record it mandated has nothing to record. `GET /api/pods`'s
pods half would answer "not hosted here" forever in every habitat — a seam kept alive to say
nothing. The runtimes half of that panel (#279) is untouched: which agent CLIs a machine can run
remains observable, and the sandbox hosts answer their own readiness through it.

**Migration:** the pods portion of the panel and its seam are removed with the substrate. A Run
waiting at the per-project cap is still visible as Queued on the Runs list, which is where its
truth always lived (the sighting record was a view of it, never the source).

### Requirement: a dispatched Run reaches exactly one job execution

**Reason:** it specified queue-message semantics — claim-by-delete on a Storage Queue — for a
transport that retires with DEC-013's supersession. The property it protected survives it: the
outbox substrate's own requirement already fixes exactly-once claiming and BR-013's deliberate
re-run, and it is the only substrate left.

**Migration:** none. The outbox path is what the dev loop, selfhost and the functional tests have
always run.

### Requirement: queue length starts jobs

**Reason:** there is no queue to measure and no job to start. The scale-to-zero it bought lives in
the sandbox substrate now — a sandbox bills nothing idle — and execution itself became a poll loop
too light to scale.

**Migration:** the KEDA job, the queue and its storage account leave the Terraform. Nothing else
observes them.

### Requirement: Agent jobs run under their own identity

**Reason:** the job it governed retires. Its guarantee — secrets resolved by name, no credential in
configuration (BR-010) — is not lost: it holds for every process this product runs and is specified
where those processes are. What retires is only the *separate principal*, whose value was standing
between the portal and a root-equivalent socket that no longer exists.

**Migration:** the dispatch identity leaves the Terraform. The Server's identity carries the
sandbox-group data role.

## MODIFIED Requirements

### Requirement: the dispatch substrate follows the habitat, and ambiguity refuses

Dispatch SHALL have one contract and **one substrate**: the same durable Postgres outbox the
product's integration events already use. A Run accepted for dispatch SHALL survive the process
dying and SHALL be redelivered after restart, because the durability is the outbox and never a
transport.

The consumer SHALL be composed only by a host that should execute Runs, never acquired by
registering the producer.

A habitat still naming the retired queue connection string SHALL be refused at startup, naming the
substrate that replaced it — a key that quietly stopped meaning anything is how a deployment ends
up running something nobody chose.

#### Scenario: a dispatch survives the process dying

- **WHEN** a Run is accepted for dispatch and the process terminates before execution
- **THEN** it is redelivered after restart and reaches a terminal state

#### Scenario: the retired queue refuses by name

- **WHEN** a habitat starts with the queue connection string still configured
- **THEN** composition refuses, naming the outbox as what replaced it — never a silently ignored
  setting

