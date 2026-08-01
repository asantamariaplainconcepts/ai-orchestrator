## 1. Backend — the install use case

- [x] 1.1 Add the install command use case: given project + starter id, re-check presence at the
      target path on the default branch (refuse naming the path if present, design D3), prepare a
      workspace via the existing `ICodeWorkspace` pipeline, write the starter bytes at
      `<prompts directory>/<filename>`, commit, push a starter-scoped deterministic branch
      (design D2), open a **draft** PR, return its URL
- [x] 1.2 Stage-named refusals: clone / push / PR failures each carry their stage and the vendor's
      reason (implement's voice); "no Connector" refuses before any workspace exists
- [x] 1.3 Functional-test the use case: happy path (URL returned), already-present refusal,
      no-Connector refusal

## 2. Frontend — the Install action

- [x] 2.1 Load the `aio-design` skill; add *Install* to the starter card (offered only with a
      Connector), pending state included
- [x] 2.2 Render the returned PR URL on the card with "review is the next step" copy; render each
      stage-named refusal
- [x] 2.3 i18n catalog entries for the action, the pending state, the PR message and every refusal

## 3. Spec truth

- [x] 3.1 Update the archived spec text at archive time via the delta (offering-writes-nothing
      narrowed; install requirement added) — verify `openspec validate` passes

## 4. Proof

- [x] 4.1 Verify in the browser preview against a real project: install produces a draft PR whose
      URL renders; re-install on the same starter refuses or reuses the branch (design D2); a
      merged install flips the starter to "already present"
- [x] 4.2 Run the full gates (build, tests, lint) and the spec validation
