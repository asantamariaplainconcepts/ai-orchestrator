# Tasks: spec-first-is-the-catalogue

## 1. The catalogue becomes one tier (design D1, D3, D5)

- [x] 1.1 Delete the `portable` tier from `Starter/manifest.json` and delete
      `Starter/portable/{triage,explain,implement,tests,review}.md`. Confirm the csproj embeds
      `Starter/**` by glob rather than per file — a per-file list would leave five dangling entries
      and a build error that reads as unrelated.
- [x] 1.2 Give `Starter/workflow/implement.md` the `ai:implement` wiring (`requiresApproval: true`,
      empty `outputLabels`) and delete the `$comment` clause at `manifest.json:2` explaining why it
      had none — the collision it describes no longer exists.
- [x] 1.3 Add a `prerequisites` block to the workflow tier: `{ file, path }` pairs. Write the files
      under `Starter/workflow/prerequisites/`. Content per D8 — the readiness rubric and the
      `RULE-001..007` shaping rules with real content, `openspec/config.yaml` with its context section
      an explicit TODO, an empty retro log, and heading-only skeletons for the documents the rubric
      links to. Two `.gitkeep` entries carry `openspec/specs/` and `openspec/changes/archive/`.
- [x] 1.4 **Do not copy this repository's product corpus.** The rubric and shaping rules are generic;
      `docs/product/mvp/` is this product's identity. Verify by reading each shipped file that no
      `ACT-`, `UC-`, `BR-` or `DEC-` identifier of this product's appears as content rather than as a
      convention example.
- [x] 1.5 Correct the three doc comments that now describe a catalogue that does not exist:
      `StarterCatalogue.cs:30` (tier ordering "the portable tier comes first"), `:116` (the
      `Requires` rationale), `:132` (the `SaveAs` rationale citing two tiers shipping `implement.md`).

## 2. The catalogue seam (design D1, D3)

- [x] 2.1 `StarterCatalogue`: `ManifestTier` and `StarterTier` gain
      `IReadOnlyList<StarterPrerequisite> Prerequisites`, loaded through the existing `Text()` resource
      reader so a declared file that is not embedded throws at first read, as prompts already do.
- [x] 2.2 `PipelineSteps.Installable` becomes `Installable(IReadOnlyCollection<string> consent)` —
      tiers with no `Requires`, plus tiers whose id the consent names. Replace the current
      `Requires is null` predicate at `PipelineSteps.cs:35-36`. Keep the doc comment's reasoning and
      update what it claims: the rule is no longer "tiers that require nothing" alone.
- [x] 2.3 Tier-id comparison is exact, not case-insensitive: ids are catalogue content the caller
      echoes back, not user-typed labels like triggers. State that on the method so the next reader
      does not "fix" it into the BR-003 comparison.

## 3. The installer writes two kinds of file (design D4)

- [x] 3.1 `StarterInstaller.File` gains `bool OnlyIfAbsent = false`.
- [x] 3.2 In the write loop (`StarterInstaller.cs:73-81`), skip a file marked `OnlyIfAbsent` whose
      target already exists in the prepared clone. The clone is the authoritative default-branch
      content — no vendor read, and no window between checking and branching.
- [x] 3.3 `Install` returns `ErrorOr<InstallOutcome>` — written paths, skipped paths, nullable
      pull-request URL — instead of `ErrorOr<string>`. Where every file was skipped, publish nothing,
      return no URL, and return **no failure**. Keep the `files.Count == 0` → `WorkspaceErrors.NoChanges()`
      refusal at `:36-41` for a genuinely empty request: that is a caller bug, not an outcome a human chose.
- [x] 3.4 Update `InstallStarterPrompt` (`:110-123`) for the new return type. It passes no
      `OnlyIfAbsent` file — its presence check at `:100-103` already refuses beforehand, and that
      refusal stays, because it names the path before any workspace exists.

## 4. Consent on the endpoint (design D2)

- [x] 4.1 `SetUpDefaultAutomations.Request` and `Command` gain `IReadOnlyList<string>? Tiers`.
      Document on the record that **absent means no tier**, and that this is the opposite default from
      `Steps` on the same record, and why (D2). The asymmetry will look like a bug to the next reader;
      one paragraph of XML doc is cheaper than the "fix".
