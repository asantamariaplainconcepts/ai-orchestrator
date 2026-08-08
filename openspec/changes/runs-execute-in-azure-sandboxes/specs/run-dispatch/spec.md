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

