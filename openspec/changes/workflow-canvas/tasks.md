# Tasks — workflow-canvas

- [ ] 1.1 The derived graph and its computed layout (design D1): edges from label agreements,
      dependency-ordered rows, unreachable Automations trailing.
- [ ] 2.1 The canvas view on the Automations tab with the list ⇄ canvas toggle and its
      remembered preference.
- [ ] 3.1 Nodes and edges drawn: action, runtime, enabled state, trigger label; dotted edges and
      node badges in one human colour (design D2).
- [ ] 4.1 The two balloon gestures and their explicit controls (design D3), both writing through
      `UpdateAutomation` so BR-003 and the self-trigger rule apply (design D4); refusals shown
      and the canvas reverted to stored state.
- [ ] 5.1 Mock routes, 375px pass, both themes; design validator green.
- [ ] 6.1 A test that closes the chain and observes the autonomous hand-off; CI green; evidence
      on #116.
