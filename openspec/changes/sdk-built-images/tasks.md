## 1. The evidence gate (design D1)

- [ ] 1.1 Exercise `aspire publish` on 13.4.x with an SDK-container-published project and the
      JS resource's publish path for real; record what exists and what doesn't in the change.
      **If the needed APIs are missing: stop, file the Aspire-upgrade item, park this change.**

## 2. The launcher (design D3)

- [ ] 2.1 Pod launcher speaks `Docker.DotNet` against the mounted socket; the docker CLI binary
      and its Dockerfile COPY disappear from the story. The absent-grant failure keeps its exact
      named message (#246); DispatchTests stay green.

## 3. The images (design D2, D4)

- [ ] 3.1 csproj container-publish properties for server, migrations, dispatch worker (base
      inferred from framework reference, non-root); the SPA rides into the server image via the
      JS publish path, `wwwroot` served as today.
- [ ] 3.2 Delete the three Dockerfiles; CI `publish-images` job pushes the three images to GHCR
      on main, tagged by commit SHA.
- [ ] 3.3 AppHost publish declarations reference images by tag (overridable in `.env`);
      regenerate the compose; drift gate deterministic.

## 4. The operator contract (design D5)

- [ ] 4.1 `selfhost/README.md` + `SELF-HOSTING.md` quickstart: pull, not build;
      `Dispatch__PodImage` default becomes the published image, still overridable.

## 5. Proof (design D6)

- [ ] 5.1 Real boot, documented quickstart verbatim: fresh volumes, published images pulled by
      tag, postgres healthy → migrations exit 0 → server HTTP 200 serving the SPA — plus one
      dispatched Run with the socket granted (the launcher's new transport, proven by a pod).
- [ ] 5.2 Full gates — build, tests (incl. E2E production path), lint, spec validation,
      compose-drift — plus a grep for every name this change retires (Dockerfile paths, the
      CLI COPY, `docker` CLI invocations in the launcher).
