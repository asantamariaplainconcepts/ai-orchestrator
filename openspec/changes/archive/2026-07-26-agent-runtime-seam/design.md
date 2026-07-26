# Design — agent-runtime-seam

## D1 — The seam carries values, never names; the worker resolves names, never stores values

The queue message stays a Run id (#16 D2). The worker — already its own identity with vault
read — loads the Run, Story and Automation through the modules it composes, builds the prompt,
resolves the project PAT and the AI credential **by name** via ISecretResolver, and hands the
runtime an instruction whose credentials exist only in job memory. BR-010 keeps holding at
rest; DEC-030's one-PAT shape and DEC-014's vault placement are unchanged.

## D2 — HYPOTHESIS (ADR-0005): the headless JSON result carries usage and cost

Per the CLI's documented result schema, `--output-format json` emits a terminal object with
`is_error`, `result`, `total_cost_usd` and a `usage` block. **This is not yet observed** — the
CLI is not runnable in the authoring session. (What IS observed: the pinned CLI 2.0.44
installs and answers `--version` inside the built job image.) Spike task 0 runs the pinned CLI inside the job
container image and records the observed shape here before the parser is trusted; the spike
needs an operator-supplied AI credential in the spike shell. Until observed, the parser is
written defensively: any miss on usage/cost yields null, and BR-011 makes null safe
("unknown", never a failure) — so a wrong hypothesis degrades to honesty, not breakage.

## D3 — Terminal states arrive with the contract, because exercising it demands an ending

ADR-0001: the contract is proven by a Run that goes Queued → Executing → Succeeded/Failed in
the real job. The BR-001 partial unique index already enumerates active states explicitly
(run-orchestration D5 wrote the filter out for exactly this day); terminal states are added to
the enum and NOT to the filter, so a finished Story frees its one-active-Run slot. The
matching/run-now suites gain the case: a Story with a terminal Run can run again.

## D4 — The runtime is exercised with a deterministic instruction, not a real implementation

#19 owns "clone, implement, open a PR". Here the worker invokes the runtime with a minimal
deterministic instruction whose output proves the contract (prompt in → result out → usage
recorded or unknown). The functional tier substitutes a fake IAgentRuntime at the seam — the
same discipline as the vendor stub — and the real CLI is exercised in the container by the
spike and at first deployed run.

## D5 — One image, pinned CLI

The dispatch worker image gains node and a version-pinned Claude Code CLI. Pinning mirrors the
Azurite API-version lesson: an unpinned runtime means local and deployed jobs speak different
contracts and green means less than it appears to.
