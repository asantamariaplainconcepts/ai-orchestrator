# Proposal: project-roles

## Why

Issue #13 (ACT-001; UC-002; BR-009). #12 shipped sign-in with a stated interim rule: **every signed-in
user holds Admin**. That was honest as a bridge and is untenable as a destination — it means anyone
the tenant admits can reconfigure any project, rotate credentials and dispatch spend. BR-009 has been
written since the charter and unenforceable ever since, because nothing named a permission and nothing
knew a caller's role on a project.

## What this actually is

The issue reads like "add a roles table". It is not, and the grill found why: **BR-009's roles are
project-scoped, and `Principal` carries one global role with no project in it**. So the work is to make
permission a function of *caller and project*, which changes the seam every module reads.

## What changes

- **Permission becomes a declared property of each operation** (design D1). Commands and queries name
  the permission they require; a decorator in the fixed CQS pipeline enforces it. BR-009's own words —
  "every operation names a required permission" — become mechanical rather than aspirational.
- **The principal keeps its identity and loses its global role** (design D2). `ICurrentPrincipal`
  answers *who*; a new seam answers *what may they do here*, given a project. The inline Admin checks
  in `ConfigureConnector` (#119's, hand-copied twice) are subsumed by the decorator.
- **Roles are project-scoped rows** (design D3), assigned by an Admin of that project from the Settings
  tab, with the two fixed bundles DEC-034 locked: Admin is everything, Member observes and triggers.
- **Bootstrap administrators come from configuration** (design D4), never from a race. With none
  configured **nobody is Admin**, and the portal says so — a deployment without an administrator is a
  real state and gets a voice, exactly as "this deployment authenticates nobody" did.
- **`/api/me` stops claiming a single role** (design D5): it reports who you are and your role per
  project you can see, because that is the only honest shape once roles are scoped.

## Impact

- Specs: new `authorization` capability (the permission model and its enforcement point);
  `backend-architecture` — one MODIFIED requirement (the identity seam, which stops carrying a role).
- Code: a permission declaration on each use case, one pipeline decorator, a project-roles table and
  its migration, the assignment slice, the Settings surface, and `/api/me`'s reshaping. The two inline
  checks in `ConfigureConnector` are deleted, not adapted.
- Config: `Auth:BootstrapAdmins` — provider object ids, set as a repository variable in the deployed
  environment the same way the Entra ids are, so it stays out of git.

## Out of scope

- Roles beyond DEC-034's two bundles, and any per-operation custom permission set. The bundles are
  locked; this makes them enforceable, it does not redesign them.
- Inviting people who have never signed in. A role attaches to a provider identity, and until someone
  signs in once there is no identity to attach it to — a real limitation, and its own slice.
- Cross-project or tenant-wide administration. Every role in this slice is scoped to one project.
- Auditing role changes beyond what BR-014 already records.
