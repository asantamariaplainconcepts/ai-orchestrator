## 1. The credential seam (D1) — nothing else compiles without it

- [ ] 1.1 Add `CredentialReference` and `CredentialSource` to `AiOrchestrator.BuildingBlocks/Secrets/`:
      a reference is either a named secret or the host path; a source names the secret, or the host's
      credential helper and the host it was asked about.
- [ ] 1.2 Add `IConnectorCredentialResolver` to the same folder — resolves a `CredentialReference` to
      a value **and** its `CredentialSource`, per read, never caching.
- [ ] 1.3 Add `IHostCredentialResolver` (contract only) — resolves by vendor host, not by name.
- [ ] 1.4 Unit-test the reference type: a reference cannot be both a name and the host path, and an
      absent reference is not silently the host path.

## 2. The host credential resolver (D2, D3)

- [ ] 2.1 Implement `GitCredentialHelperResolver` in `Infrastructure/Secrets/`: shell
      `git credential fill` with `GIT_TERMINAL_PROMPT=0`, cleared `GIT_ASKPASS`/`SSH_ASKPASS`, and a
      bounded timeout.
- [ ] 2.2 Map both vendors to their credential host (`github.com`, `dev.azure.com`) — one table, no
      vendor-specific auth mode, because a mode available to one vendor and not the other is
      forbidden by `connector-seam`.
- [ ] 2.3 Return the helper's `password` as the token and carry its `username` into the
      `CredentialSource` only (D3).
- [ ] 2.4 Fail with a stated reason — never an empty or default credential — when the helper exits
      non-zero, times out, or would prompt.
- [ ] 2.5 Compose it in the host composition root beside `SecretResolution`, self-host posture only;
      a governed deployment composes no host resolver at all.
- [ ] 2.6 Tests: a helper that answers resolves; a helper that would prompt fails with its reason and
      does not wait; a non-zero exit never yields an empty credential.

## 3. Remote parsing — the folder names the vendor

- [ ] 3.1 Implement `GitRemoteCoordinates` in `Infrastructure/`: parse `origin` for GitHub and Azure
      DevOps, SSH and HTTPS, including the `{org}.visualstudio.com` form.
- [ ] 3.2 Map Azure DevOps to Owner `{org}`, Repository `{project}`, Code repository `{repo}` — the
      three fields `AzureDevOpsBacklogConnector` actually reads.
- [ ] 3.3 Return a named failure for each of the four checks (not a directory, not a repository, no
      `origin`, neither vendor) rather than a generic error.
- [ ] 3.4 Unit tests: both vendors × both forms yield identical coordinates; each of the four
      failures is named.

## 4. The Connector accepts no credential (spec delta: `connector-configuration`)

- [ ] 4.1 Add the credential-source column to the Connector, nullable, and its EF migration; existing
      rows read back as the named-secret source.
- [ ] 4.2 Teach `ConfigureConnector` the third path: neither token nor secret name, self-host posture
      only, storing nothing in the secret store.
- [ ] 4.3 Route every existing resolution call site through `IConnectorCredentialResolver` (the poller,
      story read/write, label write, comment write, document read) — no call site resolves by name
      directly.
- [ ] 4.4 Verification uses the resolved credential against the derived coordinates; an unanswerable
      write capability stays *not verifiable* carrying its reason, and saving proceeds.
- [ ] 4.5 Record the `CredentialSource` on the Run's audit record (BR-014).
- [ ] 4.6 Functional tests for every scenario in the `connector-configuration` delta, including the
      host-path save, the rejected host credential, and the vendor switch on the host path.

## 5. A Project is added by naming a folder (spec: `local-folder-project`)

- [ ] 5.1 Add `IConnectorWriter` to `Backlog.Contracts` and implement it inside Backlog.
- [ ] 5.2 Extend `CreateProject` with the optional folder, ordered **inspect → derive → verify live →
      create Project → write Connector**, compensating by removing the Project if the Connector write
      fails (D4).
- [ ] 5.3 Compose the folder input server-side in the self-host posture only; refuse a folder sent to
      a governed deployment rather than ignoring it.
- [ ] 5.4 Honour the declaring-habitat refusal verbatim (`local-code-source` delta).
- [ ] 5.5 Functional tests: a named folder yields a configured Project; each of the four failures
      leaves the coordinates empty and the flow open; a cloud deployment refuses the folder.

## 6. The portal

- [x] 6.1 Add the folder input to the add-Project form, gated on the deployment capabilities read
      (never a client-derived posture), with its explanation beside it.
- [x] 6.2 Show the derived coordinates as editable values, and the named failure where derivation
      failed.
- [x] 6.3 State the permission requirement honestly on the host path — what this configuration
      requires, not what the credential holds (D6).
- [x] 6.4 Every string through the typed i18n catalogue; kit primitives and Platform tokens only.
- [x] 6.5 Frontend tests for the posture gating and the four named failures.

## 7. Verification — the CI-equivalent gates

- [x] 7.1 `dotnet build` clean, and CSharpier formatting applied.
- [x] 7.2 `dotnet test` — unit, functional and arch tests green.
- [x] 7.3 `pnpm lint --max-warnings=0`, `pnpm tsc --noEmit`, Prettier check.
- [x] 7.4 `pnpm build` — the production bundle, because the E2E suite serves the built bundle.
- [x] 7.5 `openspec validate --strict` for this change.
