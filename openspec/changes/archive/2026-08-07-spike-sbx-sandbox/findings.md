# spike-sbx-sandbox — findings

Machine: MacBook (Apple Silicon, arm64), macOS 26.5.2 (25F84). Date: 2026-08-07.
sbx version pinned: **v0.38.0** (`c022b14634c4bea846ca12870d1d5e97d5868b54`).

## H1 — it runs here: **in progress**

**Install (evidence).** The official tap refuses this macOS outright:

```
$ brew install docker/tap/sbx
Error: This software does not run on macOS versions other than Sonoma.
```

Every cask in `docker/homebrew-tap` (0.21.0 → 0.38.0, rc, nightly) pins
`depends_on macos: :sonoma` — an *exact* match, which cannot be what a 2026 GA product means.
Worked around by installing what the cask itself would install: downloaded
`DockerSandboxes-darwin.dmg` from the official `docker/sbx-releases` v0.38.0 release, verified
`shasum -a 256` = `6fc2306598b8185228d920c1fd0fc09695d8022ad785a5b6655752f1145e7d3c` — **byte-identical
to the cask's pinned sha256** — and copied the DMG contents to `~/.local/share/sbx-0.38.0/`
with `~/.local/bin/sbx` symlinked to its `bin/sbx`. The binary runs on macOS 26:

```
$ sbx version
sbx version: v0.38.0 c022b14634c4bea846ca12870d1d5e97d5868b54
```

*Operational note for any adoption doc: on current macOS the brew path is dead until Docker
fixes the cask constraint; manual DMG install with hash verification is the workaround.*

**Daemon (evidence).** `sbx daemon start` runs the daemon in the **foreground** (it does not
daemonize and return) — a launcher integration fact H4 must account for. Once up:

```
$ sbx daemon status
Status: running
Socket: ~/Library/Application Support/com.docker.sandboxes/sandboxes/sandboxd/sandboxd.sock
```

**Preconditions discovered before the first sandbox:**

1. A **global network policy must be initialized** or creation refuses:
   `sbx policy init <allow-all|balanced|deny-all>`. Initialized to `balanced` for H1; H3
   tightens per-sandbox. Good default posture — deny-ish out of the box, refusal names the fix.
2. **Docker account sign-in is mandatory** for sandbox creation, even though the tool is free:

   ```
   ERROR: failed to check if sandbox exists: lookup runtime: request failed:
   401 Unauthorized: user is not authenticated to Docker: no default account
   profile set: secret not found
   ```

   `sbx login` is an interactive browser flow — completed by the machine's owner, not by
   tooling. **Adoption consequence:** every sbx host (dev Mac today, selfhost VM tomorrow)
   needs a Docker identity; a headless server enrollment story (`--username` +
   `--password-stdin` with an org access token) exists on the CLI but is unverified here, and
   it makes Docker Hub availability a runtime dependency of Run execution. This finding did
   not appear in any of the surveyed articles.

**Verdict: CONFIRMED** (after owner completed `sbx login`):

```
$ time sbx create --name spike-h1 claude .        # first ever: pulls base image (~420 MB)
   ✓ Created sandbox spike-h1                      # 34.7s total, download-dominated
$ time sbx create --name spike-h1-warm claude .    # image cached
   ✓ Created sandbox spike-h1-warm                 # 4.5s
$ time sbx exec spike-h1 uname -a
Linux spike-h1 7.0.12 #1 SMP PREEMPT Mon Jul 27 16:11:32 UTC 2026 aarch64 GNU/Linux
                                                   # 0.36s — own kernel; host is macOS 26 (Darwin)
```

The microVM claim is real: the sandbox reports its own Linux 7.0.12 kernel on a macOS host.
Warm sandbox creation is ~4.5s, exec round-trip sub-second. Also observed: `sbx rm` from a
non-tty refuses without `--force` ("stdin is not a terminal") — an H4 fact, launcher code must
pass `--force`.

## H2 — it runs our work: **in progress, mechanism proven**

CLI surface: `sbx create AGENT PATH` is agent-first (claude, codex, …, **opencode** — both
DEC-012/DEC-044 runtimes are first-class), `sbx exec` is docker-exec-shaped, `--clone` runs the
agent on an in-container clone wired back via git-daemon, `sbx run -d` is detached,
`sbx run <name> --branch=X -- "<prompt>"` is the headless one-shot — the Run shape verbatim.

