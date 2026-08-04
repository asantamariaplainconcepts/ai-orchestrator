## Why

Issue #247. The compose self-host habitat offers the `LocalFolder` code source — `Identity__Mode`
is `LocalOwner`, so `HasCodeSource` answers true — but the Server there runs in a **container**:
the folder an Admin names lives on the host, invisible to the process that would work in it. The
offer fails downstream, as a path error inside a container, instead of being withheld with its
reason. The hazard is sharper than a bad error message: a mount that *simulated* reachability
would put two processes over one working copy, which is why the grill rejected simulating and
chose declaring (#247, 2026-08-04).

## What Changes

- The deployment capabilities read (#222) gains the Local-locus fact: whether a folder on the
  operator's machine is reachable from the executing process, with the **reason** when it is not
  — declared by the habitat's own composition, never inferred (ADR-0010).
- The generated self-host compose declares the reason; the dev loop (Server as host process)
  declares nothing and keeps today's behaviour.
- Naming a `LocalFolder` code source, and resolving a Run to the Local locus, are refused by name
  in a habitat that declares the locus unavailable — never a container path error.
- DEC-049 self-host docs state plainly what compose self-host can and cannot do.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `deployment-capabilities`: the answer gains the Local-locus fact, carrying its reason when
  absent — the StoreRemedy pattern, one more fact.
- `local-code-source`: the surface exists only where the folder is reachable, and a habitat that
  declares it unreachable refuses by name at save and at Run resolution.

## Impact

- `GetDeploymentCapabilities` (Projects module) — response widens.
- AppHost publish composition + regenerated `selfhost/docker-compose.yaml` (the declared reason).
- `ConfigureConnector` (Backlog) LocalFolder validation; `RunCreator` (Runs) locus resolution.
- Frontend: the code-source section reads the new fact; i18n entries.
- No schema change; no behaviour change in the dev loop or in ACA.
