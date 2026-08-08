## 0. Preconditions, named before starting (ADR-0017)

- [ ] 0.1 Confirm an Azure subscription where `Microsoft.App/SandboxGroups` can actually be created:
      the preview may be gated by region or enrolment, and **this programme's Azure access is
      currently suspect** — the deploy pipeline has failed at `Initialise` for several days because
      the Terraform state storage account is disabled. Assume broken until checked. If it is
      broken, fixing it is this spike's first task and not a footnote discovered at step four.
- [ ] 0.2 Confirm a registry the platform can pull from, and whether a private one needs a
      user-assigned managed identity as the announcement suggests.
- [ ] 0.3 Record the preview version, region and date every later observation is true of
      (ADR-0018: a measurement licenses only what it measured).

## 1. H1 — our own image boots and runs an agent

- [ ] 1.1 Build a minimal OCI image with `node`, `git` and `opencode`, push it, and create a
      sandbox from it. Record what the conversion accepted and how long the first boot took versus
      a resumed one.
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

- [ ] 4.1 Exercise **both** documented paths: the egress proxy that claims to inject credentials at
      the boundary, and secrets injected as environment variables at boot. Record which a Run would
      use, because its transcript has to say it. For the proxy path, prove the negative the way
      #288's tests do — run something inside that prints the variable and confirm it is empty.
- [ ] 4.2 Exercise the egress policy: can it express deny-all plus an allow list, as sbx's does?
      The documentation says deny-default with host allowlists; that is a claim, not a result.
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
- [ ] 6.2b Record the control surfaces available and which this codebase would use: CLI, Python
      SDK, MCP, raw REST. No .NET SDK is named; the sbx host already shells out to a CLI, so that
      is precedent rather than a blocker.

## 7. The verdict

- [ ] 7.1 One recommendation with its reason: pursue, park, or reject — and if pursue, what the
      next change would have to decide. Hypotheses that failed are written up as fully as the ones
      that held; a spike whose record only contains good news bought nothing.
- [ ] 7.2 Delete the resource group and confirm nothing is left running.
