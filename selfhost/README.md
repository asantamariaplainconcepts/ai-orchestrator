# Self-host (docker compose)

Run the whole product on a machine you own (DEC-049):

```bash
POSTGRES_PASSWORD=<pick-one> SERVER_PORT=8080 docker compose up
```

Nothing builds locally (#257): every image is **pulled** from GHCR, published there by CI on
each merge to `main` and tagged with the commit SHA plus a moving `latest`. The compose defaults
to `latest`; pin a specific build by setting `AIO_IMAGE_TAG=<commit-sha>` in your environment or
`.env`. The only registries contacted are public ones (GHCR and Docker Hub for postgres and the
dashboard).

`docker-compose.yaml` is **generated** from the AppHost (`./scripts/generate-compose.sh`); do not
hand-edit it — CI's drift gate compares the two. No Dockerfile backs any image this compose runs:
they are produced by the .NET SDK's container publish (`dotnet publish /t:PublishContainer`), with
the SPA built into the server's `wwwroot` first (`pnpm build`).

## What this habitat can and cannot do

- **Identity**: the operator who ran `docker compose up` is the owner (`LocalOwner`) — every
  action is administrator, no sign-in. Trusted networks only.
- **Runs execute in a microVM, one per Run** (#296). Each dispatched Run gets its own sandbox
  with its own kernel, started for that Run and gone with it. This replaced a substrate that ran
  each Run in a container launched over the **docker socket** — a grant that is root-equivalent on
  the machine. **Nothing here needs that socket any more.** If you wrote a
  `docker-compose.override.yaml` to mount it, delete that mount.

  One thing is yours to provide:

  1. **Docker Sandboxes (`sbx`) on this machine.** Install it and make sure its daemon is running.
     The compose names the launcher (`Agents__Sandbox__Launcher: sbx`); nothing installs it for
     you. Until it is there, every Run fails naming exactly that — a named failure, never a
     silent fallback to running the agent unsandboxed.

     > **Check your machine first.** On Linux `sbx` requires **x86_64 with KVM** (Ubuntu 22.04 or
     > newer, per Docker's own requirements). On Apple silicon it uses Hypervisor.framework. A
     > machine without KVM cannot run this habitat, and there is no fallback that keeps the
     > isolation — so confirm it before planning a deployment around it.
     >
     > **Honestly bounded:** every measurement behind `sbx` in this repository was taken on
     > macOS. The Linux leg is documented by Docker and has not been exercised here. If you are
     > the first to run this on Linux and it does not behave as described, that is a finding worth
     > an issue rather than a workaround.

  - **Your CLI sessions do not enter these sandboxes by default.** A sandboxed Run authenticates
    with the credentials this deployment stores, not with your own logins.
  - Runs against a **repository** code source clone with the Connector's credential inside the
    sandbox, exactly as the cloud habitat does.
- **Local folders are not available here** (#247). The Server runs in a container, and a folder
  on this machine is not visible to it. The compose declares this
  (`Habitat__LocalFolderUnavailableReason` on the `server` service), the portal withholds the
  option with that sentence, and the API refuses a `LocalFolder` save or a Local-locus Run with
  the same sentence. Local folders belong to the dev loop (`aspire run`), where the server is a
  process on this machine.
  - If you deliberately mount a folder into the container and remove the declaration from your
    own compose, the refusals follow the declaration and stop — and the consequence is yours:
    two processes over one working copy is the hazard the declaration exists to prevent.
