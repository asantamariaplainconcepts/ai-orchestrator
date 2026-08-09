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
- [x] 2.3 A denied request is recordable, so a habitat can show what its agents reached for.
      The platform keeps an auditable decision log per sandbox, which means it must be asked for
      **before** the sandbox is deleted — a test pins that ordering, and a second pins that an
      unreadable log says so in the output rather than failing a Run whose work already finished.

## 3. The workspace (design D2, and the spike's H2)

- [x] 3.1 The Run's workspace reaches the sandbox without the executor sharing its machine.
- [ ] 3.2 A functional test pins that no host-level grant is required on the executing machine —
      the property the pod path could not offer and the reason this change exists.

## 4. Credentials (design D4)

- [x] 4.1 One SandboxGroup per Project, its typed credentials its own, so #244's per-project
      billing identity survives. **A gap in the design, found on contact:** it said "per Project"
      while `IAgentProcessHost.Run` has no Project — it takes a command, a workspace and a timeout.
      Closed by widening the seam with an optional `projectId` that every other host ignores, and
      templating the group name so `{project}` in one setting describes a deployment whose groups
      are per Project. The id travels on `AgentInstruction`, beside the model #291 put there.
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
- [ ] 7.2 **Exercised against real Azure**, gated like `RealSbxSandbox_Should_Constraint` so CI
      never runs it: a Run end to end on the substrate, longer than the `exec` ceiling, with its
      output observed arriving while it worked, its preview relayed and then gone, and no sandbox
      surviving. Recorded verbatim, including anything that did not work.
- [ ] 7.3 Full gates, and confirm the dev loop and the local lane behave exactly as before.
- [ ] 7.4 Delete everything the proof created and say what it cost — the number the issue accepted
      without measuring is worth capturing the moment it becomes knowable.

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
