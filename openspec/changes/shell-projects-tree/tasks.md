## 1. The mirror answers "which Stories are held"

- [x] 1.1 Add the held-Stories member to `IStoryReader`
      (`src/modules/Backlog/AiOrchestrator.Modules.Backlog.Contracts/IStoryReader.cs`), returning
      vendor id and title per held Story, with the docstring stating why it exists rather than a
      `VendorStoryIds` + `Find` loop (one round trip per Story, per project, on a shell cadence).
- [x] 1.2 Implement it in the Backlog module: project `(VendorId, Title, Labels)` for the project
      and filter with `StoryHold.IsHeld`. Do **not** push the test into SQL — `Labels` is `text[]`
      and its containment operator is case-sensitive (design D3).
- [x] 1.3 Test the case fold: Stories labelled `hitl`, `HITL` and `Hitl` are all held; a
      Story with no labels and one with unrelated labels are not.
      **Written functional, not unit** (`HeldStories_Should_Constraint.cs`): `StoryHold.IsHeld`'s
      fold already has unit coverage in `StoryHold_Should_Constraint.cs`, so a second unit test
      would re-test that and not this. What was untested is the read against real Postgres, where
      `Labels` is `text[]` — the one place a SQL-side `Contains` would pass in memory and miss
      `HITL` in production.
- [x] 1.4 `dotnet build` and the Backlog unit tests pass.

## 2. The cross-project in-flight read

- [x] 2.1 Add `GetInFlight.cs` under
      `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Observation/UseCases/`, modelled on
      `GetInbox.cs`: `GET /api/in-flight`, `[Requires(Access.FiltersToCaller)]`, internal sealed
      query/response records, `WithTags("Runs")`.
- [x] 2.2 Scope it by `IProjectPermissions.VisibleProjects`, falling back to
      `IProjectCatalog.ActiveProjectIds` for the null-means-all caller — the pattern
      `GetInboxChanges` established. A non-visible project must be **absent**, not empty.
- [x] 2.3 Report each visible project's non-terminal Runs (`Queued`, `Executing`,
      `AwaitingInput`) grouped under the **subject** they belong to, and its held Stories from
      task 1. No Run is reported bare.
      **Corrected during implementation** (design D9): a subject is a Story **or** an open change,
      because `Run` targets exactly one of the two and `VendorStoryId` is null for a change-targeted
      Run. The original wording assumed every Run has a Story. Specs updated, not the code bent.
- [x] 2.4 Keep it read-model-only — the Runs tables and the Postgres Mirror, no vendor call. Add
      the comment saying why, referencing the hazard `GetInboxChanges` documents.
- [x] 2.5 Leave `GET /api/inbox` untouched. Verify by reading `GetInbox.cs` after the change:
      nothing in it moves.

## 3. Backend verification

- [x] 3.1 Functional tests in `src/tests/modules/Runs/AiOrchestrator.Modules.Runs.FunctionalTests`:
      a project with a held Story and no Run; a project with a `Queued` Run; a project whose only
      Runs are terminal (reports nothing, *including* an undismissed failure the Inbox still
      shows); a project with no live work.
- [x] 3.2 The BR-009 test, named for what it asserts: a caller who may see one of two projects
      gets a response carrying no id, name, or Story title from the other. This is AC 5 and it is
      a test, not a comment.
- [x] 3.3 A test asserting `/api/inbox`'s response is byte-identical before and after live work
      exists — the ambient count cannot move (AC 6).
- [x] 3.4 `dotnet build` clean, ArchTests and NetArchTest green (the module boundary must still
      reject implementation references), `dotnet test` for the Runs and Backlog projects.
- [x] 3.5 CSharpier passes on every touched `.cs` file.

## 4. One shared state chip

- [x] 4.1 Add the shared Run-state chip to `src/frontend/shared/ui/`, modelled on `locus.tsx`:
      glyph beside a word, tokens only, one exported component. Cover `Queued`, `Executing`,
      `AwaitingInput` and the terminal states, plus the **hold** as its own distinct treatment.
- [x] 4.2 Add every state's label to `src/frontend/shared/i18n/en.ts`. No state's label is an enum
      name.
