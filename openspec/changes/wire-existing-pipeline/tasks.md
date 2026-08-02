## 1. Discovery

- [ ] 1.1 A query that lists the candidate locations (design D2) through the seam's directory
      read (#215) and returns, per candidate, the prompt files it holds
- [ ] 1.2 One subdirectory deep, no further; the Connector's configured directory ranked first
- [ ] 1.3 Functional tests: a repository with `.claude/commands/ds`, one with `ai/prompts`, one
      with both, and one with neither

## 2. Adoption

- [ ] 2.1 Map a file name to a pipeline step; an unrecognised name produces no Automation
      (design D3)
- [ ] 2.2 Extend the setup command: take the confirmed directory, wire the matched steps through
      the existing creation path, report the rest
- [ ] 2.3 Keep the convergence rule — an enabled trigger is skipped and named, never collided with
- [ ] 2.4 Functional tests: adoption wires by name; an unmatched file is reported; an existing
      trigger is skipped

## 3. Filling the gaps

- [ ] 3.1 Install the missing steps' starters in **one** branch and one draft pull request
      (design D4), reusing the publish pipeline rather than adding a second
- [ ] 3.2 Functional test: two gaps produce one pull request, and a step with a file produces none

## 4. The surface

- [ ] 4.1 Load the `aio-design` skill; the action becomes propose → confirm → report
- [ ] 4.2 The report renders all five facts (design D5), with the pull request link where there is
      one
- [ ] 4.3 i18n entries; no hardcoded copy

## 5. Proof

- [ ] 5.1 Browser-preview verification of both shapes: a repository whose pipeline is adopted, and
      an empty one whose gaps are filled
- [ ] 5.2 Full gates — build, tests, lint, spec validation, design validator — plus a grep of the
      e2e tier for any name this change moves