- [x] 4.2 Pass the consent into `PipelineSteps.Installable(...)` where `gaps` is computed
      (`SetUpDefaultAutomations.cs:188-190`). With no consent the gap list is empty, so no starter is
      installed and — via the existing short-circuit — no branch and no pull request.
- [x] 4.3 `FillGaps` (`:369`) also writes the consented tiers' prerequisites, as `OnlyIfAbsent` files,
      into the same branch and pull request. Adjust the guard: today it returns early on
      `gaps.Count == 0`; it must now proceed when there are no prompt gaps but prerequisites remain to
      write — AC-7's "a consented tier with no prompt gap still brings its prerequisites".
- [x] 4.4 Update the PR body text: it currently says it installs "the starter prompts for the pipeline
      steps this repository had no file for". It now also carries process documents, and the body is
      where a reviewer learns that.
- [x] 4.5 `InstalledStarters` gains the prerequisite facts — written and skipped-as-present — kept
      separate from the prompt files, per the report requirement. Do not fold them into `Files`.

## 5. Discovery carries the tiers (design D6)

- [x] 5.1 `DiscoverPipeline.Response` gains `Tiers`: id, title, `requires`, and the prerequisite paths.
      From the catalogue the projection already walks — no extra vendor read, the constraint the plan
      requirement already imposes.
- [x] 5.2 The plan projection (`:127-149`) computes `installable` per step from
      `Installable(consent)` — but discovery has no consent. Return the step's **tier id** on each row
      instead and let the card decide, so a switch toggles without a round trip. Verify the existing
      `.Where(step => step.Exists || step.Installable)` filter still drops rows that can never act.

## 6. The card (design D6, D8)

- [ ] 6.1 Mirror every DTO change in `useWorkflowSetup.ts`: `tiers` on the discovery response,
      `tierId` on `PlannedStep`, `tiers?: string[]` on the input, the split installed report.
- [ ] 6.2 Hold consent state in `WorkflowSetupSection`, empty by default, cleared when the chosen
      candidate changes — the same reasoning as #262's exclusion reset.
- [ ] 6.3 Render the consent control **outside** the `Plan` list, so it is reachable when the plan has
      no rows. That is the empty-repository case, which is the case it exists for. Compose from the
      existing kit per `DESIGN.md`; copy through `en.ts` (DEC-021 — hardcoded JSX copy fails CI).
- [ ] 6.4 Show the tier's `requires` text and the prerequisite paths with the control. The copy states
      that the paths are written **where they are not already present** — true without any read, and
      the report tells the truth afterwards.
- [ ] 6.5 Filter the plan by consent: a row whose tier is unconsented and whose file does not exist is
      not shown. Toggling the switch adds and removes those rows.
- [ ] 6.6 Send `tiers` from the confirm. Enable the confirm when anything would happen — a consented
      gap, or an existing file to wire. With nothing consented and nothing present, there is nothing
      to press.
- [ ] 6.7 Report the prerequisites as their own fact, beside the prompts.

## 7. Mock mode

- [ ] 7.1 `mock.ts:516` serves a `portable` tier and `:156-230` build plans from the portable
      triggers. Rebuild both around the spec-first tier, with its prerequisites, so mock mode exercises
      the switch by hand.
- [ ] 7.2 Correct the comment at `mock.ts:139-141`. It states the `ai:implement → ai:tests → ai:review`
      edges make #262's marker reachable by hand. After this change the catalogue has no hand-off edges
      at all (design D10), and a comment asserting the opposite is a lie left in the codebase.
- [ ] 7.3 Have the mock's `set-up-defaults` honour `tiers` and answer with the split installed report.

## 8. Decision records and docs (design D9)

- [ ] 8.1 Write `docs/adr/0012-a-seeded-document-is-the-project-s-own.md` from `docs/adr/template.md`.
      It must carry the narrow argument — *"the weaker of the two" presumes two* — state that DEC-048's
      read-time invariant is untouched, and record the cost: on day one every repository that presses
      the button holds the same readiness bar.
- [ ] 8.2 Add **DEC-064** to `docs/product/mvp/10-locked-mvp-decisions.md`, revising DEC-048's rubric
      clause and citing the ADR. Never edit DEC-048 in place — the file's own convention.
