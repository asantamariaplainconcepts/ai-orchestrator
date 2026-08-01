# Tasks: starter-prompts

## 1. The catalogue (design D3, D4, D7)

- [x] 1.1 `prompts/starter/portable/` — five markdown files with real frontmatter: `triage.md`,
      `explain.md`, `implement.md`, `tests.md`, `review.md`. Each names no document outside the
      project's own repository.
- [x] 1.2 `prompts/starter/workflow/` — copies of this repository's `.claude/commands/aio/*.md`, the
      prompts this product's own development runs on. Copies, not references (D4), and the drift cost
      is recorded in `design.md` rather than mitigated by a match test.
- [x] 1.3 A manifest beside them: tier, source filename, **the name it saves as**, one-sentence
      purpose, and the capability each still assumes (D7). The saved name is separate from the source
      name because it had to be: both tiers ship an `implement.md`, so without it they resolved to
      one path and only one could ever be taken. Found while writing 4.4, not while writing the
      manifest.
- [x] 1.4 Embed the catalogue into the Projects module as resources, so the tested bytes are the
      served bytes.

## 2. The endpoint (design D5, D6)

- [x] 2.1 `GET /api/projects/{projectId}/starter-prompts`, one query slice,
      `[Requires(ProjectPermissions.ReadAutomations)]` + `IScopedToProject`. No new permission.
- [x] 2.2 The response carries, per starter: tier, filename, target path resolved through the same
      `PromptPath` the Run path uses, purpose, assumptions, content.
- [x] 2.3 Where the project has a Connector, read each starter's target path and mark the ones that
      already exist. Bounded by the catalogue's size, not the repository's.
- [x] 2.4 Where there is no Connector, presence reads as **unknown** — not absent, not an error. The
      distinction is the same one BR-011 makes about cost, for the same reason.

## 3. The surface

- [x] 3.1 A panel on the Automations tab, beside the prompt-path field and the scratchpad (#189).
- [x] 3.2 Tiers visibly separate, each starter showing purpose, filename, what it assumes, and its
      content to copy.
- [x] 3.3 A starter the project already has is marked as such, and the copy control still works —
      the product is not the thing preventing an overwrite, the Admin is.
- [x] 3.4 The workflow tier states its prerequisites where they are read before they are needed.
- [x] 3.5 Design-system gate passes.

## 4. Tests

- [x] 4.1 A test over **every** starter the endpoint would serve: it loads, and
      `PromptText.WithoutFrontmatter` — the routine the Run path itself calls (6.1) — leaves a
      non-empty body. Enumerated from the manifest, so adding
      a starter without covering it is impossible rather than merely discouraged.
- [x] 4.2 A test that every starter's frontmatter is actually present and actually stripped — a file
      with no `---` block would pass 4.1 while failing the criterion it exists for.
- [x] 4.3 Functional: the endpoint returns both tiers, each labelled by what it requires, with target
      paths composed by the same `PromptPath` a Run uses. **The permission refusal is not a test I
      wrote** — the slice declares `ProjectPermissions.ReadAutomations` and adds no new permission, so
      the refusal is the one `ProjectRoles_Should_Constraint` polices structurally and
      `ProjectRoleAssignment_Should_Constraint` exercises behaviourally against the same pipeline.
      Same call as #189's 4.4, and said here rather than ticked.
- [x] 4.4 Functional: with a Connector and an existing file at a starter's path, that starter is
      marked present and every other is not. Without a Connector, presence is unknown for all.
- [x] 4.5 Functional: the reads are bounded by the catalogue — one per starter, and exactly the
      collision read. **Not** "asserts nothing was written": the handler's only seam is
      `IDocumentReader`, which has no write on it, so a write would not compile and an empty-write-log
      assertion would only be asserting that nobody seeded one. #189's false green taught that.
- [x] 4.6 E2E: the set is reachable from the Automations tab and the tiers are distinguishable.
- [x] 4.7 Three mutations, each after confirming the build reached zero errors (ADR-0004):
      presence `false` instead of null when nothing was read reddens the unknown-not-absent test;
      collapsing every `saveAs` back to its source name reddens the distinct-path test; and stripping
      the frontmatter from one starter reddens the frontmatter test and nothing else.

## 5. Documentation

- [x] 5.1 `ARCHITECTURE.md`: the catalogue is offered, never written, and why — beside DEC-062 and
      the scratchpad paragraph, which are the same first hour.
- [x] 5.2 No decision record. D1 and D2 were the issue's own stated difficulty and were settled with
      the owner before the proposal, with the measurements in `design.md`. A record only if review
      overturns them.


## 6. Found during implementation

- [x] 6.1 `StripFrontmatter` moved from `RunExecutor` to `BuildingBlocks.Agents.PromptText`. The
      criterion is that a starter behaves the same run by this product or by a local agent runner,
      and that is only testable against the **same** routine the Run path uses — a test that
      reimplemented the rule would assert two implementations agree today. Same reasoning as #189's
      shared Story description, one change later.
- [x] 6.2 The two `implement.md` files collided on one target path. Fixed with an explicit saved name
      per starter (1.3) and pinned by a test (4.4), rather than by renaming one file and hoping the
      next pair is noticed.
