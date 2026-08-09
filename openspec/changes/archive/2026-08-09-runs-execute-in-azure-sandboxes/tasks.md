## 0. Preconditions (ADR-0017)

- [x] 0.1 A subscription where `Microsoft.App/sandboxGroups` can be **created**, not merely read.
      The spike burned three wrong ones establishing that signing in is not authority and that a
      role name is not its scope: `Reader` at subscription scope, a subscription that was
      `Disabled` outright, and a `Contributor` that turned out to be scoped to one client web app.
      Name the subscription and confirm `az group create` succeeds **before** anything else.
      **Answered by the spike: `Azure subscription 1` (`422bb77e-…`)** — `*` permissions,
      `Microsoft.App` registered, `sandboxGroups` offered in Spain Central among others, and a
      resource group created and deleted there twice.
- [x] 0.2 A fine-grained `github_pat_…` and an `sk-ant-…`, both human-minted — an agent cannot
      create either. **The `sk-ant-` will not exist**: the Anthropic account is the organisation's,
      not the developer's, so the `claude` disk is unavailable to this deployment. A
      `github-copilot` credential was created instead, its value entered through the CLI's hidden
      prompt. It is enough for 4.2 and 4.3; it is **not** enough for a real agent Run, which needs
      the `Copilot Requests` permission the repository-scoped PAT does not carry.
- [x] 0.3 Record the preview version and region every later observation is true of (ADR-0018).
      **`aca 1.0.0-preview.1`, spaincentral, subscription `422bb77e-…`, 2026-08-09** — written at
      the head of `evidence.md`.

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
- [x] 2.3 A denied request is recordable, so a habitat can show what its agents reached for.
      The platform keeps an auditable decision log per sandbox, which means it must be asked for
      **before** the sandbox is deleted — a test pins that ordering, and a second pins that an
      unreadable log says so in the output rather than failing a Run whose work already finished.

## 3. The workspace (design D2, and the spike's H2)

- [x] 3.1 The Run's workspace reaches the sandbox without the executor sharing its machine.
- [x] 3.2 A functional test pins that no host-level grant is required on the executing machine —
      the property the pod path could not offer and the reason this change exists. **Exercised
      against real Azure**: a file written on this Mac was read back inside a remotely created
      microVM. No mount, no socket, no grant.

## 4. Credentials (design D4)

- [x] 4.1 One SandboxGroup per Project, its typed credentials its own, so #244's per-project
      billing identity survives. **A gap in the design, found on contact:** it said "per Project"
      while `IAgentProcessHost.Run` has no Project — it takes a command, a workspace and a timeout.
      Closed by widening the seam with an optional `projectId` that every other host ignores, and
      templating the group name so `{project}` in one setting describes a deployment whose groups
      are per Project. The id travels on `AgentInstruction`, beside the model #291 put there.
- [x] 4.2 No credential value is readable inside the sandbox. **Exercised against Azure**: a
      sandbox created with the credential attached, asked from inside for its whole environment
      and every file under `$HOME`, `/etc` and `/tmp` containing `github_pat_` — nothing. The
      test never learns the token; it looks for the shape, so the secret stays out of the
      repository and the assertion can still fail.
      **A fifth defect found on the way:** `create` never passed `--credential` at all, so every
      sandbox this host made had no credential and no agent could have authenticated. Design D4
      promised it and the code did not ask.
- [x] 4.3 The transcript names the injection as the credential source, beside the two it already
      names. Asserted on the runtime the selector hands back rather than on the host — 6.1b was a
      wire nobody had connected between exactly those two.
- [x] 4.4 Provisioning tolerates role propagation. **Not reproduced on 2026-08-09** — the CLI now
      grants the data role itself at `sandboxgroup create` and every data-plane call worked at
      once — so this is implemented from the spike's measurement rather than from a failure seen
      today, and that is stated rather than dressed up. A sandbox creation refused for
      authorization is retried six times at ten seconds, covering the ~1 minute the spike watched;
      a grant that never arrives still fails, one minute later, **naming the role to add and to
      which identity**. Anything that is not an authorization refusal fails at once, because
      retrying a bad disk name only delays the sentence an operator needs. Three tests, verified
      able to fail by disabling the loop.

## 5. Previews (design D5)

- [x] 5.1 The port is created **Entra-gated**, never `--anonymous`, and the portal relays it as it
      does today. run-previews' contract is unchanged: reachable while the Run lives, nothing
      afterwards — not a stale route, not the option.

