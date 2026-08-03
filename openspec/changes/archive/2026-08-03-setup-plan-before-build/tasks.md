# Tasks: setup-plan-before-build

## 1. The plan (design D1, D2)

- [x] 1.1 `DiscoverPipeline.Candidate` gains `Plan` — trigger, prompt file, exists, gated,
      installable — computed from the listing already read.
- [x] 1.2 Rows where a starter cannot be installed and no file exists are filtered; the
      `Installable` flag is carried rather than collapsed.

## 2. The surface (design D3)

- [x] 2.1 Render the plan above the button, one row per step, with the exists/installs badge.
- [x] 2.2 Name the gate on the step that carries it.
- [x] 2.3 Collapse after three rows, expandable.
- [x] 2.4 Delete the install-missing checkbox; the build always installs what the plan showed.
- [x] 2.5 Move the draft-pull-request sentence beside the button.

## 3. Tests

- [x] 3.1 Functional: the plan names the file each step wires, marks an existing file as present and
      a missing one as a starter to install, and writes nothing while doing it.
- [x] 3.2 Functional: the plan names the step that waits for a person.
- [x] 3.3 E2E: the checkbox is gone.
- [x] 3.4 **Not covered by E2E, and said rather than faked** (design D4): the plan rows and the
      safety sentence need a Connector serving directory listings, which this tier's GitHub stub
      cannot do. A first draft asserted the sentence anyway and failed — correctly — because the
      state is unreachable. Deleted rather than weakened.
- [x] 3.5 Mutation check, build verified first: reporting the starter name for a file that exists,
      and reporting every step as ungated, each redden their own test.

## 4. Gates

- [x] 4.1 `tsc`, eslint, prettier, design-system validator, 442 non-E2E, 39 E2E.
- [x] 4.2 `docker build` of the portal image.
- [x] 4.3 The frontend build was run through `rtk proxy` and the new copy grepped out of the bundle
      before trusting any test result — `rtk` masks a failed build, which invalidated a mutation
      check earlier in this session.