**Custom image without a registry (evidence).** The experimental `sbx kit` path was not needed:
a template is a plain Docker image, and `sbx template load` takes a tar —

```
$ docker build -t spike-sbx/template:v1 .        # FROM docker/sandbox-templates:claude-code (poc/Dockerfile)
$ docker save spike-sbx/template:v1 -o spike-template-v1.tar
$ sbx template load spike-template-v1.tar        # 2.5s, no push, no registry
$ time sbx run -d --template docker.io/spike-sbx/template:v1 --name spike-h2 claude .   # 8.7s
$ sbx exec spike-h2 sh -c 'cat /etc/spike-marker; jq --version'
spike-sbx-sandbox template v1 (2026-08-07)
jq-1.8.1
```

The sandbox runs OUR image. Also observed inside: the workspace is a **virtiofs rw mount at the
same absolute path as on the host** (not a copy), and the credential machinery is visible as env
(`SBX_CRED_ANTHROPIC_MODE=none` before any secret is set; no raw key anywhere).

**The secrets model is the full firewall (design-level finding).** sbx service secrets live in
the OS keychain; the sandbox receives a **sentinel**, and the host-side proxy swaps in the real
credential on outbound requests — the agent can authenticate but can never read or exfiltrate
the value. This is the credential-injection pattern design.md D1 attributed to CubeSandbox
alone; **sbx has it too**, which strengthens the sbx case materially.

**The Run shape, end to end (evidence — opencode leg).** The `opencode` agent is first-class in
sbx, and DEC-044's free default model needs **zero secrets** — inside the sandbox:

```
$ sbx run -d --name spike-h2-oc opencode .                     # 13.9s create, stock opencode template
$ sbx exec spike-h2-oc ... opencode run -m opencode/deepseek-v4-flash-free \
    "Read greet.js and reply with exactly one line: the function name it exports"
→ Read greet.js
greet
$ sbx exec spike-h2-oc ... opencode run ... "Edit greet.js so the greeting ends with an exclamation mark."
Done.
$ git -C h2-repo diff                                           # on the HOST
-  return "Hello, " + name;
+  return "Hello, " + name + "!";
```

An agent read, reasoned and **edited** inside the microVM and the host saw the diff — the Run
shape (story → prompt → change → reviewable diff) works verbatim. First attempt also produced
H3 evidence ahead of schedule: under the `balanced` global policy the free model's endpoint was
refused with `Forbidden: Blocked by network policy: domain opencode.ai:443 — no matching allow
rule, blocked by default deny policy`, and one `sbx policy allow network opencode.ai` fixed it.
Deny-by-default is real and its refusal names the remedy.

**Note on opencode auth generally:** there is no "opencode" service secret — services are
providers (`anthropic, cursor, droid, github, google, groq, mistral, nebius, openai,
openrouter, xai`). A paid-provider opencode Automation would use that provider's secret;
opencode's own Zen account has no sbx service and would forfeit the sentinel property — with
DEC-044's free default this never arises.

**The sentinel, proven (task 2.3, re-scoped).** The owner skipped the claude leg (no Anthropic
Console account); the credential-injection property was proven with the github service secret
instead — and the reality is stronger than the docs' "sentinel" language:

```
$ gh auth token | sbx secret set github          # value never displayed anywhere
$ sbx run -d --name spike-h2-sentinel shell .    # fresh sandbox (secrets inject at creation)
$ sbx exec spike-h2-sentinel sh -c '...'
gh-mode=apikey
GITHUB_TOKEN: len=0 prefix=                       # EMPTY — not even a sentinel value to steal
--- api.github.com/user via proxy:
200
"login": "asantamariaplainconcepts"               # uncredentialed curl, authenticated at egress
$ sbx exec spike-h2-sentinel git ls-remote --heads https://github.com/<owner>/ai-orchestrator.git
141b1d9f… refs/heads/change/actionable-failure-inbox    # the PRIVATE product repo, no PAT inside
```

A plain curl and a plain git — carrying nothing — authenticate at the proxy. The agent cannot
leak what it never holds, and does not even need to know auth exists. For the product this is
BR-010's end state: the PAT does the Run's git work without ever entering the Run's reach.
(Mechanically this implies the proxy terminates TLS with a CA the sandbox trusts — worth
knowing when a Run talks to services that pin certificates.) **Claude headless auth ergonomics
remain the one unverified box** — injection is destination-generic, so the risk is CLI login
UX, not the security property.

## H3 — the firewall is real: **CONFIRMED**