## 6. Retiring the pod path (design D6)

- [x] 6.1 Remove `Dispatch:PodImage`, its launcher, the docker-socket grant and the compose warning
      that went with it.
- [x] 6.2 A habitat naming a pod image is refused at composition, naming what replaced it.
- [x] 6.3 **selfhost adopts sbx**, because retiring the pod would otherwise leave it with nothing:
      the Server image carries no agent CLI on purpose, so in-process is not an option there.
      `DeclareServerShape` names the sbx launcher where it named a pod image.
- [x] 6.4 **Record the selfhost leg as NOT VERIFIED, loudly.** Every measurement behind sbx is
      macOS; the spike's own findings name Linux x86_64 + KVM as the prerequisite and call the
      selfhost leg an afternoon's work on a Linux VM that has not happened. Shipping this makes
      that hypothesis load-bearing for a habitat nobody has run it in (ADR-0005). The refusal a
      selfhost operator meets if KVM is absent must name it, and `selfhost/README.md` must say what
      the machine needs before the operator finds out from a failed Run. **Done**: recorded in the
      habitat declaration, in design D6, and in `selfhost/README.md`, which now says x86_64 + KVM
      is required, that the socket grant should be deleted, and — plainly — that every measurement
      behind sbx here was taken on macOS and the Linux leg has not been exercised.
- [x] 6.5 Confirm in-process execution still works for a habitat that names no launcher at all —
      it stops being selfhost's answer, but it is still the default and the local lane's.

## 6b. A defect found while wiring this, already on `main`

- [x] 6.1b **`instruction.Preview` was never forwarded to the host by either runtime**, so no Run
      has ever published a preview port — the executor built the instruction with its preview and
      the chain dropped it. run-previews' own gated test called the host **directly**, proving the
      host publishes a port and never that a Run reaches it with one: a component test cannot see a
      wire that was never connected. Fixed in both runtimes, with a test asserting on what a
      runtime hands its host, verified able to fail by cutting the wire again.

## 7. Proof

- [x] 7.1 Unit and functional coverage with fakes that can fail: the ceiling absorbed, the
      declarations refused when missing, the empty credential, the per-project group.
      **Done**: the `aca` CLI is stood in for by a script that records every invocation, so "did
      not disable auto-suspend" is an assertion rather than an assumption, and the stand-in only
      lets the agent finish after several polls — an implementation that ran one exec and returned
      would never see it. Each behaviour verified able to fail by removing it: dropping the
      auto-suspend call reddens one test, dropping disposal reddens two.
