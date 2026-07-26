# Tasks — run-visibility

## 1. The read slice

- [x] 1.1 `ListRuns` use case in the Runs module: `GET /api/projects/{projectId}/runs`,
      newest-first (`CreatedAt DESC, Id DESC`), optional `vendorStoryId` filter, response =
      the recorded BR-014 subset only.
- [x] 1.2 Functional tests on the existing Runs fixture: ordering, the story filter, the empty
      list, and the response shape (no field the Run does not record).

## 2. The portal surface

- [x] 2.1 Catalog entries + Runs section on the project page: design-system table/list, columns
      story / automation (label, action, runtime) / state / created / dispatched / output /
      logs / cost — the last three as em-dash empty values.
- [x] 2.2 Client-side join with the automations query; a Run whose Automation is gone renders
      empty automation cells (design D1).
- [x] 2.3 Per-Story filter: backlog rows link to the Runs section filtered by that story;
      the filter is visible and clearable. (API filter covered functionally; the link-click
      path needs a connected backlog, which local verification did not have — stated, not
      papered over.)
- [x] 2.4 Empty state for a project with no Runs.
- [x] 2.5 Frontend lint (catalog rule) + build green; verify both themes render.

## 3. Close-out

- [ ] 3.1 Full suite + verify sweep; CI green.
- [ ] 3.2 Exercise on the dev portal after the operator's pending `terraform apply` if it has
      happened; otherwise state the local evidence explicitly (ADR-0005 — no deployed claim
      without exercising it).
