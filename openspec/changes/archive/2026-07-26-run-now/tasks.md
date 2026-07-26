# Tasks — run-now

## 1. The shared path

- [x] 1.1 Extract `RunCreator` from `StoryChangedHandler` with a discriminated outcome
      (design D1); the handler consumes it with unchanged behaviour — the existing matching
      suite must pass untouched.

## 2. The endpoint

- [x] 2.1 `POST /api/projects/{projectId}/runs` `{vendorStoryId, automationId}`: validate
      through `IStoryReader` + `IAutomationCatalog` (design D2), skip only detection, map
      outcomes to answers (409 BR-001, stated BR-007 limitation, 200 with waiting note at cap).
- [x] 2.2 Functional tests: no-label dispatch; 409 on active Run; cap → Queued + empty queue;
      two-phase refusal; unknown story/automation refusals; matching suite unchanged.

## 3. The portal

- [x] 3.1 Run now on each backlog row (single button when one enabled Automation; picker when
      several); refusals visible; Runs section reflects the result; catalog copy.
- [x] 3.2 Frontend lint + build; browser-verify success and refusal against the seeded local
      stack (connector row + mirror stories, per the run-visibility retro).

## 4. Close-out

- [x] 4.1 Full suite + verify sweep; ARCHITECTURE.md sentence in the Runs section; CI green.