Global policy is machine state with a deliberate ceremony: `sbx policy init` refuses to
re-initialize (`sbx policy reset` first). `balanced` turned out to be 193 allow rules; the test
used a true minimal posture:

```
$ sbx policy reset && sbx policy init deny-all
$ sbx policy allow network "github.com,*.github.com,opencode.ai"
$ sbx run -d --name spike-h3 opencode .          # fresh sandbox under the new policy
$ sbx exec spike-h3 ... opencode run ... "Read greet.js ..."   # → greet  (allowed egress works)
$ sbx exec spike-h3 git clone --depth 1 https://github.com/octocat/Hello-World.git   # CLONE-OK
$ sbx exec spike-h3 curl https://example.com
Blocked by network policy: domain example.com:443
  detail: no matching allow rule — blocked by default deny policy      # HTTP 403 from the proxy
$ sbx exec spike-h3 curl https://api.anthropic.com                      # 403 — not in allowlist
$ sbx exec spike-h3 curl http://host.docker.internal:18888              # 403 — host's OWN services
$ sbx exec spike-h3 curl http://192.168.64.1:18888                      # 403 — gateway IP too
```

Everything not allowed answers a clean 403 whose body names the rule and the default — the
agent-facing error is self-explaining (an agent reading it knows to ask for an allowance, an
operator reading a log knows what fired). The host's localhost is unreachable by default, which
is the deny that matters for a Run executing untrusted repo content next to an orchestrator.
Balanced policy restored after the test (plus the `opencode.ai` allowance).

## Discovered along the way — Run previews are nearly free

