## 1. Connector seam — the directory listing read

- [ ] 1.1 Add the directory-listing read to the seam interface (names only, one level, default
      branch), with vendor-neutral result types distinguishing "absent directory" from "vendor
      refusal" (design D1/D3)
- [ ] 1.2 Implement it for GitHub (contents API), confined to the GitHub connector class
- [ ] 1.3 Implement it for Azure DevOps beside GitHub, translation unit-tested both directions,
      hypothesis label per ADR-0005 (class doc + no exercised claim)
- [ ] 1.4 Unit-test the GitHub translation and both failure shapes (absent vs refused)

## 2. Backlog module — the query use case

- [ ] 2.1 Add the prompts-listing query use case (`GET /api/projects/{projectId}/prompts`): resolve
      the Connector, read its prompts directory setting, resolve the PAT by name at call time
      (BR-010), perform the seam read, return names + the degradation reason as data (design D2/D3)
- [ ] 2.2 Handle "no Connector" as the same ordinary degraded outcome, never a 500
- [ ] 2.3 Functional-test the use case: listed names, absent directory, no Connector

## 3. Frontend — the picker

- [ ] 3.1 Load the `aio-design` skill; build the prompt-name field as a picker-with-free-text on
      the design system (same pattern as the output-label picker, #165)
- [ ] 3.2 Query hook fetching on field focus, TanStack Query de-duplicated (design risks)
- [ ] 3.3 Degraded rendering: plain text input + readable reason when the endpoint says so; saving
      stays possible in every case
- [ ] 3.4 i18n catalog entries for the field, suggestions, and each degradation reason — no
      hardcoded copy

## 4. Proof

- [ ] 4.1 Verify in the browser preview against a real project: listed prompts appear on focus,
      free text still saves, degradation renders on a project with no Connector
- [ ] 4.2 Run the full gates (build, tests, lint) and the spec validation
