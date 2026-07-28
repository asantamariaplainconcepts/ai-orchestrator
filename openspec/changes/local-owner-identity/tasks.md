# Tasks — local-owner-identity

- [x] 1.1 The seam in BuildingBlocks (design D1): a principal with id, name and role, no null
      case and no "is identity configured" flag for callers to branch on.
- [x] 2.1 The local-owner implementation, composed by the host, requiring no configuration in
      the local habitats (design D4).
- [x] 3.1 The startup refusal (design D2): local owner + provisioned infrastructure or a public
      address ends the start with the reason, in the shape of the worker's database guard.
- [x] 4.1 The hosted-without-identity warning naming OPN-002 (design D3).
- [x] 5.1 Terraform asserted not to set the value — checked against the infrastructure
      definition, not assumed.
- [x] 6.1 Tests: a clean local start has an Admin principal; the refusal fires on both
      conditions; functional and E2E run with a real principal and no tenant.
- [x] 7.1 The portal shows who it thinks you are; docs updated; CI green; evidence on #119.
