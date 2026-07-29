# Tasks — readable-run-output

- [ ] 1.1 `ClaudeCodeHeadlessRuntime` asks for `--output-format stream-json --verbose` (design D1).
- [ ] 1.2 Its result parser reads the terminal `result` event from the stream instead of parsing the
      whole of stdout, keeping the null-on-miss behaviour BR-011 turns into unknown (design D1).
- [ ] 1.3 A test that fails on the flag change alone: a streamed stdout yields a **succeeded** Run with
      its reply and usage — the regression the current `catch (JsonException)` would have produced.
- [ ] 2.1 A frontend line interpreter: JSON-if-it-parses else text, lifting `type`, a text body and
      token counts when present, pretty-printing the rest (design D2). No runtime branching.
- [ ] 2.2 Tool invocations render as one compact line naming the tool and its subject, with the full
      object behind a disclosure.
- [ ] 3.1 Assistant text renders through `renderStoryMarkdown`, with no session, message or part id in
      the prose (design D3).
- [ ] 4.1 A running tokens/cost total derived from the lines, showing unknown rather than zero, and
      yielding to the Run's recorded usage once it has ended (design D4).
- [ ] 5.1 An uninterpretable line renders verbatim and does not break the section (design D5).
- [ ] 6.1 The Output section's four states — empty, loading, failed, populated — in both themes and
      keyboard-reachable.
- [ ] 7.1 i18n keys for the new labels; the mock serves a transcript with at least one text event, one
      tool event and one unparseable line.
- [ ] 8.1 Unit tests for the interpreter: an opencode-shaped event, a Claude-shaped event, an object
      with no recognised text field, a non-JSON line, an event carrying usage, and an event carrying
      none.
- [ ] 9.1 E2E: a Run page shows the agent's sentence as prose with no part id visible, and a malformed
      line still appears.
- [ ] 10.1 CI green; evidence on #130.
