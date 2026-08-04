## 1. The evidence gate (design D1)

- [x] 1.1 Exercise `aspire publish` on 13.4.x with an SDK-container-published project and the
      JS resource's publish path for real; record what exists and what doesn't in the change.
      **Recorded:** no Aspire upgrade needed, and no JS publish API either — CI runs
      `pnpm build` (wwwroot) before `dotnet publish /t:PublishContainer`, publish includes
      wwwroot, and the Vite resource was already run-mode-only. `PublishWithContainerFiles`
      exists in Aspire.Hosting 13.4.6 but is unnecessary here. `/t:PublishContainer` produced
      all three images locally on the first try.

## 2. The launcher (design D3)

- [x] 2.1 Pod launcher speaks `Docker.DotNet` against the mounted socket; the docker CLI binary
      and its Dockerfile COPY disappear from the story. The absent-grant failure names the
      socket and its endpoint (#246's named-failure stance; the CLI left the sentence because it
      left the story); DispatchTests stay green (30/30). The probe's two CLI calls collapsed
      into one InspectImage whose not-found answer itself proves the daemon reachable.

## 3. The images (design D2, D4)

- [x] 3.1 csproj container-publish properties for server, migrations, dispatch worker — the two
      workers pin an EXPLICIT aspnet base (the twice-made runtime-image mistake, made
      unmakeable); non-root `app` user; the SPA rides in via wwwroot inclusion.
- [x] 3.2 The three self-host Dockerfiles deleted; `publish-images.yml` pushes the three images
      to GHCR on main, SHA + `latest`. **Found in the retired-names grep:** a fourth Dockerfile
      (ConversationSession, #166) exists and stays — it bakes agent CLIs via RUN steps the SDK
      cannot express and belongs to the Azure path, which this change excludes; the spec delta
      and the file itself now say so.
- [x] 3.3 AppHost publish declarations reference `ghcr.io/...:${AIO_IMAGE_TAG:-latest}` through
      a spelled-once helper (no new required .env variable; SHA pinning available); compose
      regenerated; drift gate clean and deterministic.

## 4. The operator contract (design D5)

- [x] 4.1 `selfhost/README.md` + `SELF-HOSTING.md` quickstart: pull, not build;
      `Dispatch__PodImage` default is the published worker image (plain tag — the same
      declaration serves the aspire-run rehearsal, where nothing interpolates compose
      placeholders); the pods panel's image-missing remedy is now one `docker pull`;
      `selfhost/.env.example` now exists — the quickstart named it and the proof runs the
      quickstart verbatim.

## 5. Proof (design D6)

- [x] 5.1 Real boot, documented quickstart verbatim (cp .env.example → docker compose up),
      fresh volumes, SDK-built images under their GHCR names: postgres healthy → migrations
      exit 0 → server HTTP 200 serving the SPA titled "AI Orchestrator", running as `app`.
      Pod transport proven against the real socket and the real worker image: created, started,
      waited, removed, no leftover container; the failure path carries the pod's stderr.
      **Honest limit:** the images were local-tagged — pull-from-GHCR is only exercisable after
      the first merge publishes them; the deploy watch at sync covers it. **Observed:** the SDK
      image (like the current aspnet:10.0 base) carries no libgssapi — benign, scram-sha-256
      auth is the product's path and migrations proved it against a real postgres.
- [x] 5.2 Full gates — Release build 0 errors, tests (see PR checks; suite includes the E2E
      production path), CSharpier, Prettier/tsc, design-system, `openspec validate`,
      compose-drift clean — plus the retired-names grep: zero `ProcessStartInfo("docker")`, the
      old build commands gone from docs and panel copy, and the one surviving Dockerfile is the
      documented Azure-path exception above.
