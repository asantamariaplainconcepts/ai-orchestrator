## 1. Publishing, bounded by the sandbox's life (design D1, D3)

- [x] 1.1 The Automation names a preview port; nothing named means no preview, and every existing
      Automation is therefore unchanged. Decide port-on-Automation vs port-on-Project against the
      design's open question before writing the column. (Decided **Automation**: the Project knows
      the application, but two Automations over one repository may start different things, and
      only the prompt knows whether its change is runnable. Nullable column, additive migration,
      so every existing row reads as no preview. The bound (1–65535) is validated at save, where
      the Admin is looking, rather than by docker at Run time in front of somebody who did not
      choose the number.)
- [x] 1.2 The sandbox host publishes that port when it creates the sandbox — `-p <sandboxPort>`
      with the host port **omitted**, which is the ephemeral form (`-p 0:` is rejected outright:
      "port 0 out of range"), then reads back the allocated host port. (A preview that cannot be
      resolved is logged and the Run proceeds WITHOUT one — the agent's work is the Run, and a
      missing window is a missing window, not a failure.)
- [x] 1.3 The allocated port is recorded in a per-process ledger beside the pods ledger, written
      on publish and removed in the same `finally` that disposes the sandbox — so no code path
      exists in which a record outlives its sandbox. A test that cancels mid-Run asserts the
      record is gone. (`RunPreviewHost`, in memory for the pods ledger's stated reason. Removal
      happens BEFORE disposal is attempted, so a failed removal cannot leave a reachable-looking
      entry pointing at a port nothing serves.)

## 2. The read that cannot lie (design D2)

- [x] 2.1 An endpoint answers "does this Run have a live preview right now", reading the ledger
      and the Run's state — never a stored field. A terminal Run answers no by construction, not
      by a branch that could be forgotten. (Terminality reads `RunStates.IsTerminal`, not a third
      hand-written copy — the comment on RunStates records that such copies have drifted twice.)
- [x] 2.2 A process holding no sandboxes answers that previews are unavailable in this habitat,
      distinct from a Run having none — the "not hosted here" sentence the pods panel already
      owes.

## 3. The relay (design D4)

- [x] 3.1 A proxy endpoint scoped to one Run's published port: it resolves the port from the
      ledger, refuses any other target, and applies the Run's own authorization at the relay.
      Tests cover the refusals, not only the happy path.
- [x] 3.2 Nothing is serving yet is a state with its own answer, distinguishable from the Run
      having no preview and from the relay refusing. (503 from the relay when the port is
      published but nothing is listening — the ordinary state of a Run whose agent has not
      started its server yet.)

## 4. The surface (design D4, D5, aio-design)

- [x] 4.1 The Run detail frames the preview beside the live output, with a restrictive `sandbox`
      attribute that permits scripts but grants no same-origin access, and copy naming whose
      application is being rendered. i18n as contract; routed through the design system.
      (Verified in the browser: sandbox="allow-scripts allow-forms allow-popups", no
      allow-same-origin, referrerPolicy=no-referrer.)
- [ ] 4.2 The terminal transition: a Run that finishes while its preview is open reports that and
      offers the diff, rather than a broken frame. Reachable in the mock, like every other state.
      (NOT done: the frame currently vanishes when the Run ends, which satisfies 4.3 but not the
      spec's "reports that the Run finished" scenario — a Member watching would see it disappear
      with no explanation.)
- [x] 4.3 No affordance on a terminal Run — verified by opening one, not by reading the code.

## 5. Proof

- [ ] 5.1 Unit and functional coverage: publish/dispose lifecycle, the terminal-Run answer, the
      relay's three refusals, and the unavailable-habitat sentence. Fakes that can fail.
- [ ] 5.2 The manual exercise on a machine with sbx, recorded as evidence (ADR-0001): a real Run
      serving a real page, viewed in the portal, then the Run ending and the affordance
      disappearing — including anything that did not work.
- [ ] 5.3 Full gates: build, tests, CSharpier, ESLint, tsc, the design-system validator, spec
      validation.
