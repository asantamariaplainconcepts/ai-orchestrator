## Why

Issue #246. The compose self-host cannot execute a Run: since #225 the Server is the executing
process in queue-less habitats, but the agent CLIs live only in the DispatchWorker image, and
#247 just made the neighbouring gap honest rather than closed. Fattening the Server image was
rejected at grill as throwaway work. The direction chosen instead — the exploration that read
Orbion and loop-task is recorded on the issue — is a **docker pod per Run**: the ACA Job pattern
shrunk to one machine, on the seam #225 already proved can take another substrate.

## What Changes

- A third `IRunDispatcher` substrate: `DockerRunDispatcher` spawns the DispatchWorker image per
  dispatched Run through a new per-Run entry mode (execute exactly this Run id, then exit — no
  queue drained).
- The worker gains that entry mode (`--run <id>`), useful in ACA for debugging too.
- The compose habitat wires it when the operator grants the docker socket — explicitly, never by
  default, and the compose says in plain words that the socket is root-equivalent.
- The host's CLI sessions are mounted into the pod **by default** (owner decision at grill,
  consequence recorded: Runs act and bill as those sessions); mechanics observed first —
  read-only mount, falling back to copy-in if the CLI writes on refresh.
- A global cap on concurrent pods, configurable, default 2.
- The dev loop may opt in by configuration; docker presence is checked and refused with a named
  reason, never assumed (ADR-0010).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `run-dispatch`: a third substrate — per-Run docker pods — with its selection, refusals,
  parallelism bound, and crash story.

## Impact

- `DispatchComposition` (selection), new `DockerRunDispatcher` (ServiceDefaults), DispatchWorker
  `Program` (per-Run entry mode), AppHost publish composition + regenerated compose, selfhost
  README, functional tests at the dispatcher seam.
- No module changes: the executor, BR-004 and the Run lifecycle are untouched.
