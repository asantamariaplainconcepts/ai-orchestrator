# Tasks — story-documents

## 1. The seam

- [ ] 1.1 `FindLinkedChange` + `ListChangeDocuments` + `ReadDocument` on `IBacklogConnector`
      in product vocabulary (design D1/D2); stubs in both functional fixtures.
- [ ] 1.2 GitHub implementation: timeline cross-references for the linked change, PR files
      filtered to added/modified markdown, content read at the head ref (design D3). Errors
      through the existing taxonomy.

## 2. The read slices

- [ ] 2.1 `GET .../stories/{vendorStoryId}/documents` (the linked change + its document paths,
      absence reported distinctly) and
      `GET .../stories/{vendorStoryId}/documents/content?path=…` (content at the head ref).
- [ ] 2.2 Functional tests against the stub: documents listed; no linked change; change with
      no markdown; a read failure surfacing as itself; the head-ref parameter actually used.

## 3. The portal

- [ ] 3.1 Documents section on the Story detail page: paths listed, selected one rendered via
      `renderStoryMarkdown` (design D4), the three absences distinguished (D5); catalog copy.
- [ ] 3.2 Frontend lint + build.

## 4. Close-out

- [ ] 4.1 UC-023 in docs/product/mvp/04; full suite; CI green.
