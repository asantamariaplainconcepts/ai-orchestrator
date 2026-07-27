# Tasks — automation-editing

## 1. The slices

- [x] 1.1 Extract the overlap check so create, edit and enable share it, with an
      exclude-this-id parameter (design D1).
- [x] 1.2 `PUT .../automations/{id}` (validated as create, D4's response), and
      `POST .../{id}/enable` / `/disable` (D2).

## 2. Tests

- [x] 2.1 Functional: edit applies; an overlapping edit is refused and changes nothing; an
      unchanged trigger is not compared to itself; re-enabling into a collision is refused;
      disabling stops matching.
- [x] 2.2 An active Run survives its Automation being edited and disabled (design D3).
      **Found by writing it:** it did not. `IAutomationCatalog.Detail` filtered on `Enabled`, so
      disabling an Automation made an in-flight Run fail with "no longer enabled" — the exact
      opposite of UC-006. `Detail` is now deliberately unfiltered (matching still reads only
      enabled ones through `EnabledAutomations`), and the test pins it.

## 3. Portal

- [x] 3.1 Edit and enable/disable in the Automations section; refusals visible; catalog copy;
      lint + build.

## 4. Close-out

- [x] 4.1 Full suite (CI's own filtered command too); CI green.