`sbx ports` publishes `[[HOST_IP:]HOST_PORT:]SANDBOX_PORT`; **omitting HOST_PORT allocates an
ephemeral host port bound to loopback**. Product possibility (owner's idea, 2026-08-07): a Run
whose change is runnable starts the app inside its sandbox, the launcher publishes an ephemeral
port, and the portal reverse-proxies a preview into the Run detail — per-Run preview
environments with no extra infra, lifetime tied to the sandbox, no port collisions, loopback so
only the host's own Server reaches it. Caveats for a future proposal: agent-authored HTML
served through the portal origin needs strict iframe sandboxing or an isolated origin; and live
run-output streaming is the sibling feature (the log the worker already captures, streamed
while Executing). Not a spike task — recorded so the idea survives.

## H4 — a .NET process can drive it: **CONFIRMED**

`poc/SbxHarness.cs` (file-based .NET 10 console app, `dotnet run poc/SbxHarness.cs`) drives the
full cycle over `Process.Start`: create detached → exec → collect → `rm --force`. 10/10 checks:

- **Success**: exit 0, stdout captured, guest kernel confirms the microVM (`Linux`).
- **Work failed**: an inner `exit 3` travels to the host verbatim as the exec's exit code, with
  stderr captured — the same shape `WaitContainerAsync.StatusCode` + logs give the launcher today.
- **Launcher refusal, two arms**: an impossible request (unknown sandbox) exits non-zero naming
  the sandbox on stderr; an absent `sbx` binary throws `Win32Exception` before any process
  exists. Distinguishable from each other and from work failure — the #279 remedy pattern maps.
- The `shell` agent creates sandboxes for arbitrary workloads (no AI agent involved) — the
  launcher does not need to pretend a Run is "claude" to get a sandbox.
- Off-tty ergonomics: `rm` requires `--force`; earlier correction — `sbx daemon start` DOES
  daemonize and return, it just took >180s on its very first boot (subsequent daemon starts are
  fast); a supervisor (launchd/systemd) is still the right call for a server host.

## H5 — the overhead is tolerable: **CONFIRMED** (order of magnitude, as scoped)

Same image, same trivial work, full lifecycle (create → exec → remove), three runs each:

| | run 1 | run 2 | run 3 |
|---|---|---|---|
| sbx microVM cycle | 5.17s | 4.51s | 4.42s |
| `docker run --rm` | 0.24s | 0.22s | 0.19s |

Isolation tax ≈ **4.5s per Run** on this Mac. One full-LLM anchor each way (create + headless
`opencode run` reading the fixture + remove): sbx 36.3s vs docker 18.6s — single samples, free-
model latency variance included, consistent with the cycle tax plus sandbox creation. Against a
real Run (minutes of agent work), seconds of tax. Andrew Lock's "crippling" verdict was about
interactive dev loops, not batch Runs; for this product's shape the overhead is immaterial.
Caveat worth carrying: **default sandbox memory is 50% of host RAM** — a launcher running
N concurrent sandboxes must pass `--memory` explicitly or two sandboxes exhaust the machine.

## H6 — the cloud shape is nameable: **desk-check written**

**KVM prerequisite.** Linux x86_64 + KVM (Ubuntu 22.04+ per Docker's requirements). Bare metal:
trivially yes. Cloud VMs need nested virtualization: **Azure** documents it on v3+ D/E families
(Dv3/Ev3 onward) — the worked example for a selfhost VM; **GCP** supports it behind a flag;
**AWS** does not on regular instances (metal only). The selfhost README must name this
prerequisite the day sbx becomes a launcher option.

**Who invokes the CLI on a remote sbx host.** sbx exposes no REST API (H1/H4: CLI + local
daemon socket). Candidates, most product-shaped first:

1. **The worker lives on the VM as a host process** (systemd unit), consuming the queue exactly
   as the containerized worker does today, invoking `sbx` locally. Smallest conceptual delta —
   the queue habitat already separates Server from worker; what changes is the worker's
   packaging (host process vs container), because a containerized worker cannot reach the
   host's sbx daemon. This inverts #246's "pod runs the worker image byte-for-byte" — the
   follow-up must own that trade.
2. SSH from the orchestrator to the VM per Run — operationally brittle, credentials to manage,
   nothing product-shaped about it.
3. A future sbx daemon API — the direction exists (0.37 experimental SSH sessions, community
   SDKs shelling out today) but nothing to build on yet.

**One VM vs several.** `MaxConcurrentPods` becomes per-VM slots (bounded by RAM ÷ per-sandbox
`--memory`, not by CPU). Several VMs = several queue consumers — the existing competing-
consumer pattern already balances Runs across them with no scheduler to build; what stays
per-VM is the Docker identity (`sbx login` — headless enrollment via `--username` +
`--password-stdin` with an org token, unverified in this spike) and the loopback-bound
ephemeral ports (a Run-preview proxy must be colocated on the Run's own VM).

## Verdict: **GO** — the follow-up is `split-run-pod-into-executor-and-sandbox`

Every load-bearing assumption held, several came back stronger than hypothesized:

- H1 ✅ real microVM on the dev Mac (own kernel), warm create ~4.5s
- H2 ✅ custom image without a registry; full headless Run (read → edit → host-visible diff)
  with opencode + the DEC-044 free model and zero secrets; **credential injection proven with
  the private product repo — the credential never exists inside the sandbox at all**
- H3 ✅ deny-by-default with a 3-domain allowlist runs the Run and blocks everything else —
  including the host's own localhost — with refusals that name the rule
- H4 ✅ a .NET process drives the whole cycle over the CLI with the three launcher outcomes
  distinguishable
- H5 ✅ ~4.5s isolation tax per Run — immaterial against minutes-long Runs
- H6 ✅ desk-checked: Azure v3+ nested virt, worker-as-host-process invoking sbx locally,
  N VMs = N competing queue consumers

The follow-up change this licenses (design D5): the executor stays with the orchestrator; the
sandbox holds workspace + agent CLI only; `ISandboxLauncher`-style seam chosen by configuration
presence, sbx as its first driver beside the existing docker-pod driver. Its first three design
questions, sharpened by evidence: (1) does DEC-062's "the agent publishes its PR itself" become
"the agent pushes through the injecting proxy, holding nothing" — the evidence says it can;
(2) who supervises the sbx daemon and the per-host Docker identity in each habitat (the new
operational condition this spike discovered); (3) what `--memory` policy bounds concurrent
sandboxes per host. **Not verified, carried honestly:** claude headless auth ergonomics under
sbx; headless `sbx login` enrollment; Linux/KVM behaviour (all evidence here is macOS —
the selfhost leg needs one afternoon on a Linux VM before the follow-up's selfhost tasks are
written as fact).

## Post-spike machine state

sbx v0.38.0 at `~/.local/bin/sbx` (manual DMG install), daemon running, global policy
`balanced` + `opencode.ai`, github service secret in the keychain (removable with
`sbx secret rm github`), spike sandboxes left `stopped` (`spike-h1`, `spike-h2`, `spike-h2-oc`,
`spike-h3`, `spike-h2-sentinel`; `sbx rm --force <name>` disposes), template
`docker.io/spike-sbx/template:v1` loaded in the sbx runtime and present in host docker.
