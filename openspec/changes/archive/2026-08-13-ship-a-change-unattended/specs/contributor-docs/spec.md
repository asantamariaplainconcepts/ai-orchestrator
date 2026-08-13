## MODIFIED Requirements

### Requirement: the solo path and the spec-less lane are documented, not folklore

`CONTRIBUTING.md` SHALL state the solo-maintainer review path (DEC-016: GitHub forbids
self-approval, so the recorded gate is the label transition plus the PR checklist) and the
spec-less lane (DEC-025: `lane:spec-less`, retro still mandatory, nothing to archive).

It SHALL also state the **unattended route** (DEC-068, ADR-0027): `/aio:ship` carries a ready issue to
`main` in one run with no review stage, the invocation is the recorded authorisation in place of
DEC-016's in-session go-ahead, a halt applies the hold and hands back to a person, and the staged
route remains the default. It SHALL name what the route gives up — that no human reads the spec or the
diff — rather than presenting it as a faster equivalent.

#### Scenario: a contributor hits the self-approval wall

- **WHEN** someone tries to approve their own PR to satisfy the sync gate
- **THEN** `CONTRIBUTING.md` already tells them what the recorded gate is instead

#### Scenario: a contributor finds an unreviewed change on main

- **WHEN** someone reads a merge commit whose PR says it was shipped unattended
- **THEN** `CONTRIBUTING.md` already explains the route that produced it, what authorised it, and
  when it is appropriate — so an unreviewed merge is documented practice rather than an anomaly to
  reconstruct
