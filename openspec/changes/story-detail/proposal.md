# Proposal: story-detail

## Why

Issue #37. The Mirror holds title, state and labels — not the description, which is where the
actual requirement lives. Two consequences, one visible and one not: a Member must leave the
portal to read a Story, and **the Agent implements from a headline** (the prompt built in #19
carries title/state/labels only). Mirroring the body fixes both with one field.

This change also adds **UC-022** to the corpus — *Member opens a Story and reads its detail* —
because nothing there covers a detail view and RULE-003 needs the id to exist.

## What Changes

- **`Body` on Story**, filled by the same reconciliation that handles title/state/labels and
  counted in its change detection, so an edited description updates the Mirror on the next poll
  (DEC-028) and announces itself as a `StoryChanged` like any other change.
- **`GET /api/projects/{projectId}/backlog/stories/{vendorStoryId}`** and a **Story detail
  route** in the portal reached from the backlog table: vendor id, title, state, labels, and
  the body rendered as markdown.
- **Sanitised rendering**: the body comes from whatever repository a project points at. No raw
  HTML, no script, no `javascript:` URLs — a security requirement, not a formatting preference.
- **The Agent's prompt carries the body** (scope addition recorded on the issue): `StorySnapshot`
  gains it, and `RunExecutor` includes it. One extra consumer of the same field, no new seam.

## Impact

- Affected specs: `backlog-mirror` (body + detail read), `agent-execution` (the prompt carries
  the requirement).
- Touched: Backlog module (entity, migration, reconciliation, Contracts snapshot, one read
  slice), Runs module (prompt), frontend (route, detail screen, markdown rendering + catalog),
  docs/product/mvp/04 (UC-022), tests.
- Out of scope: PR documents (#38), editing anything, comments.
