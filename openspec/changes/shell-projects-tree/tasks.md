## 1. The mirror answers "which Stories are held"

- [ ] 1.1 Add the held-Stories member to `IStoryReader`
      (`src/modules/Backlog/AiOrchestrator.Modules.Backlog.Contracts/IStoryReader.cs`), returning
      vendor id and title per held Story, with the docstring stating why it exists rather than a
      `VendorStoryIds` + `Find` loop (one round trip per Story, per project, on a shell cadence).
- [ ] 1.2 Implement it in the Backlog module: project `(VendorId, Title, Labels)` for the project
      and filter with `StoryHold.IsHeld`. Do **not** push the test into SQL — `Labels` is `text[]`
      and its containment operator is case-sensitive (design D3).
- [ ] 1.3 Unit-test the case fold: Stories labelled `hitl`, `HITL` and `Hitl` are all held; a
      Story with no labels and one with unrelated labels are not.
- [ ] 1.4 `dotnet build` and the Backlog unit tests pass.

## 2. The cross-project in-flight read

- [ ] 2.1 Add `GetInFlight.cs` under
      `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Observation/UseCases/`, modelled on
      `GetInbox.cs`: `GET /api/in-flight`, `[Requires(Access.FiltersToCaller)]`, internal sealed
      query/response records, `WithTags("Runs")`.
- [ ] 2.2 Scope it by `IProjectPermissions.VisibleProjects`, falling back to
      `IProjectCatalog.ActiveProjectIds` for the null-means-all caller — the pattern
      `GetInboxChanges` established. A non-visible project must be **absent**, not empty.
- [ ] 2.3 Report each visible project's non-terminal Runs (`Queued`, `Executing`,
      `AwaitingInput`) grouped under the Story they belong to, and its held Stories from task 1.
      Every Run carries its Story; no Run is reported bare.
- [ ] 2.4 Keep it read-model-only — the Runs tables and the Postgres Mirror, no vendor call. Add
      the comment saying why, referencing the hazard `GetInboxChanges` documents.
- [ ] 2.5 Leave `GET /api/inbox` untouched. Verify by reading `GetInbox.cs` after the change:
      nothing in it moves.

## 3. Backend verification

- [ ] 3.1 Functional tests in `src/tests/modules/Runs/AiOrchestrator.Modules.Runs.FunctionalTests`:
      a project with a held Story and no Run; a project with a `Queued` Run; a project whose only
      Runs are terminal (reports nothing, *including* an undismissed failure the Inbox still
      shows); a project with no live work.
- [ ] 3.2 The BR-009 test, named for what it asserts: a caller who may see one of two projects
      gets a response carrying no id, name, or Story title from the other. This is AC 5 and it is
      a test, not a comment.
- [ ] 3.3 A test asserting `/api/inbox`'s response is byte-identical before and after live work
      exists — the ambient count cannot move (AC 6).
- [ ] 3.4 `dotnet build` clean, ArchTests and NetArchTest green (the module boundary must still
      reject implementation references), `dotnet test` for the Runs and Backlog projects.
- [ ] 3.5 CSharpier passes on every touched `.cs` file.

## 4. The tree in the shell

- [ ] 4.1 Add the typed query hook and types for `/api/in-flight` in a feature slice (not
      `shared/` — that is cross-cutting plumbing only, per `frontend-architecture`), on the same
      30s cadence `useInbox` uses.
- [ ] 4.2 Add every new string to `src/frontend/shared/i18n/en.ts`. No literal user-facing copy in
      JSX — it fails CI (DEC-021).
- [ ] 4.3 Turn `NavItems` in `src/frontend/shared/ui/AppShell.tsx` into the tree: project rows →
      `/projects/:id`, Story rows → the story route, Run rows → `/projects/:id/runs/:runId`. Keep
      one component feeding all three containers — two copies is how the phone lost the identity
      block before.
- [ ] 4.4 A project with no live work renders as its row alone: no empty group, no placeholder, no
      zero count.
- [ ] 4.5 Keep the Inbox entry and its badge exactly as they are, above or below the tree as the
      layout dictates — but unchanged in behaviour.
- [ ] 4.6 Rail behaviour: every project stays present as a glyph at
      `--sidebar-w-collapsed` (64px), and opening one reveals the same children with the same
      destinations via `popover.tsx` — the idiom the environment chip already uses on the rail. Do
      not add a collapsible/accordion primitive; none exists and per-project collapse is out of
      scope.
- [ ] 4.7 Sheet behaviour: the same entries rendered inline, never a popover inside the drawer
      (`design-contract`).
- [ ] 4.8 Every collapsed entry — nested ones included — carries its name via `aria-label` and
      `title`.
- [ ] 4.9 Tokens and kit primitives only; no raw hex, no raw pixel values (DEC-051, `DESIGN.md`).

## 5. Frontend verification

- [ ] 5.1 `pnpm lint` passes at `--max-warnings=0` and `pnpm format:check` passes.
- [ ] 5.2 `pnpm typecheck` (`tsc --noEmit`) passes.
- [ ] 5.3 `pnpm build` passes — run it through `rtk proxy` (or plain `pnpm`), never a filter that
      can report success over a broken build.
- [ ] 5.4 Run the design validator; it reports no raw values in the touched components.

## 6. E2E and the corpus

- [ ] 6.1 E2E in `src/tests/AiOrchestrator.EndToEndTests`: the expanded tree shows a project's
      held Story and in-flight Run with the right destinations. Extend
      `CollapsibleSidebar_Should_Constraint.cs` for the parity claim — the collapsed rail offers
      the same destinations as the expanded tree. A unit test cannot assert this honestly.
- [ ] 6.2 Build the frontend bundle before running E2E — the suite serves the built output, so a
      `.tsx` edit is invisible to it until `pnpm build` has run.
- [ ] 6.3 Add **UC-033 — a Member sees every project's live work in one panel** to
      `docs/product/v1/04-capabilities.md`, actor ACT-002, tracing to UC-021 and UC-026 and citing
      BR-009, BR-001, BR-002 and DEC-067 (RULE-003).
- [ ] 6.4 Confirm no product-corpus requirement changed — only the capability list gained an entry.

## 7. Close out

- [ ] 7.1 `openspec validate shell-projects-tree` passes.
- [ ] 7.2 Full solution build and test run green; commit incrementally so the branch keeps its
      narrative.
