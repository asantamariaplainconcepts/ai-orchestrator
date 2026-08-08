# spike-azure-container-apps-sandboxes — proposal

Issue: none (spike) · Investigation · Actors: the selfhost operator and the deployed habitat ·
Touches the execution seam behind UC-012 · DEC-012 (pluggable runtimes), DEC-013 (dispatch),
BR-005 (phase timeout), BR-010 (secret names only), ADR-0017 (a rehearsal needs its credential)

## Why

Two spikes ago this programme adopted Docker Sandboxes (`sbx`) for the dev loop and left one
question standing: **what runs a Run in a deployed habitat?** The `--clone` spike then answered the
half nobody wanted — sbx cannot break co-location. Its `/run/sandbox/source` is a read-only
virtiofs mount of a directory the executor prepared on the same machine, same inode. So "connect
the orchestrator to a VM in Azure" has meant "move the worker there as a queue consumer", and that
is a bigger change than it sounds.

[Azure Container Apps Sandboxes](https://techcommunity.microsoft.com/blog/appsonazureblog/introducing-azure-container-apps-sandboxes-secure-infrastructure-for-agentic-wor/4524131)
entered public preview on 2026-06-02 with a resource model that, on paper, does not have that
constraint: a `Microsoft.App/SandboxGroups` resource, sandboxes booted from **your own OCI image**
into a hardware-isolated microVM, each with its own ports, volumes, network egress policy and
managed identity — created over ARM rather than over a socket on the machine that asks.

If a sandbox can be created remotely and can obtain the repository itself, the executor stops
needing to be where the sandbox is, and the deployment question changes shape entirely. If it
cannot, that is worth knowing before any deployment design is drawn on the assumption that it can.

Every claim above is the vendor's. ADR-0005: a claim that depends on verification is a hypothesis
until exercised, and ADR-0018: a measurement licenses only what it measured — so this spike states
what it ran, on which preview, on which date.

**Not a candidate to replace sbx in the dev loop.** sbx runs on the developer's own machine, which
is what makes the session carriage of #288 possible at all. This is about the habitat that is not
somebody's laptop.

## What Changes

Nothing ships. The spike produces evidence and a recommendation, and either opens the deployment
design or closes it with a recorded reason.

## Capabilities

### New Capabilities

None — an investigation.

### Modified Capabilities

None. Any requirement this suggests is written by whatever change follows it.

## Impact

A throwaway resource group, an image pushed to a registry, and a written record. No repository
code changes; anything built to drive the API lives in a scratch harness, exactly as the sbx spike's
console harness did.

**Precondition, named up front (ADR-0017).** This needs an Azure subscription where
`Microsoft.App/SandboxGroups` can be created, and the preview may be gated by region or by
enrolment. The known state of this programme's Azure access is that the deploy pipeline has been
failing at `Initialise` for several days because the Terraform state storage account is disabled —
so **subscription access is assumed broken until somebody checks**, and confirming it is task 0.1
rather than a surprise at step four.
