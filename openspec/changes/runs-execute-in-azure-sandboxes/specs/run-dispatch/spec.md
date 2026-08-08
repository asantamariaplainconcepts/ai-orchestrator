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