- [x] 7.2 **Exercised against real Azure**, gated like `RealSbxSandbox_Should_Constraint` so CI
      never runs it. `RealAcaSandbox_Should_Constraint`, four tests, all green — and they found
      **four defects a green unit suite could not**: `fs cp` takes no `--id`; the platform has no
      recursive copy at all, so the workspace had to become tar → copy → untar; the last lines of
      every Run were dropped by the poll loop's hold-back; and the egress decision log is JSON,
      so the line-based reader reported nothing exactly when it mattered. Verbatim in
      `evidence.md`, with what held and what is still unverified.
      **A real agent Run is covered too**, on the second attempt at a token: `Copilot Requests`
      exists only on a personal-account PAT (github/copilot-cli#223), and with one the agent
      authenticates, answers and bills through the shipped host — three runs, three passes.
      **Which surfaces a design consequence rather than closing one:** a personal token bills the
      model to that person's seat, not to the Project, and that is what #244 forbids and what
      D4's per-Project group exists to guarantee. The platform's two typed providers are an
      Anthropic key the organisation does not hand out and a Copilot token that must be personal,
      so **the credential model this change designed cannot be satisfied by either today**. Named
      in `evidence.md` and belonging to the follow-up.
- [x] 7.3 Full gates — **587 tests green** across all eight suites, E2E included. Two things the
      run itself surfaced and neither was code: the E2E suite serves the built bundle, so a
      wiped `wwwroot` reds it until `pnpm build`; and Playwright's browsers live in
      `~/Library/Caches`, so a disk cleanup takes them with it.
- [x] 7.4 Everything created was deleted — **0 sandboxes** after four Runs, then the group and the
      resource group. The billed surface was five short-lived 1 vCPU / 2 GiB microVMs under ten
      minutes total. **No figure**: Azure's cost data lags by hours, so a number read now would be
      zero for the wrong reason, and the honest answer is the shape of the usage (ADR-0018).

## 8. One dispatch substrate (design D7 — scope widened by the owner, 2026-08-09)

- [x] 8.1 Remove the queue substrate: `QueueRunDispatcher`, `DispatchQueueReader`,
      `AddRunDispatchReader`, and the branches in `DispatchComposition` that chose by queue
      presence. The outbox path — already what the dev loop, selfhost and the functional tests
      run — becomes the only one.
- [x] 8.2 A habitat still naming the queue connection string is refused at composition, naming
      the outbox as its replacement, exactly as the retired pod image is.
- [x] 8.3 Retire the `DispatchWorker` project: the csproj, the slnx entry, and its image in
      `publish-images.yml`. Its job — consume, claim, execute — is the Server's
      `OutboxRunSubscriber`, which already exists and already does it.
- [x] 8.4 The functional-test fixtures drop Azurite and the queue, and pin the new race instead:
      the outbox consumer must not auto-execute Runs the tests drive by hand.
- [x] 8.5 Terraform drops the queue, its role assignments, the scaler's vault secret and the KEDA
      job — **but not the storage account and not the identity**, which the sweep found have
      second jobs the plan missed: the account hosts the portal's Data Protection key ring
      (#180), and the identity is deliberately reused by conversation sessions so the portal
      never gains the ability to read a project credential. Both stay, with their comments
      rewritten to say what they are now for. The deploy is red for an unrelated reason; these
      leave as text.
- [x] 8.6 DEC-013 is marked superseded in the corpus, naming this change and the reasons its
      three motivations evaporated.
- [x] 8.7 What is given up is recorded where it is given up: no horizontal scale-out of
      execution, no separate execution identity. Either can return by placing a consumer
      elsewhere; the seam does not close.

## 9. No pod survives its substrate (owner's sweep, 2026-08-09)

Asked for directly: *"no quiero código comentado en mi repo … prefiero eliminar todo el código
al máximo"*. Retiring a substrate and leaving its vocabulary behind is the same defect as leaving
its code — a reader cannot tell which of the two the product still means.

- [x] 9.1 The frontend's `features/pods/` becomes `features/runtimes/`: `usePods` → `useRuntimes`,
      `PodsScreen` → `RuntimesScreen`, the query key, the route `/pods` → `/runtimes`, and every
      `pods.*` copy key.
- [x] 9.2 The endpoint follows: `GetAgentPods` → `GetAgentRuntimes`, `/api/pods` →
      `/api/runtimes`. It has answered for runtimes alone since the pod half retired.
- [x] 9.3 **The Run locus value `Pod` becomes `Sandbox`, with the migration that makes it safe.**
      Found by the sweep and bigger than a rename: the value is persisted as a string, so EF's
      model diff sees only a moving column default while every existing row still reads `Pod` and
      the next `Enum.Parse` on it throws. The migration carries the `UPDATE`; the scaffolded
      `AlterColumn` is the footnote. DEC-005 has said *Agent — never "pod"* since the beginning,
      and the interface said "Agent pod" anyway.
- [x] 9.4 Copy a Member reads: "In a sandbox", "Run in a sandbox", "an Agent in a sandbox cannot
      see this machine's disk". The user manual and the domain glossary follow — the glossary's
      Agent entry still described DEC-013's KEDA-scaled job.
- [x] 9.5 The duplicate pod-image refusal in `AgentSandboxComposition` is deleted. It said "you
      named both", which stopped being constructible: there is no second substrate to layer, and
      a refusal that cannot fire teaches its reader the two still coexist. The retirement refusal
      in `DispatchComposition` is the one that survives, and it names the launchers.
- [x] 9.6 Stale cross-references to deleted types (`AgentPodsHost`, `PodRunLauncher`, "the pods
      probe's sibling") and present-tense claims about the pod as a live option are gone from the
      comments.
- [x] 9.7 Spec deltas for what the sweep changed: `agent-execution` (the locus value, plus the
      scenario that a row written before the rename still loads), `frontend-architecture` (the
      dialog's card and the code-source constraint), `dev-orchestration` (the server shape names
      a launcher, not a pod image; and the compose description drops the worker and the queue
      emulator the queue retirement had already deleted), and `agent-sandboxing` REMOVED for the
      layering refusal.

## 10. The documentation says what the product is (owner's question, 2026-08-09)

Asked as a question — *"docs product, readme etc estan modificados?"* — and the honest answer was
"four files, and the corpus is broadly stale". A product brief that still promises a KEDA job is
not a stale comment; it is the product describing itself wrongly to the next reader.

- [x] 10.1 The root `README.md` and `AGENTS.md` no longer open by promising KEDA-scaled ACA Jobs,
      and the dev loop's inventory drops Azurite and the dispatch worker — both of which the
      quickstart claimed `aspire run` starts. What the local loop proves is restated: dispatch is
      now the *same* path everywhere, and what is unexercised locally is the deployed **sandbox**,
      not the scaler.
- [x] 10.2 The product corpus: the brief, the user journey, the bounded contexts, the actor table
      and the glossary's **Dispatch** entry. DEC-002 and DEC-010 are **amended in place with a
      dated note** rather than rewritten — a locked decision is a record — and DEC-054 is marked
      absorbed, because "the dispatch substrate follows the habitat" now has exactly one branch.
- [x] 10.3 `infra/README.md`: the whole Dispatch section is replaced. It documented how to enqueue
      a message by hand against a queue that no longer exists, and told a reader KEDA is the one
      thing only verifiable in Azure. It now says what `dispatch.tf` still holds and why neither
      resident is about dispatch.
- [x] 10.4 **A live defect the doc sweep surfaced, not a doc fix:** `infra/deploy.sh` still
      published `AiOrchestrator.DispatchWorker`, still read `dispatch_job_name` from Terraform
      outputs that this change deleted, and still rolled and verified a job that no longer exists.
      The deploy would have failed at the first `tf` read. Removed, with the #92 lesson its
      comments carry kept — the worker retired, the check it taught did not.

## 11. A microVM per sweep, and 125 GB (owner's report, 2026-08-09)

Reported directly — *"sandboxes ocupa 125 gb hay que poner limite"* — and it turned out to be a
defect of this substrate, found on a machine rather than by a test.

- [x] 11.1 **`sbx ls` showed 31 running sandboxes, 25 of them `aio-probe-*`.** The readiness probe
      creates one every thirty-second sweep and disposes it in a `finally`. The pairing is
      correct; what a `finally` cannot survive is the process not being there to run it. Stop the
      dev loop mid-sweep and the microVM outlives the only reference anyone held to it. A week of
      restarts is a full disk, and no in-process discipline prevents it.
- [x] 11.2 The host **claims its namespace** instead: a fresh process removes whatever still
      carries `aio-probe-*` or `aio-run-*` before creating its first sandbox, once per process.
      Two tests, both verified able to fail: the sweep removes both of its own names and nothing
      that is not its to remove, and it happens once rather than before every Run — a reap per Run
      would remove a Run running beside it. **The constraint that buys is written down:** two
      orchestrators sharing a machine would reap each other, which DEC-016 puts out of scope.
- [x] 11.3 A second, smaller hole closed on the way: `Create` builds a sandbox and can throw
      afterwards, outside every caller's `finally`. It now unwinds what it built. **This was not
      the cause** — an early diagnosis blamed `CarrySession`, which logs and continues rather than
      throwing — and the comment says so rather than taking credit for the fix that mattered.
- [x] 11.4 The 25 orphans removed: **8.9 GiB free → 121 GiB**. The five `spike-*` sandboxes and
      `aio-carry-probe` were left alone — they are a human's, not the product's.

## 12. Somewhere for opencode to run (owner's question, 2026-08-09)

- [x] 12.1 **There is no public opencode disk** — the platform publishes `claude`, `copilot` and
      language runtimes, and both agent disks need a credential this organisation cannot supply
      on #244's terms. That left this product's other runtime, and the free model that makes the
      local loop need no AI credential at all, with nowhere to run.
- [x] 12.2 **A deployment can build its own**: measured, a disk from `node:22-bookworm` was Ready
      in seconds and took `opencode-ai@1.18.6`, which answered on Node 22.23.2 inside a sandbox.
      The conversation session's image already installs exactly that.
- [x] 12.3 **The gap was ours, and it is closed.** `create` takes `--disk` for a public name and
      `--disk-id` for a private one; this host only ever passed the first, so it could use
      Microsoft's disks and none of ours. `Agents:Sandbox:DiskId` now names one, naming both is
      refused, and the shipped host was exercised against a private disk on real Azure.
