## 1. The entry mode

- [x] 1.1 DispatchWorker accepts `--run <id>`: executes exactly that Run, exit 0 on completed
      execution (design D4), non-zero only when execution could not happen
- [x] 1.2 Exercised at the worker seam against a real database (2026-08-04 probes): bad id →
      exit 64; missing database → exit 69 named (found the unhandled-throw hang and fixed it);
      unknown Run against real Postgres → executor's logged no-op, exit 0

## 2. The launcher

- [x] 2.1 `IRunPodLauncher` + docker-CLI implementation: `docker run --rm`, compose network,
      env for the database, the image from `Dispatch:PodImage`
- [x] 2.2 Consumer mode in composition (design D1/D2): image named → subscriber hands to the
      launcher; nothing named → in-process exactly as today
- [x] 2.3 Refusals by name: no socket, unknown image — the Run fails with the sentence (D3)
- [x] 2.4 The cap: semaphore default 2, `Dispatch:MaxConcurrentPods`; the third Run waits (D6)
- [x] 2.5 Functional tests at the launcher seam (faked launcher): selection, refusal, cap, and
      the nothing-configured path unchanged

## 3. Sessions

- [x] 3.1 Observe a real CLI in a pod with the read-only mount; record the observation in
      design.md and fix the mechanism (mount vs copy-in) accordingly (design D5)
- [x] 3.2 The default-on session provisioning with its off switch; the transcript names the
      credential source
- [ ] 3.3 Exercised for real: one Run in a pod against the free model, end to end

## 4. The habitat

- [x] 4.1 AppHost publish composition: image name set, socket mount present but commented with
      its root-equivalent warning; regenerate the compose
- [x] 4.2 selfhost/README.md: how to grant the socket, what the default sessions mean, the cap
- [x] 4.3 Dev-loop opt-in documented (config keys), refused with a named reason without docker

## 5. Proof

- [x] 5.1 Full gates — build, tests, lint, spec validation, compose-drift — plus a grep of the
      e2e tier for any name this change moves
