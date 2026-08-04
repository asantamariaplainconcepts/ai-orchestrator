# Self-host (docker compose)

Run the whole product on a machine you own (DEC-049):

```bash
POSTGRES_PASSWORD=<pick-one> SERVER_PORT=8080 docker compose up
```

`docker-compose.yaml` is **generated** from the AppHost (`./scripts/generate-compose.sh`); do not
hand-edit it — CI's drift gate compares the two.

## What this habitat can and cannot do

- **Identity**: the operator who ran `docker compose up` is the owner (`LocalOwner`) — every
  action is administrator, no sign-in. Trusted networks only.
- **Runs** execute against a **repository** code source: the pod clones with the Connector's
  credential, exactly as the cloud habitat does.
- **Local folders are not available here** (#247). The Server runs in a container, and a folder
  on this machine is not visible to it. The compose declares this
  (`Habitat__LocalFolderUnavailableReason` on the `server` service), the portal withholds the
  option with that sentence, and the API refuses a `LocalFolder` save or a Local-locus Run with
  the same sentence. Local folders belong to the dev loop (`aspire run`), where the server is a
  process on this machine.
  - If you deliberately mount a folder into the container and remove the declaration from your
    own compose, the refusals follow the declaration and stop — and the consequence is yours:
    two processes over one working copy is the hazard the declaration exists to prevent.
