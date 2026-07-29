# Tasks — actionable-failure-inbox

- [ ] 1.1 `Run.DismissedAt` and its migration; `Dismiss(at)` refusing anything that is not `Failed`.
- [ ] 2.1 A dismiss slice, and the Run read exposing the timestamp (design D3).
- [ ] 3.1 The inbox's failure lane and the pulse's failure count share one condition — failed, no
      newer Run, not dismissed (design D5).
- [ ] 4.1 The Run page offers *Run again* on a Failed Run, calling the existing Run-now path with the
      Run's own Automation (design D1), and shows BR-001's refusal in Run now's voice.
- [ ] 5.1 The Run page offers *Dismiss*, and a dismissed Run shows the fact and its time.
- [ ] 6.1 Tests: re-running creates a Run through the same path and the entry leaves the inbox;
      re-running with an active Run is refused naming BR-001; dismissing removes the entry from both
      the inbox and the pulse count while the Run stays `Failed`; dismissing a non-failed Run is
      refused; a dismissed Run shows when.
- [ ] 7.1 Four states, both themes, i18n catalogue, focus visible.
- [ ] 8.1 CI green; evidence on #145.
