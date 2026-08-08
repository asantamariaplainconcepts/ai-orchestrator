## Context

Three substrates have been measured for running an agent, and each answers a different habitat:

| | Where | Isolation | How a Run starts |
|---|---|---|---|
| in-process | a machine somebody owns | none — the agent is a child of the portal | `Process.Start` |
| sbx | the dev loop | microVM, own kernel | local CLI, workspace **mounted** from the host |
| **ACA Sandboxes** | a deployment | microVM, own kernel | authenticated API, workspace **sent** |
| ~~pod image~~ | *was* the deployment | container, **shared kernel** | **docker socket, root-equivalent** |

The last row is what this change removes. Everything below follows from measurements in
`spike-azure-container-apps-sandboxes/findings.md`; where a decision rests on one, the number is
cited so a reviewer can check it rather than trust it.

## Goals / Non-Goals

**Goals.** A deployed Run executes in a hardware-isolated sandbox with no socket anywhere, its
workspace arrives without co-location, its output streams while it works, and nothing survives it.

**Non-Goals.** The dev loop. In-process execution. Run durability across worker restarts — declined
explicitly in D2. Measuring cost. Making sbx and this share an implementation.

## Decisions

### D1 — A third implementation of the launcher seam, not a fourth kind of thing

`AgentSandboxComposition` already selects a host by configuration presence, and the spec already
refuses a habitat that names two substrates. Adding `Agents:Sandbox:Launcher = aca` fits that
without touching either rule.

This is possible only because of D2. If the executor had to learn a new lifecycle, the seam would
have been the wrong place and this would have been a new component.

### D2 — The poll loop lives inside the host, and the seam survives

The measurement that shapes this change: **`aca sandbox exec` fails between 50 and 60 seconds**
(three attempts at 60 s, three failures, each giving up at ~121 s with `retry policy expired`),
while a Run may last thirty minutes under BR-005 and must stream its output line by line (#96). One
`exec` can do neither.

So `AcaAgentProcessHost.Run` starts the agent **detached** inside the sandbox, writing to a file,
and polls with short `exec` calls — reading new lines and forwarding them through `onOutput` as it
goes. Verified in the spike: detached work continued while short polls read `work 4` … `work 7`,
sandbox `Running` throughout.

From outside, `Run()` still blocks until the agent finishes and still streams. The executor learns
nothing.

*Alternative rejected — put the loop in the executor and make the Run durable across worker
restarts.* Genuinely better: the sandbox keeps working when its watcher dies, and suspend/snapshot
would let a Run outlive the process that started it. It also changes what an in-flight Run **is** —
durable state, resumption, and a conversation with BR-004 about what "nothing retries" means when
the work survives. That is its own change and is named in the issue's out-of-scope rather than
smuggled in here.

### D3 — What the habitat must declare, because the defaults are wrong for a Run

Two platform defaults are actively hostile to this workload, and both were found by exercise rather
than documentation:

- **Auto-suspend is on at 600 s**, and "idle" means no data-plane activity *from outside*, not no
  work inside. With the timeout lowered to 60 s the spike watched a sandbox go `Stopped` at t+41 s
  **while a process wrote inside every second**. An agent that thinks for ten minutes would be
  suspended mid-thought. The launcher disables it.
- **Deny-default egress is opt-in, not default.** A sandbox created with no policy reached
  `example.com` and `pypi.org` with 200s; `egress show` said none was configured. The portal
  documents deny-by-default as a property of the platform. The launcher declares the policy
  explicitly, and the spike confirmed the deny side genuinely denies (403 for both, 200 for an
  allowed host) with an auditable decision log.

Declared by the habitat, never inferred (ADR-0010). A deployment that forgets is a deployment whose
agent runs unrestricted, so composition refuses rather than defaults.

### D4 — One SandboxGroup per Project

Credentials attach to the **group**, as typed providers — `github-copilot` (a fine-grained
`github_pat_`) and `anthropic-claude` (`sk-ant-`), confirmed from the CLI's own help. That is a
better credential story than the pod path's environment values, because nothing enters the sandbox.

But #244 promises a project's Runs bill to that project's identity, and a group-level credential
shared across projects would break it silently. So a Project gets its own group.

*Alternative rejected — one group per deployment with generic per-Run secrets.* Simpler, and it
throws away the property that makes this substrate worth adopting: the value would travel into the
sandbox again.

### D5 — Previews are relayed, and the port is Entra-gated

`aca sandbox port add` returns a public URL, and `--anonymous` is opt-in. Handing that URL out
would move the preview's boundary outside the product and make "nothing after the Run" depend on a
deletion happening.

So the port is created gated, and the portal relays it exactly as it does today. run-previews'
contract is unchanged, which is the point: a substrate swap that changed what a Member sees would
be two changes wearing one name.

### D6 — The pod substrate is removed rather than left standing

Leaving it would mean two cloud substrates, which the spec already refuses at composition, and
would keep the docker socket in the product's supported surface. A habitat still naming a pod image
is refused, naming what replaced it — the same shape as every other refusal here, because an
operator upgrading needs the sentence more than the error.

**A hole found on contact with the code, and the decision that closed it.** The sentence above was
originally "in-process execution remains for a machine somebody owns, so nothing is left without an
answer." That is false. The Server's own image **deliberately carries no agent CLI** — fattening it
was rejected at grill, and only the conversation image installs `claude` and `opencode`. Retiring
the pod would have left a `docker compose up` selfhost with no substrate at all: not in-process,
because there is no CLI to run; not the pod, because it is being removed.

So **selfhost adopts sbx**. One isolation model everywhere, and the docker socket leaves the
product entirely rather than surviving in a corner — which was the point of the exercise that
started all of this.

**This makes an unverified claim load-bearing, and that is stated rather than absorbed.** All of
`spike-sbx-sandbox`'s evidence is macOS. Its own findings record the Linux prerequisite as
x86_64 + KVM and name the selfhost leg as needing "one afternoon on a Linux VM" before the
follow-up's selfhost tasks are trustworthy. That afternoon has not happened. Until it does, the
selfhost habitat's substrate is a **hypothesis** (ADR-0005), and this change carries a task for it
rather than a claim.

## Risks / Trade-offs

**Public preview.** The CLI surface is expected to move, and every measurement behind this design
carries its date for that reason. A GA that changes `exec`'s ceiling would make D2 unnecessary
rather than wrong.

**Cost is unmeasured and accepted.** Stated in the issue. It is the one thing that could still
refuse this, and it is not knowable from a session.

**Polling has a resolution.** Output arrives in poll-sized chunks rather than truly line-by-line.
UC-027 asks that a Member see the Run working, not that they see every keystroke, but the interval
is a number this change has to choose and defend.

**Role propagation is observable.** The spike hit 403s for about a minute after granting the data
role. Provisioning must tolerate it rather than treat the first failure as fatal.

## Migration Plan

The pod path is removed in the same change that replaces it, so no deployment carries both. A
habitat naming `Dispatch:PodImage` is refused at composition with the sentence that names its
replacement. Nothing about the outbox, the queue or a Run's durable state changes — the substrate
decides where execution happens, never what survives a crash.

## Open Questions

- The poll interval: frequent enough that UC-027 reads as live, sparse enough not to spend an
  `exec` per second on a thirty-minute Run.
- Whether a sandbox should be created per Run or reused across a project's Runs. Per Run is the
  obvious answer and matches "a sandbox is per Run and does not outlive it"; snapshots make the
  other one tempting and it should be refused deliberately rather than by omission.
