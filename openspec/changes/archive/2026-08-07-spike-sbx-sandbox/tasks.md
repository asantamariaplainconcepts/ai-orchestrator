## 1. H1 — it runs here

- [x] 1.1 Install `sbx` on this Mac (brew, per docs.docker.com/ai/sandboxes), record the exact
      version pinned; boot a default sandbox and run a trivial command in it. Record boot
      wall-clock. If Apple Silicon or macOS version refuses, the spike ends here with a no-go
      naming the refusal.

## 2. H2 — it runs our work

- [x] 2.1 Author the custom image under `poc/` and load it into sbx. (Reality was simpler than
      planned: the experimental `sbx kit` path was unnecessary — a template is a plain Docker
      image; `docker save` + `sbx template load` needs no registry. Credential discipline
      exceeded the plan: sbx service secrets live in the OS keychain and the sandbox only ever
      sees a sentinel the host proxy swaps at egress.)
- [x] 2.2 Execute the Run shape end to end against the owner's scratch repo — done with the
      **opencode** agent and the DEC-044 free model, which needs zero secrets: headless
      `opencode run` inside the sandbox read the repo, then an edit prompt changed `greet.js`
      and the diff appeared in the host workspace (virtiofs). Evidence in findings.md.
- [x] 2.3 The credential-injection leg (re-scoped by the owner, 2026-08-07: no Anthropic
      Console account — the claude headless run stays **not verified**; the property under
      test was the sentinel, and it is proven stronger than planned with the github service
      secret the owner does have). Evidence: `gh auth token | sbx secret set github` (value
      never displayed); in a fresh sandbox `GITHUB_TOKEN` is EMPTY (len=0 — not even a
      sentinel), yet an uncredentialed `curl api.github.com/user` returns 200 as the owner,
      and `git ls-remote` against the **private product repo** lists branches with zero
      credentials inside. Injection is by destination service, so the anthropic/openai paths
      are the same mechanism; claude-specific auth ergonomics remain the one unverified box.

## 3. H3 — the firewall is real

- [x] 3.1 Re-run 2.2 with deny-by-default network policy allowing only GitHub and the AI
      provider endpoint. It must still complete. (Done: `policy reset` → `init deny-all` →
      allow `github.com,*.github.com,opencode.ai`; fresh sandbox; agent run succeeded and a
      github.com clone succeeded. Balanced policy restored afterwards.)
- [x] 3.2 Prove the deny: from inside the sandbox, attempt one unallowed egress (e.g. the
      host's localhost and one arbitrary domain) and record the observed failure verbatim.
      (Done: example.com, api.anthropic.com, host.docker.internal AND the host gateway IP all
      answered HTTP 403 "Blocked by network policy … blocked by default deny policy" — the
      host's own services are unreachable by default. Evidence in findings.md.)

## 4. H4 — a .NET process can drive it

- [x] 4.1 A ~100-line console harness under `poc/` (not in `src/`): Process.Start the `sbx`
      CLI to create → run 2.1's kit → wait → collect exit code and logs → destroy. Assert the
      three outcomes the PodRunLauncher shape needs distinguishable: success, non-zero exit
      with captured stderr, and launcher-level refusal (sbx absent/broken) — the #279 remedy
      pattern needs the third to be nameable. (Done: `poc/SbxHarness.cs`, file-based .NET 10
      app, 10/10 checks green — inner exit codes travel verbatim, refusals name their cause on
      stderr, absent binary is a distinct Win32Exception, and the `shell` agent gives
      arbitrary-workload sandboxes. `rm` needs `--force` off-tty.)

## 5. H5 — the overhead is tolerable

- [x] 5.1 Same Run shape three times under sbx, three times as today's docker pod (existing
      `Dispatch:PodImage` path); record wall-clocks side by side in findings.md. Order of
      magnitude only — the question is "seconds or minutes of tax per Run", not a benchmark.
      (Done with `docker run --rm` of the same image as the pod-path stand-in — the full
      dispatch stack would have added Postgres+queue noise to both sides equally. Tax ≈ 4.5s
      per cycle; LLM anchor 36.3s vs 18.6s single-sample. 50%-of-RAM default memory noted.)

## 6. H6 — the cloud shape is nameable (desk-check)

- [x] 6.1 Document in findings.md: KVM/nested-virt prerequisites for a selfhost VM (named
      Azure SKU families as the worked example), the three candidate answers for "who invokes
      the CLI on a remote sbx host" (host-side launcher service, SSH from the orchestrator, a
      future sbx API), and what one-vs-several VMs changes for MaxConcurrentPods-style
      bounding. Questions sharpened, nothing built. (Done — see findings H6; key discovery:
      several VMs need no scheduler, they are just competing queue consumers.)

## 7. Verdict

- [x] 7.1 findings.md closes with go/no-go per D5: each H1–H5 verdict with its evidence, H6's
      open questions, and — if go — the named follow-up change
      (`split-run-pod-into-executor-and-sandbox`) with its first three design questions. Run
      `openspec validate spike-sbx-sandbox` and the repo lint gates for the changed docs.
      (Done: verdict GO with three not-verified boxes carried honestly — claude auth
      ergonomics, headless `sbx login`, Linux/KVM leg. Validate: valid. Prettier: clean.
      Machine state documented in findings for reversibility. **Correction:** an earlier
      "Prettier: clean" claim in this session was false — `rtk` masked a non-zero exit
      (the known rtk-masks-failures hazard); re-run through `rtk proxy`, design.md and
      findings.md do differ from Prettier's output. It is not a gate: CI's `format:check`
      is `prettier --check .` scoped to `src/frontend` (green), and 32 of 33 files in
      `openspec/specs/` are likewise unformatted, so openspec markdown sits outside the
      effective formatting gates. Left unformatted deliberately — reformatting one file
      would make it the outlier.)
