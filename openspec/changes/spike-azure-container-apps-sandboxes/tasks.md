## 0. Preconditions, named before starting (ADR-0017)

- [ ] 0.1 Confirm an Azure subscription where `Microsoft.App/sandboxGroups` can actually be
      created. **Done 2026-08-08, and it overturned this spike's own premise.** The proposal
      assumed access was suspect because the deploy fails at `Initialise`; that is a disabled
      Terraform state storage account and says nothing about creating sandboxes. Observed: Azure
      CLI 2.82.0 signed in to a Visual Studio Enterprise subscription, `Microsoft.App`
      **Registered**, and the provider offering `sandboxGroups`, `sandboxGroups/vnetConnections`
      and `sandboxes` at api-version **`2026-02-01-preview`** in a region list including **Spain
      Central**. The subscription was never the blocker — **but the role is.** Second check the
      same day, on subscription `e2f02d95-…` ("Sandbox - Services"): `az group create` refused with
      `AuthorizationFailed`, and RBAC gives this principal **`Reader` at subscription scope**.
      Signing in is not being able to create. Also recorded: that subscription holds 34 resource
      groups that read as live client environments, so it is a shared company subscription rather
      than a sandbox, and where this spike runs is a decision for its owner. **Still open**: a
      subscription or resource group where this principal has Contributor.
- [ ] 0.2 Confirm a registry the platform can pull from, and whether a private one needs a
      user-assigned managed identity as the announcement suggests.
- [x] 0.3b Install the `aca` CLI — it is a **separate surface, not `az containerapp`** — and run
      `aca doctor`. Reported install is `curl -fsSL https://aka.ms/aca-cli-install | sh` followed
      by `aca auth login`; verify the current one rather than trusting a quoted line.
- [ ] 0.4b Mint the credentials the provider paths need, and name them here before starting
      (ADR-0017). Reported: the Copilot provider validates a fine-grained `github_pat_…` token and
      **rejects classic `ghp_…`**, so an existing classic PAT will not do. Both are human-only
      steps — an agent cannot mint them — which is exactly what ADR-0017 asks to be scheduled
      rather than discovered.
- [ ] 0.3 Record the preview version, region and date every later observation is true of
      (ADR-0018: a measurement licenses only what it measured).

## 1. H1 — our own image boots and runs an agent

