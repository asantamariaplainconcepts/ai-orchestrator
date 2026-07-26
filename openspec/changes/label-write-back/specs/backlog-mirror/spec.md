# backlog-mirror

## ADDED Requirements

### Requirement: a Member applies or removes a trigger label through the Connector

The system SHALL let a Member apply or remove a label on a mirrored Story via
`PUT`/`DELETE /api/projects/{projectId}/backlog/stories/{vendorStoryId}/labels/{label}`. The
write SHALL go to the vendor through the Connector seam **before** the mirror changes, and the
mirror SHALL then be re-synchronised through the same reconciliation path polling uses — so
portal labelling and vendor labelling are one mechanism (DEC-027) and the resulting
`StoryChanged` event drives matching identically. A vendor-rejected write SHALL surface its
distinct error and leave the mirror untouched. Both operations SHALL be idempotent. The portal
SHALL offer apply/remove for enabled Automations' trigger labels and render other labels
read-only.

#### Scenario: the portal drives the loop

- **WHEN** a Member applies an enabled Automation's trigger label to a Story from the backlog
  page
- **THEN** the vendor receives the label, the re-synchronised mirror shows it, and a Run is
  created by the ordinary matching path

#### Scenario: removal writes back the same way

- **WHEN** the Member removes a trigger label they applied
- **THEN** the vendor no longer has the label and the mirror, once re-synchronised, agrees

#### Scenario: the vendor refuses

- **WHEN** the vendor rejects the write (unavailable or permission)
- **THEN** the API returns the vendor's distinct error and the mirrored Story is unchanged

#### Scenario: idempotence follows HTTP

- **WHEN** the same PUT is repeated, or a DELETE targets a label the Story does not carry
- **THEN** the outcome equals the single application / a successful no-op
