# Tasks — project-roles

## The contract

- [ ] 1.1 `Principal` loses `Role`; `ICurrentPrincipal` answers who, and its three call sites follow
      (design D2).
- [ ] 1.2 A permission seam answering "what may this caller do on this project", in BuildingBlocks
      beside the principal.

## Enforcement

- [ ] 2.1 Commands and queries declare their required permission; undeclared means Admin (design D1).
- [ ] 2.2 An authorization decorator in `AddVsaCqsArchitecture`'s fixed chain enforces it.
- [ ] 2.3 The two inline Admin checks in `ConfigureConnector` are deleted, not adapted.
- [ ] 2.4 A refusal names permission as the reason and does not disclose whether the project exists.

## The data

- [ ] 3.1 A project-roles table in the Projects schema, keyed by project and provider identity id,
      with its migration (design D3).
- [ ] 3.2 Bootstrap administrators from configuration, holding Admin everywhere (design D4).
- [ ] 3.3 With none configured and no roles, nobody is Admin and the host announces it.

## The surfaces

- [ ] 4.1 Grant, change and remove a role, refusing a person who has never signed in.
- [ ] 4.2 The Settings tab lists roles and offers the two bundles.
- [ ] 4.3 `/api/me` reports identity plus role per visible project (design D5); the shell shows the
      name and stops showing a global role.

## Verification

- [ ] 5.1 Functional: a Member is refused configuring and allowed triggering; Admin on one project is
      nothing on another; an undeclared operation requires Admin; no-role refusals disclose nothing.
- [ ] 5.2 Functional: the bootstrap administrator needs no grant, and with neither configured nor
      stored roles nobody holds Admin.
- [ ] 5.3 A test that the decorator cannot be bypassed by a handler that declares nothing.
- [ ] 5.4 E2E: an Admin grants a role from Settings and the list shows it.
- [ ] 6.1 CI green; evidence on #13, including what the deployed portal reports for a signed-in user.
