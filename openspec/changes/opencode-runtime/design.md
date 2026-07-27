# Design — opencode-runtime

## D1 — Selection is a seam, not a switch

`IAgentRuntimeSelector.For(runtimeName)` returns the runtime and its credential secret name
(or none). Composition registers the pairs; the executor stays ignorant of which runtimes
exist. A third runtime is a registration, not an executor edit — the same shape module
discovery gave hosts.

## D2 — OBSERVED (spike, CLI v1.18.6): the event stream carries usage per step

`opencode run -m opencode/deepseek-v4-flash-free --format json "Reply with exactly: ok"`
exited 0 with three JSONL events: `step_start`; `text` (part.text = "ok"); `step_finish`
(part.tokens {input, output, reasoning, cache}, part.cost = 0). Usage = sum over
`step_finish`; log = concatenated `text` parts. No credential was present. UNVERIFIED HALF
(hypothesis): that "no credential" survives a clean container environment — the authoring
machine may carry ambient opencode state. The in-container spike settles it; a wrong guess
degrades to a Failed run with the CLI's own stderr as evidence, not to silence.

## D3 — Credential absence is a supported configuration, not an error path

The executor today resolves one AI credential name unconditionally; a free-model runtime must
not fail on a vault miss for a key it does not need. The selector's credential name is
optional: none → empty credential handed to the runtime, which sets no provider variable.
Claude Code's path is unchanged (its name stays required — its provider has no free tier).

## D4 — Fail loud on unknown event shapes, but only where it matters

An unrecognised event type is skipped (the stream is decorative beyond text/step_finish); a
stream with no step_finish at all yields usage-unknown; a non-zero exit or empty stream fails
the Run with the raw streams as evidence — the same honesty split Claude Code's parser made.