- [ ] 8.3 Update `docs/product/manual/README.md`: the consent switch, what the pull request contains,
      and that the portable starters are gone. Judgement call at implementation whether the screenshots
      need reshooting (design Open Questions).
- [ ] 8.4 Correct `automation-configuration`'s adoption rationale if it now reads as contradicting
      itself: `openspec/specs/automation-configuration/spec.md:762` cites DEC-048 for "the copy is the
      weaker of the two". That sentence stays true — the delta spec does not touch it — but the ADR must
      be reachable from it, so a reader who finds the sentence finds the revision.

## 9. Tests

- [ ] 9.1 `StarterCatalogue_Should_Constraint.cs:93,101` asserts `tiers.First().Id == "portable"` and
      partitions on it. Rewrite around one tier that declares a prerequisite.
- [ ] 9.2 `StarterPromptsEndpoint_Should_Constraint.cs:51` asserts `tiers[0].Id == "portable"`; `:130`
      documents the two-tier `implement.md` collision. Rewrite both.
- [ ] 9.3 `SetUpDefaultAutomations_Should_Constraint.cs:38,55` asserts the wired portable set including
      `ai:tests`. Rewrite around the spec-first triggers.
- [ ] 9.4 `PipelineAdoption_Should_Constraint.cs` (lines 301, 320, 341-346, 364-367, 386-389, 407-436,
      461-464) is built throughout on the portable triggers. Rewrite around the spec-first set, keeping
      every behaviour #262 pinned: absent vs empty selection, partial selection, excluded ≠ skipped,
      case-insensitive triggers, unknown trigger matches nothing.
- [ ] 9.5 New unit: every declared prerequisite loads and has a non-empty body. Same guarantee starters
      have, for the same reason — one offered as working and broken is worse than none.
- [ ] 9.6 New unit: the duplicate-trigger refusal holds with no tier exception (the spec's new scenario).
- [ ] 9.7 New functional: no consent installs nothing — no `PreparedBranch`, no pull request, no
      failure — while files already in the directory are still wired.
- [ ] 9.8 New functional: consent installs prompts **and** prerequisites into one branch, and
      `StubInstallWorkspace.PublishedFiles` contains both, with exactly one pull request.
- [ ] 9.9 New functional: a prerequisite whose path already exists is absent from the published files
      and reported as already present; the prompts still install.
- [ ] 9.10 New functional: a consented tier whose prompts all exist but whose prerequisites do not
      still opens a pull request carrying the prerequisites alone (the 4.3 guard, asserted rather than
      assumed).
- [ ] 9.11 New functional: everything already present writes nothing, opens no pull request, and
      reports no failure.
- [ ] 9.12 New functional: an unknown tier id in the consent succeeds and installs nothing.
- [ ] 9.13 **Stated, not faked:** no E2E test for the consent surface.
      `SetupPlan_Should_Constraint.cs:18-24` already records why — plan rows need a Connector serving
      directory listings, and that tier's GitHub stub answers issues only. Record it in the doc comment;
      do not write a test that cannot reach the state.
- [ ] 9.14 **Stated, not faked:** #262's broken-hand-off marker has no reachable test after this change,
      because the catalogue has no hand-off edges left (design D10). Say so in `planHandoff.ts`'s doc
      comment rather than deleting a correct function or inventing an edge to justify it.
- [ ] 9.15 Mutation check on the load-bearing assertions: making `Tiers` absent mean *every* tier,
      dropping `OnlyIfAbsent` so a prerequisite overwrites, and folding prerequisites into the prompt
      list must each redden a distinct test. Build first — a green suite against a stale build proves
      nothing.

## 10. Gates

- [ ] 10.1 `dotnet build`, then the non-E2E suite, then the E2E suite.
- [ ] 10.2 CSharpier, Prettier, ESLint `--max-warnings=0`, `tsc --noEmit`, the design-system validator.
- [ ] 10.3 Frontend production build — run through `rtk proxy`, not `rtk`. This repository has had a
      broken build report success under `rtk` and invalidate a mutation check; grep the new copy out of
      the emitted bundle before trusting any result.
- [ ] 10.4 `openspec validate spec-first-is-the-catalogue`.