- [x] 4.3 Give `Executing` and `Succeeded` different glyphs **and** different words — not merely
      different colours. Verify the chip is legible in greyscale.
- [x] 4.4 Migrate `src/frontend/features/runs/RunsSection.tsx` onto the shared chip and delete its
      local `StateBadge`, which rendered `{state}` verbatim and painted `Succeeded`/`Executing`/
      `Planning` one green.
- [x] 4.5 Grep the frontend for any other local state-to-appearance mapping and migrate or remove
      it, so the shared chip is the only one (`design-contract`).
- [x] 4.6 Check the Run detail and the Runs list by eye after the migration: same states, same
      words, nothing regressed to a bare colour.

## 5. The tree in the shell

- [x] 5.1 Add the typed query hook and types for `/api/in-flight` in a feature slice (not
      `shared/` — that is cross-cutting plumbing only, per `frontend-architecture`), on the same
      30s cadence `useInbox` uses.
- [x] 5.2 Add every new string to `src/frontend/shared/i18n/en.ts`. No literal user-facing copy in
      JSX — it fails CI (DEC-021).
- [x] 5.3 Turn `NavItems` in `src/frontend/shared/ui/AppShell.tsx` into the tree: project rows →
      `/projects/:id`, Story rows → the story route, Run rows → `/projects/:id/runs/:runId`. Keep
      one component feeding all three containers — two copies is how the phone lost the identity
      block before.
- [x] 5.4 Render each Run row's state and each held Story's hold through the shared chip from group
      4 — never a treatment defined inside the tree.
- [x] 5.5 A project with no live work renders as its row alone: no empty group, no placeholder, no
      zero count.
- [x] 5.6 Keep the Inbox entry and its badge exactly as they are, above or below the tree as the
      layout dictates — but unchanged in behaviour.
- [x] 5.7 Rail behaviour: every project stays present as a glyph at
      `--sidebar-w-collapsed` (64px), and opening one reveals the same children with the same
      destinations via `popover.tsx` — the idiom the environment chip already uses on the rail. Do
      not add a collapsible/accordion primitive; none exists and per-project collapse is out of
      scope.
- [x] 5.8 Sheet behaviour: the same entries rendered inline, never a popover inside the drawer
      (`design-contract`).
- [x] 5.9 Every collapsed entry — nested ones included — carries its name via `aria-label` and
      `title`.
- [x] 5.10 Tokens and kit primitives only; no raw hex, no raw pixel values (DEC-051, `DESIGN.md`).

## 6. Frontend verification

- [x] 6.1 `pnpm lint` passes at `--max-warnings=0` and `pnpm format:check` passes.
- [x] 6.2 `pnpm typecheck` (`tsc --noEmit`) passes.
- [x] 6.3 `pnpm build` passes — run it through `rtk proxy` (or plain `pnpm`), never a filter that
      can report success over a broken build.
- [x] 6.4 Run the design validator; it reports no raw values in the touched components.

## 7. E2E and the corpus

- [x] 7.1 E2E in `src/tests/AiOrchestrator.EndToEndTests`: the expanded tree shows a project's
      held Story and in-flight Run with the right destinations. Extend
      `CollapsibleSidebar_Should_Constraint.cs` for the parity claim — the collapsed rail offers
      the same destinations as the expanded tree. A unit test cannot assert this honestly.
- [x] 7.2 Build the frontend bundle before running E2E — the suite serves the built output, so a
      `.tsx` edit is invisible to it until `pnpm build` has run.
- [x] 7.3 Add **UC-033 — a Member sees every project's live work in one panel** to
      `docs/product/v1/04-capabilities.md`, actor ACT-002, tracing to UC-021 and UC-026 and citing
      BR-009, BR-001, BR-002 and DEC-067 (RULE-003).
- [x] 7.4 Confirm no product-corpus requirement changed — only the capability list gained an entry.

## 8. Close out

- [ ] 8.1 `openspec validate shell-projects-tree` passes.
- [ ] 8.2 Full solution build and test run green; commit incrementally so the branch keeps its
      narrative.
