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
- **Runs execute in pods** (#246): each dispatched Run starts its own container from the
  DispatchWorker image and exits with it. Two things are yours to provide, deliberately:

  1. **The image**: `docker pull ghcr.io/asantamariaplainconcepts/ai-orchestrator/dispatch-worker:latest`
     — the compose names that image (`Dispatch__PodImage`); nothing pulls it for you, because a
     pod host that fetches images unasked is a surprise, not a convenience. Override the name in
     your own compose to use a different worker image.
  2. **The docker socket** — your explicit grant, in a `selfhost/docker-compose.override.yaml`
     you write:

     ```yaml
     services:
       server:
         # Root inside the container: holding the socket already IS root on this machine,
         # and the socket's group differs across hosts — this is the honest spelling.
         user: "0"
         volumes:
           - /var/run/docker.sock:/var/run/docker.sock
         environment:
           Dispatch__PodSessionsHome: "/home/<you>"
     ```

     **The socket is root-equivalent on this machine.** Whoever can reach it can do anything
     your docker daemon can. That is why the generated compose never mounts it: until you do,
     every Run fails naming exactly this grant — a named failure, never a silent fallback.

  - **Your CLI sessions enter the pods by default**: `~/.config/opencode` and `~/.claude` are
    mounted read-only from `Dispatch__PodSessionsHome`. Pod Runs act and bill as those
    sessions. Turn it off with `Dispatch__PodSessions: "false"`.
  - At most **2 pods** run concurrently (`Dispatch__MaxConcurrentPods`); a Run past the cap
    waits — delayed, never dropped.
  - Runs against a **repository** code source clone with the Connector's credential inside the
    pod, exactly as the cloud habitat does.
- **Local folders are not available here** (#247). The Server runs in a container, and a folder
  on this machine is not visible to it. The compose declares this
  (`Habitat__LocalFolderUnavailableReason` on the `server` service), the portal withholds the
  option with that sentence, and the API refuses a `LocalFolder` save or a Local-locus Run with
  the same sentence. Local folders belong to the dev loop (`aspire run`), where the server is a
  process on this machine.
  - If you deliberately mount a folder into the container and remove the declaration from your
    own compose, the refusals follow the declaration and stop — and the consequence is yours:
    two processes over one working copy is the hazard the declaration exists to prevent.
