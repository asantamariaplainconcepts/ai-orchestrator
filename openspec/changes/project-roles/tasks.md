# Tasks — project-roles

## The contract

- [x] 1.1 `Principal` loses `Role`; `ICurrentPrincipal` answers who, and its three call sites follow
      (design D2).
- [x] 1.2 A permission seam answering "what may this caller do on this project", in BuildingBlocks
      beside the principal.

## Enforcement

- [x] 2.1 Commands and queries declare their required permission; undeclared means Admin (design D1).
- [x] 2.2 An authorization decorator in `AddVsaCqsArchitecture`'s fixed chain enforces it — outside
      validation and therefore outside caching, so no cached answer can reach the wrong caller.
- [x] 2.3 The two inline Admin checks in `ConfigureConnector` are deleted, not adapted.
- [x] 2.4 A refusal names permission as the reason and does not disclose whether the project exists.
- [x] 2.5 Cross-project reads narrow their own answer to the caller's projects (design D7) — projects,
      connectors, inbox.

## The data

- [x] 3.1 A project-roles table in the Projects schema, keyed by project and provider identity id,
      with its migration (design D3).
- [x] 3.2 Bootstrap administrators from configuration, holding Admin everywhere (design D4).
- [x] 3.3 With none configured and no roles, nobody is Admin and the host announces it.
- [x] 3.4 A people table recording who this deployment has met, written by signing in **and** by
      creating a project — the invariant that a role-holder is always known (design D8).

## The surfaces

- [x] 4.1 Grant, change and remove a role, refusing a person who has never signed in, and refusing to
      leave a project with no administrator.
- [x] 4.2 The Settings tab lists roles and offers the two bundles, from the server's enum.
- [x] 4.3 `/api/me` reports identity plus role per visible project (design D5); the shell shows the
      name and stops showing a global role.

## Verification

- [x] 5.1 Functional: a Member is refused configuring and allowed observing; Admin on one project is
      nothing on another; no-role refusals disclose nothing; the projects list is scoped.
- [x] 5.2 Functional: the bootstrap administrator needs no grant (single id and a separated list), and
      the only administrator can be neither removed nor demoted while none is configured.
- [x] 5.3 Composition: an undeclared operation is refused even from an Admin, both bundles behave, a
      project-scoped declaration with no project fails loudly — plus a reflection sweep asserting
      every request in the product declares. Verified red by removing one attribute.
- [x] 5.4 E2E: the People panel is on Settings, renders for an Admin, and states both truths.
      **Scoped deliberately:** the AppHost habitat has one caller and no provider, so there is nobody
      to grant to and no sign-in to perform. The grant, the roster afterwards, the stranger refusal
      and the last-administrator dead end are covered against the real API in
      `ProjectRoleAssignment_Should_Constraint`, which composes the habitat that has roles.
- [x] 6.1 CI green; `Auth__BootstrapAdmins` wired through Terraform from a repository variable, so
      the deploy cannot leave the owner locked out of the portal.
