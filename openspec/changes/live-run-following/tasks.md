# Tasks — live-run-following

- [ ] 1.1 `AgentInstruction.OnOutput` (optional); both process wrappers forward lines.
- [ ] 2.1 Chunk table + migration; the channel-owning writer (design D2); executor wires the
      sink and completes it after the runtime returns.
- [ ] 3.1 Log read slice with `complete` (D3); Run page polls while executing; mock route.
- [ ] 4.1 Functional: growth, crash-partial, finished-complete; the stub runtime forwards
      lines so the tests exercise the real writer.
- [ ] 5.1 DEC-050 (revises DEC-031) + UC-027; CI's filtered command locally; CI green.
