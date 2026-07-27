# Tasks — automation-editing

## 1. The slices

- [ ] 1.1 Extract the overlap check so create, edit and enable share it, with an
      exclude-this-id parameter (design D1).
- [ ] 1.2 `PUT .../automations/{id}` (validated as create, D4's response), and
      `POST .../{id}/enable` / `/disable` (D2).

## 2. Tests

- [ ] 2.1 Functional: edit applies; an overlapping edit is refused and changes nothing; an
      unchanged trigger is not compared to itself; re-enabling into a collision is refused;
      disabling stops matching.
- [ ] 2.2 An active Run survives its Automation being edited and disabled (design D3).

## 3. Portal

- [ ] 3.1 Edit and enable/disable in the Automations section; refusals visible; catalog copy;
      lint + build.

## 4. Close-out

- [ ] 4.1 Full suite (CI's own filtered command too); CI green.