- [ ] 1.1a Cheapest first probe (the image is already built and exercised locally — see
      `findings.md`; what is unverified is the platform's import of it): create a sandbox from the **public prebuilt disk** (`--disk
      copilot` is the one reported) and confirm the basic loop — create, exec, delete — before any
      image of ours exists. A failure here is about access, not about our packaging.
- [ ] 1.1 Then build a minimal OCI image with `node`, `git` and `opencode`, push it, and create a
      sandbox from it with `--disk-id`. Record what the conversion accepted and how long the first
      boot took versus a resumed one.
- [ ] 1.2 Run `opencode run` to completion inside and record its output verbatim. `opencode
      models` is a cheap second probe: it answered 41 on the authoring host and 495 inside an sbx
      sandbox, so the number says something about the environment as well as the CLI.

## 2. H2 — the workspace, and whether co-location breaks

- [ ] 2.1 Get a repository-sized workspace into a sandbox created from elsewhere, by **each**
      mechanism the platform offers, and time all of them: upload over the data plane, a clone the
      sandbox performs over its own egress, and a volume. Measuring one and concluding about the
      three is the mistake ADR-0018 names — and the first draft of this task did exactly that, by
      asking only about the clone.
- [ ] 2.2 Have the agent commit and publish a branch and a pull request from inside (DEC-062), and
      confirm nothing needs to travel back to whoever created the sandbox.
- [ ] 2.3 State the verdict in one sentence: does the executor still have to be where the sandbox
      is? Cite the `--clone` spike either way, because that is the finding this either overturns or
      confirms.

## 3. H3 — a preview port, alive and then absent

- [ ] 3.1 Serve something inside on a declared port and reach it from outside for the life of the
      sandbox.
- [ ] 3.2 End the sandbox and confirm there is nothing left — not an error page, not a stale
      route. Record how exposure is scoped: public by default would be a finding, not a detail.

## 4. H4 — the credential

- [ ] 4.1 Exercise the **typed provider credentials** reported for this preview —
      `aca sandboxgroup credential create --type github-copilot` and `--type anthropic-claude`.
      Those are exactly the two this product's runtimes authenticate against, so if they work the
      deployed habitat's credential story is better than a generic secret, not worse. Confirm the
      value is injected through the platform path and **prove the negative the way #288's tests
      do**: run something inside that prints the variable and confirm it is empty.
- [ ] 4.1b Record the consequence for Claude specifically. #288 could not carry a Claude Code
      session on macOS because it lives in the system keychain with no file to copy; if
      `anthropic-claude` is a first-class provider here, the substrate that **cannot** do session
      carriage solves the Claude problem another way. That is a finding about the product's shape,
      not about Azure.
- [ ] 4.2 Exercise the egress policy: deny-all plus allow list, reported as
      `--egress-default Deny --egress-rule "github.com:Allow"` and changeable on a live sandbox
      with `aca sandbox egress set`. Confirm a denied host is actually refused — an allow list
      nobody tested the deny side of is a list, not a policy.
- [ ] 4.3 Write down the negative explicitly: **#288's session carriage cannot work here** — there
      is no machine owner whose files to copy — so this substrate requires stored credentials.

## 5. H5 — the lifecycle against a real Run

- [ ] 5.1 Run an agent that thinks for several minutes with no I/O and confirm the idle timeout
      does not suspend it mid-work.
- [ ] 5.2 Suspend and resume deliberately, mid-run, and confirm whether a live CLI actually
      continues — the announcement claims memory, disk and running processes are restored.
- [ ] 5.3 Confirm a phase can be killed at BR-005's timeout, and that nothing retries (BR-004).

## 6. H6 — economics and limits

- [ ] 6.1 Record what a thirty-minute Run costs, and what idle actually costs.
- [ ] 6.2 Check a Sandbox Group's maximum sandbox count against this product's per-project
      concurrency cap.

## 6b. The seam

- [ ] 6.1b Answer the question that decides how big any follow-up is: does this substrate fit
      `IAgentProcessHost` — command, arguments, workspace, environment, timeout, line callback,
      optional published port — as a third implementation beside the local host and sbx? Exec with
      streamed stdout/stderr, ports and a CLI to shell out to suggest yes. If it does not fit, say
      exactly which member of the interface it breaks, because that is the difference between a
      change and a programme.
- [ ] 6.2b Decide what a **C# executor** drives this with, which is not the same question as
      "is there an SDK". There are two clients — a control plane over ARM that creates groups, and
      a data plane (`SandboxGroupClient`) that owns exec, files, ports, secrets and egress — and
      everything a Run needs is in the second. Python (`azure-containerapps-sandbox`, `0.1.0b3`)
      and JavaScript (`@azure/containerapps-sandbox`, `1.0.0-beta.1`) both exist and are beta; no
      dedicated .NET package surfaced, and `Azure.ResourceManager.AppContainers` is ARM-only.
      **So confirm the `aca` CLI covers the data plane** — exec with streamed output, file
      transfer, ports — and not merely group management. If it does, this is the same shell-out
      shape `SbxAgentProcessHost` already is. If it does not, the answer is raw REST and the
      follow-up is bigger than a seam implementation.

## 7. The verdict

- [ ] 7.1 One recommendation with its reason: pursue, park, or reject — and if pursue, what the
      next change would have to decide. Hypotheses that failed are written up as fully as the ones
      that held; a spike whose record only contains good news bought nothing.
- [ ] 7.2 Delete the resource group and confirm nothing is left running.
