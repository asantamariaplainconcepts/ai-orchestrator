## 1. Domain + persistence (Projects)

- [x] 1.1 `Project.DefaultRuntime` (nullable) + `ProjectRuntimeCredential` collection;
      `Automation.Runtime` nullable; one migration; existing rows untouched.
- [x] 1.2 Settings endpoints: Admin-scoped read/write of default + credential names (BR-009,
      BR-010 — names only, never echoed to non-Admins).
- [x] 1.3 Contracts: `IProjectRuntimeSettings.Resolve(projectId)` for the Runs module; the
      Automation catalog's `Runtime` becomes nullable in its contract views.

## 2. Execution (Runs + ServiceDefaults)

- [x] 2.1 One resolution function (design D2): run choice → automation → project default →
      deployment default; credential project → deployment → none; the executor's inline chain is
      deleted, not paralleled. Transcript names the credential's source.
- [x] 2.2 Run now and re-run accept the optional runtime; recorded on `Run.RuntimeName`;
      run-on-change already does.
- [x] 2.3 AC6: runtimes export env vars only for non-empty values (design D5).

## 3. Surfaces (frontend)

- [x] 3.1 Settings: default-runtime select + per-runtime credential name inputs, Admin-only.
- [x] 3.2 Automation form: "Project default" first option naming the current resolution; sends
      null; edit round-trips it.
- [x] 3.3 Run now dialog: runtime select pre-set to the resolution; re-run path reuses it; the
      approval bar shows the resolved runtime (D3 — no re-choice).
- [x] 3.4 i18n + mock coverage for the new states.

## 4. Tests

- [x] 4.1 Functional (Projects): settings CRUD with BR-009/BR-010 shapes; nullable-runtime
      Automation round-trip; existing rows unchanged post-migration.
- [x] 4.2 Functional (Runs): the chain's order pinned (all four levels); per-Run choice recorded
      and not persisted anywhere else; label-triggered Runs offer no override; project credential
      outranks deployment (transcript names source).
- [x] 4.3 Unit: env construction never carries an empty credential variable.

## 5. Verification

- [x] 5.1 Gates: csharpier, frontend, design validator, production build; module suites local.
- [x] 5.2 Mock mode: settings surface, the form's default option, the dialog pre-selection —
      explicit `resize_window` first (the viewport lesson).
- [ ] 5.3 CI green on the PR head (verified job-by-job), at sync.
- [x] 5.4 AC6's real Local run: recorded as exercised only when actually run on this machine
      (ADR-0005); otherwise it ships as the stated hypothesis the unit pin backs.
