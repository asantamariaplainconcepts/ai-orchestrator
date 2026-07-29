# Tasks — readable-run-output

- [x] 1.1 `ClaudeCodeHeadlessRuntime` asks for `--output-format stream-json --verbose` (design D1).
- [x] 1.2 Its result parser reads the terminal `result` event from the stream instead of parsing the
      whole of stdout, keeping the null-on-miss behaviour BR-011 turns into unknown (design D1).
- [x] 1.3 A test that fails on the flag change alone: a streamed stdout yields a **succeeded** Run with
      its reply and usage — the regression the current `catch (JsonException)` would have produced.
- [x] 2.1 A frontend line interpreter: JSON-if-it-parses else text, lifting `type`, a text body and
      token counts when present, pretty-printing the rest (design D2). No runtime branching.
- [x] 2.2 Tool invocations render as one compact line naming the tool and its subject, with the full
      object behind a disclosure.
- [x] 3.1 Assistant text renders through `renderStoryMarkdown`, with no session, message or part id in
      the prose (design D3).
- [x] 4.1 A running tokens/cost total derived from the lines, showing unknown rather than zero, and
      yielding to the Run's recorded usage once it has ended (design D4).
- [x] 5.1 An uninterpretable line renders verbatim and does not break the section (design D5).
- [x] 6.1 The Output section's four states — empty, loading, failed, populated — in both themes and
      keyboard-reachable.
- [x] 7.1 i18n keys for the new labels; the mock serves a transcript with at least one text event, one
      tool event and one unparseable line.
- [~] 8.1 **Not done as written — the tier does not exist.** This task asked for frontend unit tests,
      and `testing-strategy` names four tiers, all of them .NET (`*.UnitTests`, `*.FunctionalTests`,
      E2E, ArchTests); no JS test runner is installed. Covered instead by *observation*: the mock carries
      a text event, a tool event, a collapsed event and a non-JSON line, and all four were verified
      rendering in the browser — prose sanitised into two paragraphs, `Read` with `src/feature.ts` as one
      line, the warning verbatim, and no session, message or part id anywhere in the document. The
      BR-011 claim was verified by temporarily serving a usage-free log: the counter read
      `unknown · unknown`, with no zero shown.
- [~] 9.1 **Not done — the E2E tier cannot reach it.** A transcript needs a Run with stored log chunks,
      and E2E has no way to produce one: the agent runtime is real there and no endpoint appends log
      lines. Follow-up recorded on the issue.
- [ ] 10.1 CI green; evidence on #130, including the two tasks above and why.
