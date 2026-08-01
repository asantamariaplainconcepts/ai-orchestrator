# Tasks: starter-prompts

## 1. The catalogue (design D3, D4, D7)

- [ ] 1.1 `prompts/starter/portable/` — five markdown files with real frontmatter: `triage.md`,
      `explain.md`, `implement.md`, `tests.md`, `review.md`. Each names no document outside the
      project's own repository.
- [ ] 1.2 `prompts/starter/workflow/` — copies of this repository's `.claude/commands/aio/*.md`, the
      prompts this product's own development runs on. Copies, not references (D4), and the drift cost
      is recorded in `design.md` rather than mitigated by a match test.
- [ ] 1.3 A manifest beside them: tier, filename, one-sentence purpose, and the capability or tool
      each still assumes (D7). Data, not prose in a class — the endpoint and the tests read the same
      manifest.
- [ ] 1.4 Embed the catalogue into the Projects module as resources, so the tested bytes are the
      served bytes.

## 2. The endpoint (design D5, D6)

- [ ] 2.1 `GET /api/projects/{projectId}/starter-prompts`, one query slice,
      `[Requires(ProjectPermissions.ReadAutomations)]` + `IScopedToProject`. No new permission.
- [ ] 2.2 The response carries, per starter: tier, filename, target path resolved through the same
      `PromptPath` the Run path uses, purpose, assumptions, content.
- [ ] 2.3 Where the project has a Connector, read each starter's target path and mark the ones that
      already exist. Bounded by the catalogue's size, not the repository's.
- [ ] 2.4 Where there is no Connector, presence reads as **unknown** — not absent, not an error. The
      distinction is the same one BR-011 makes about cost, for the same reason.

## 3. The surface

- [ ] 3.1 A panel on the Automations tab, beside the prompt-path field and the scratchpad (#189).
- [ ] 3.2 Tiers visibly separate, each starter showing purpose, filename, what it assumes, and its
      content to copy.
- [ ] 3.3 A starter the project already has is marked as such, and the copy control still works —
      the product is not the thing preventing an overwrite, the Admin is.
- [ ] 3.4 The workflow tier states its prerequisites where they are read before they are needed.
- [ ] 3.5 Design-system gate passes.

## 4. Tests

- [ ] 4.1 A test over **every** starter the endpoint would serve: it loads, and
      `RunExecutor.StripFrontmatter` leaves a non-empty body. Enumerated from the manifest, so adding
      a starter without covering it is impossible rather than merely discouraged.
- [ ] 4.2 A test that every starter's frontmatter is actually present and actually stripped — a file
      with no `---` block would pass 4.1 while failing the criterion it exists for.
- [ ] 4.3 Functional: the endpoint returns both tiers, with target paths composed by `PromptPath`, and
      refuses a caller without the permission.
- [ ] 4.4 Functional: with a Connector and an existing file at a starter's path, that starter is
      marked present and every other is not. Without a Connector, presence is unknown for all.
- [ ] 4.5 Functional: requesting the set writes nothing — no vendor write, no agent pass.
- [ ] 4.6 E2E: the set is reachable from the Automations tab and the tiers are distinguishable.
- [ ] 4.7 Mutation-check every red (ADR-0004), confirming the build reached zero errors first. For
      each threshold or default asserted, check what value would have failed under the old behaviour
      — #189's false green came from asserting a value the old bound also allowed.

## 5. Documentation

- [ ] 5.1 `ARCHITECTURE.md`: the catalogue is offered, never written, and why — beside DEC-062 and
      the scratchpad paragraph, which are the same first hour.
- [ ] 5.2 A decision record only if review overturns D1 or D2. Both are the issue's own stated
      difficulty and were settled with the owner before proposal; recording them in `design.md` with
      the measurements is where they belong unless something contradicts them.
