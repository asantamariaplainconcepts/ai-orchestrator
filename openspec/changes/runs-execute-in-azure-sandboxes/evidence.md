# What was exercised against real Azure, and what it found

Task 7.2. Recorded verbatim, including what did not work (ADR-0001). Everything below was
observed on **2026-08-09**, `aca 1.0.0-preview.1`, region **spaincentral**, subscription
`422bb77e-…` ("Azure subscription 1"), group `aio-exercise` in `rg-aio-sandbox-exercise`
(task 0.3).

Run it again with:

```
AIO_ACA_EXERCISE=1 AIO_ACA_GROUP=aio-exercise \
ACA_SUBSCRIPTION=… ACA_RESOURCE_GROUP=… \
dotnet test src/tests/AiOrchestrator.DispatchTests --filter RealAcaSandbox_Should_Constraint
```

## The point of doing this at all

The stand-in script proves the host **calls** what it should. It cannot prove the platform
**answers** as the design believed, because the script was written from the same beliefs. **Five
defects** — four here and a fifth under *The credential* below — were invisible to a green unit
suite, and every one of them would have shipped.

## Four defects the exercise found

### 1. `fs cp` has no `--id`, and the host used one

```
exit 2: error: unexpected argument '--id' found
Usage: aca sandbox fs cp [OPTIONS] <SOURCE> <DESTINATION>
```

The remote side is `<sandbox-id>:<path>`. The very first real call failed.

### 2. **The platform has no recursive copy at all** — the workspace design was wrong

```
exit 1: Error: Is a directory (os error 21)
```

No verb under `sandbox fs` — `ls`, `cat`, `write`, `rm`, `mkdir`, `stat`, `cp` — copies a tree.
A Run's workspace is a git clone, so "send the workspace" could not be one call. `SendWorkspace`
now **tars locally → copies one file → untars inside**, and deletes the archive either way.

This is the single biggest correction here, and it was unreachable from a fixture: the spike had
sent one file with `fs write` and generalised from it.

### 3. The last lines of every Run were dropped

The 90-second Run completed and streamed, but `working 89` never arrived. `Forward` holds back a
chunk's final element on purpose — a chunk not ending in a newline is a line still being written,
and a watcher should not see half a sentence. That reasoning stops the moment the exit code is on
disk: nothing is partial after the process has gone. `Forward` now takes `ended`, and the reads
after the exit file (and after a timeout) pass it.

Against a stand-in that answers instantly this is invisible.

### 4. The egress decision log is JSON, and the code read lines

The real answer:

```json
{"networkEgress":{"allowed":[],"denied":[
  {"timestamp":"2026-08-09T09:13:51Z","host":"example.com","method":"GET","path":"/","scheme":"https"}]}}
```

The reader filtered lines containing `Deny`, which the real output never contains — so a Run that
reached a blocked host reported **nothing at all**, the exact failure mode the feature exists to
prevent. Now parsed from `networkEgress.denied`, and an unrecognised shape is reproduced verbatim
rather than dropped.

**The stand-in had invented a table.** A fixture that invents its subject's answers can only ever
confirm the invention (ADR-0016); it now carries the real JSON, kept verbatim.

## What held

| | Observed |
|---|---|
| A Run past the `exec` ceiling | 90 s of work, completed, exit 0, **1 m 43 s** wall clock |
| Output while it worked | `working 1` … `working 90`, arriving in poll-sized chunks |
| Auto-suspend | disabled per sandbox; the Run outlived the t+41 s suspension the spike saw |
| Workspace without co-location | a file written on this Mac read back inside a **remotely created** microVM — no mount, no socket, no host grant (task 3.2) |
| Deny-default egress | `example=403` with `github.com` allowed |
| The decision log | `example.com GET /` named, timestamped, in the Run's own output |
| Nothing survives | 4 Runs, each creating a sandbox: **0 sandboxes** afterwards |

## Two smaller things worth writing down

