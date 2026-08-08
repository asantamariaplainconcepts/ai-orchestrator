## 0. Preconditions (ADR-0017)

- [x] 0.1 A subscription where `Microsoft.App/sandboxGroups` can be **created**, not merely read.
      The spike burned three wrong ones establishing that signing in is not authority and that a
      role name is not its scope: `Reader` at subscription scope, a subscription that was
      `Disabled` outright, and a `Contributor` that turned out to be scoped to one client web app.
      Name the subscription and confirm `az group create` succeeds **before** anything else.
      **Answered by the spike: `Azure subscription 1` (`422bb77e-…`)** — `*` permissions,
      `Microsoft.App` registered, `sandboxGroups` offered in Spain Central among others, and a
      resource group created and deleted there twice.
- [ ] 0.2 A fine-grained `github_pat_…` and an `sk-ant-…`, both human-minted — an agent cannot
      create either. Needed for the typed credential providers in group 4.
- [ ] 0.3 Record the preview version and region every later observation is true of (ADR-0018).

## 1. The host (design D1, D2)

- [x] 1.1 `AcaAgentProcessHost` implements `IAgentProcessHost`, selected by
      `Agents:Sandbox:Launcher = aca` exactly as sbx is — configuration presence, never inference
      (ADR-0010). A habitat naming two substrates stays refused.
- [x] 1.2 `Run()` starts the agent **detached** inside the sandbox and polls with short `exec`
      calls, forwarding new output through `onOutput` as it arrives. It blocks until the agent
      finishes. **The ~50 s ceiling never reaches the executor** — a test proves a Run longer than
      the ceiling completes, which is the assertion that would fail if the loop were removed.
- [x] 1.3 BR-005's timeout kills the phase and BR-004 still never retries. The sandbox is disposed
      in a `finally` that survives cancellation, like sbx's.
- [x] 1.4 Choose and defend the poll interval (design's open question): live enough for UC-027,
      sparse enough not to spend an `exec` per second across thirty minutes.

## 2. What the habitat declares (design D3)

- [x] 2.1 The launcher disables auto-suspend on every sandbox it creates. A test pins it, because
      the platform default (600 s, measuring idleness from outside) suspends a thinking agent —
      observed at t+41 s with a 60 s timeout while a process wrote inside every second.
- [x] 2.2 Egress is declared deny-default with an allow list, and composition **refuses** a habitat
      that names this launcher without one. Measured: a sandbox with no policy reached
      `example.com` and `pypi.org` with 200s, whatever the documentation says.
- [ ] 2.3 A denied request is recordable, so a habitat can show what its agents reached for.

## 3. The workspace (design D2, and the spike's H2)

- [x] 3.1 The Run's workspace reaches the sandbox without the executor sharing its machine.
- [ ] 3.2 A functional test pins that no host-level grant is required on the executing machine —
      the property the pod path could not offer and the reason this change exists.

## 4. Credentials (design D4)

- [ ] 4.1 One SandboxGroup per Project, its typed credentials its own, so #244's per-project
      billing identity survives.
- [ ] 4.2 No credential value is readable inside the sandbox. Prove the negative the way #288's
      tests do: run something inside that prints the variable and assert it is empty.
- [ ] 4.3 The transcript names the injection as the credential source, beside the two sources it
      already names.
- [ ] 4.4 Provisioning tolerates role propagation — the spike saw 403s for about a minute after
      granting `Container Apps SandboxGroup Data Owner`.

## 5. Previews (design D5)

- [x] 5.1 The port is created **Entra-gated**, never `--anonymous`, and the portal relays it as it
      does today. run-previews' contract is unchanged: reachable while the Run lives, nothing
      afterwards — not a stale route, not the option.

## 6. Retiring the pod path (design D6)

- [ ] 6.1 Remove `Dispatch:PodImage`, its launcher, the docker-socket grant and the compose warning
      that went with it.
- [ ] 6.2 A habitat naming a pod image is refused at composition, naming what replaced it.
- [ ] 6.3 Confirm in-process execution is untouched — a machine somebody owns keeps its answer.

## 7. Proof

- [ ] 7.1 Unit and functional coverage with fakes that can fail: the ceiling absorbed, the
      declarations refused when missing, the empty credential, the per-project group.
- [ ] 7.2 **Exercised against real Azure**, gated like `RealSbxSandbox_Should_Constraint` so CI
      never runs it: a Run end to end on the substrate, longer than the `exec` ceiling, with its
      output observed arriving while it worked, its preview relayed and then gone, and no sandbox
      surviving. Recorded verbatim, including anything that did not work.
- [ ] 7.3 Full gates, and confirm the dev loop and the local lane behave exactly as before.
- [ ] 7.4 Delete everything the proof created and say what it cost — the number the issue accepted
      without measuring is worth capturing the moment it becomes knowable.
