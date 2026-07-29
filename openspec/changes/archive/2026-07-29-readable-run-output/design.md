# Design: readable-run-output

## D1 — The default runtime streams, and its parser must move with the flag

`--output-format json` buys one thing: a single well-formed document to parse at the end. Switching to
`--output-format stream-json --verbose` gives up exactly that, and the current parser is built on it —
`JsonDocument.Parse(stdout)` over the whole stream (`ClaudeCodeHeadlessRuntime.cs:72`).

With NDJSON that call throws, and the `catch (JsonException)` does something worse than fail loudly:
it returns `Succeeded: false` with the raw streams as the log. **Every successful Run would be recorded
as a failure.** So the flag and the parser are one change, not two — the flag alone is a regression.

The parser therefore reads the **last line that parses as JSON and carries `type: "result"`**, and
takes `is_error`, `result` and the usage block from it. Last rather than first: a stream may contain
more than one object with that shape only if the CLI changes, and the terminal one is the one whose
usage is total.

Its existing defensiveness is kept, and it now matters more: a miss yields null, which BR-011 renders
as unknown. A future CLI that renames the event degrades to an unknown usage on a Run whose success is
still read from the exit code — not to a failed Run.

Two things deliberately do not change. `HeadlessProcess` is untouched: it already streams per line, and
this change only alters what those lines contain. And the Run's end-of-run usage record is still the
runtime's, parsed from the terminal event — UC-020 keeps its meaning, and D4's live counter is a view,
never the record.

## D2 — Dialect-tolerant, not dialect-specific

The tempting design is a normalised event stream behind `IAgentRuntime`, the way BR-015 normalises
webhook and polling into one shape. The issue rules it out: no new event model, no migration, the raw
line stays the stored truth.

The way to honour both the ask and the constraint is to render tolerantly. Every line is treated as:

1. **JSON that parses** → lift a small set of well-known fields when they happen to be present (a
   `type`, a text body, token counts), render those, and pretty-print whatever is left into a
   collapsible block.
2. **Anything else** → verbatim.

The portal never asks *which runtime wrote this*. That is the property worth having: adding a third
runtime does not require a frontend change, it only makes more of its fields collapse. Runtime
knowledge stays out of the portal in the sense that matters, without the backend model the issue
excluded.

**The known cost, recorded so nobody rediscovers it as a bug:** field-lifting is a heuristic. A runtime
that names its text field something else degrades to a pretty-printed object rather than prose. D5 is
what makes that degradation acceptable — the line is still there, still readable, still complete.

A tool invocation is one line naming the tool and its subject, because a transcript of what the agent
*did* is most of what a watcher wants and the full argument object is almost never it. The object stays
one disclosure away.

## D3 — The agent's prose is as untrusted as a Story's

Assistant text is rendered through `renderStoryMarkdown`, the existing pipeline: `marked` configured to
emit no raw HTML, then a DOMPurify allow-list, with `javascript:` and `data:` hrefs excluded by URI
regexp.

Not a new sanitiser and not a bare `<pre>`. Agent output is model output — the same category as a Story
description written by whoever can open an issue — and this is the second place in the product to make
that judgement, so it uses the first place's answer rather than a second opinion. Belt and braces is
also the reason: either the parser config or the sanitiser alone is one regression from an XSS.

## D4 — Spend is counted from the lines, and absent stays unknown

The counter sums token counts found in the lines the page already holds, and shows a cost when the
lines carry one. It is a **view over the transcript**, computed at render time, holding no state the
page could disagree with itself about.

Absent usage reads **unknown**, never zero. BR-011 already says so and the Run table already behaves
that way; a live counter showing `0 tokens · $0.00` for a Run whose events simply do not carry usage
would be the one lie this screen has not told yet — and zero is a claim, while unknown is a fact.

The Run's own recorded usage stays authoritative once the Run ends. Where the live sum and the recorded
total differ, the recorded one wins, because it is the runtime's own report rather than the portal's
arithmetic over what it happened to see.

## D5 — A line the portal cannot read is still a line

A plain stderr line, a future runtime's dialect, a truncated write, a malformed event: each renders
verbatim, and the page keeps rendering the rest.

This is what makes D2's heuristic honest. A pretty renderer that swallows what it cannot classify would
lose exactly the lines a person is reading the log to find — the crash, the stack trace, the
unexpected. The transcript's contract is that it is complete first and pretty second.

## D6 — Rendered from the string the page already has

Each stored chunk is one line the process emitted, and the log read returns them joined. So the
transcript splits that string on newlines and interprets each line: no API change, no new endpoint, no
schema change, and AC-6 holds by construction rather than by care.

This also keeps #96's dedupe-by-sequence intact — the page still accumulates one string, and the
transcript is a function of it. Nothing about live delivery changes, which is deliberate: that
machinery works, and the complaint was never about latency.
