# Proposal: readable-run-output

## Why

Issue #130 (ACT-002; UC-027, UC-020; BR-011, BR-010, BR-005). The Run page is the screen a Member
watches while the product works, and it is the least readable thing we ship: the Output section shows
opencode's raw event envelopes, with the sentence the agent wrote buried in `part.text` beside session
and part ids that mean nothing to a reader — while the table above says the cost is **unknown**, with
the token counts sitting in the same JSON.

Two of this change's findings are worth stating before the design, because they change what the work
is:

- **The default runtime's silence is one flag, not missing infrastructure.** `HeadlessProcess` already
  raises `OutputDataReceived` per line and invokes `OnOutput` for each
  (`HeadlessProcess.cs:47`), so the streaming path has always worked. `ClaudeCodeHeadlessRuntime` asks
  for `--output-format json` (`ClaudeCodeHeadlessRuntime.cs:40`), which prints one document at exit.
  Nothing needs building; a flag needs changing.
- **The spec already required this and the default runtime never honoured it.** `agent-execution`
  says output "SHALL be persisted incrementally while a Run executes". For `ClaudeCodeHeadless` that
  has been false since it was written. AC-1 is therefore a defect against an existing requirement
  rather than new scope, which is why the delta modifies that requirement instead of adding one.

## What changes

- **The default runtime streams** (design D1): `--output-format stream-json --verbose`, and the result
  parser reads the terminal `result` event instead of parsing the whole of stdout.
- **The Output section becomes a transcript** (design D2), rendered **dialect-tolerantly**: every line
  is "a JSON object if it parses, text if it doesn't". Well-known fields are lifted when present; the
  rest is pretty-printed into a collapsible block. The portal never branches on which runtime wrote a
  line, so a third runtime needs no frontend change.
- **The agent's prose goes through the existing sanitiser** (design D3), `renderStoryMarkdown` — the
  same belt-and-braces path a Story description takes, because agent output is exactly as untrusted.
- **Tool invocations become one compact line each** (design D2), naming the tool and its subject.
- **Spend is counted as it arrives** (design D4): a running total from the lines themselves, showing
  **unknown** rather than zero when the events carry no usage (BR-011).
- **Nothing recognised is nothing lost** (design D5): an uninterpretable line renders verbatim, and
  the page does not fail.

## Impact

- Specs: `agent-execution` — one MODIFIED requirement (incremental output, now true of every runtime,
  with the result read from the terminal event). `run-orchestration` — one ADDED requirement (the
  output reads as a transcript).
- Code: `ClaudeCodeHeadlessRuntime` (the flag and the result parser) plus the Run screen's Output
  section and a new line-interpreting module in the frontend.
- **No schema change, no migration, no normalised event table.** Each stored chunk still holds the
  exact line the process emitted, and the log endpoint is untouched: the transcript is produced at
  render time from the same string the page already receives.

## Out of scope

- Redacting secret values a tool might echo into a log. Pre-existing since #96 — the chunks are
  already stored and served raw, and rendering them more prettily does not worsen it. Its own slice,
  with its own acceptance criteria.
- Interpreting either runtime's event *semantics* beyond the well-known fields — no per-runtime
  branching, which is the whole point of D2.
- Downloading or exporting a transcript, and any third runtime.