**`aca sandbox delete` prompts, and the product was already right.** Interactively it asks
`(y/N)` and a piped invocation answers *Aborted*. `Dispose` passes `--yes`. Had it not, every Run
would have leaked a sandbox and a bill — and the unit test would still have been green, because
the stand-in never prompts.

**The signed-in default subscription is the disabled one.** `az group create` failed with
`ReadOnlyDisabledSubscription` naming `647b3372-…` ("ANDONI Visual Studio Enterprise"), which is
`Disabled` and is also why this repository's deploy has been red. The working one had to be
selected explicitly. This is the third time the programme has paid for *signing in is not
authority* (ADR-0017).

## The credential, exercised (2026-08-09, later the same day)

A `github-copilot` credential was created on the group — id only, the value entered through the
CLI's hidden prompt and never through this session.

### A fifth defect: the host never asked for it

`aca sandbox create` was called with `--group` and `--disk` and nothing else. Design D4 promised
per-Project typed credentials; the code never passed `--credential`, so every sandbox this host
made had no credential at all and no agent inside could ever have authenticated. No fixture could
notice — the stand-in was never going to authenticate anything.

`AcaSandboxOptions.Credentials` now carries the ids and the habitat declares them
(`Agents:Sandbox:Credentials`). Ids, never values (BR-010). Unlike the egress list this is **not**
refused when absent: a habitat whose agent authenticates some other way is legitimate, and a Run
without a credential fails loudly at the agent rather than silently at the boundary.

### 4.2 — held, and asserted without ever holding the secret

A sandbox created with the credential attached, then asked from inside: its whole environment,
and every file under `$HOME`, `/etc` and `/tmp` containing `github_pat_`. **Nothing.** The
platform holds the token and injects it at its egress boundary, which is the property that makes
this substrate worth adopting — the pod path handed the value in as an environment variable.

The test never learns the token. It looks for the *shape* every GitHub fine-grained PAT has, which
keeps the secret out of the repository, out of any CI log and out of this session, and can still
fail: were the platform to inject the credential as an environment variable, `github_pat_` would
be sitting in the output.

### 4.3 — held

Composing the `aca` launcher, the runtime the selector hands back names the platform's injection
as its credential source, distinctly from sbx's refusal-to-carry. Asserted on the runtime rather
than the host, because #296's own 6.1b finding was a wire nobody had connected between exactly
those two.

### A real agent Run — REFUSED, and the reason is worth having

The `copilot` disk carries `copilot` 1.0.69 and `gh`. With the credential attached and egress
opened to `github.com`, `api.github.com` and `api.githubcopilot.com`:

```
• If using a Fine-Grained PAT, ensure it has the 'Copilot Requests' permission enabled
```

**A repository-scoped fine-grained PAT is not a Copilot-models PAT.** This was named as unmeasured
when the token was chosen, and it is now measured: the platform accepts the token, and Copilot
refuses it. A real agent Run needs a PAT carrying **Copilot Requests**; nothing else about the
substrate is in the way.

This also settles the question of reusing one token for two jobs: it does not work, which is the
outcome least-privilege would have wanted anyway.

## What is still NOT verified, and why

- **A real agent Run** — blocked on a PAT with the `Copilot Requests` permission, above.
  Everything measured here used a shell command as the agent, which is exactly right for the
  boundary and proves nothing about a model.
- **4.4 — role propagation.** Not observed this time: `aca sandboxgroup create` now grants the
  data role itself and every data-plane call worked immediately. The spike's 403s did not
  reproduce, which is weaker than "tolerated" — it is "did not happen today".

## Cost (task 7.4)

Everything created was deleted: 0 sandboxes, and the group and resource group removed after the
run. The billed surface was five short-lived 1 vCPU / 2 GiB microVMs totalling **under ten
minutes** of sandbox lifetime. Azure's cost data lags by hours, so a figure taken now would be
zero for the wrong reason; the honest statement is the shape of the usage, not a number this
session can read.
