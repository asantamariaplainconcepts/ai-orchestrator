# Retro log

Append-only. One entry per change, written on the branch *before* the merge so it rides in the
change's own commit; post-merge findings are appended later as separate entries. Each entry
records what worked, what didn't, one change to make next time, and the human-vs-agent time
invested. **Never rewrite prior entries.**

When an entry surfaces a **structural** lesson — anything recurring, or anything that changes how
the workflow behaves — graduate it to an ADR under [`docs/adr/`](../adr/) and link it here. The
graduation rule is the *second* occurrence, not the third: patterns left ungraduated recurred ten
times in the project this framework came from.

## Entry format

```
## <date> — <change-name>
- **Worked:** …
- **Didn't:** …
- **Next time:** …
- **Time invested:** human <h>, agent <h>, cost <USD> (source: telemetry / manual)
- **ADR:** <link if a structural decision was graduated>
```

---

## 2026-07-25 — project-scaffolding

- **Worked:** Spec-before-code did its job — the layout, the deferral of Terraform, and the
  out-of-scope fence were all settled while they were still text. Lifting the analyzers, ArchTests
  and build props from the reference kit and applying a mechanical rename got the compile-time
  guardrails working in minutes rather than a day. Probing every guardrail instead of trusting it
  paid immediately: it revealed that MOD002 only inspects member *signatures*, so the ArchTest
  assembly-reference check is not redundant with it but complementary — neither alone is
  sufficient, and we would have believed otherwise.
- **Didn't:** Local verification was worth less than it looked. CI found five defects that green
  local runs had hidden: two lint-lane bugs, a flaky test of mine (`Guid.CompareTo` does not
  reflect GUID v7 time ordering — it passed locally purely by luck), SPA tests that only passed
  because a stale `wwwroot` was lying around, and then, from the E2E lane's very first run, two
  genuine holes in the product itself — the host had no `http` endpoint (which had been silently
  breaking `aspire run` too) and *nothing applied database migrations*. The functional tier had
  concealed the migration hole by migrating inside its own fixture, a path the application did
  not have. Also self-inflicted: a `git reset --hard` to undo a probe commit silently discarded
  unrelated uncommitted edits (a `.gitignore` rule and checklist ticks), which then had to be
  reconstructed.
- **Next time:** Run the E2E lane before claiming a foundation is done, not after — it was the
  only tier that could see the two real defects, and both were in the seams between components
  rather than inside any one of them. And never use `git reset --hard` to undo a probe while
  other work is uncommitted; revert the probe file directly.
- **Time invested:** human ~1.0 h (charter and corpus grill answers, spec review), agent ~2.5 h,
  cost not measured (source: manual — the telemetry stack lands in bootstrap Phase 2, so these
  are estimates and should be read as such)
- **ADR:** graduated in the `ceremonies` change —
  [ADR-0001](../adr/0001-verify-claims-by-exercising-them.md) (the endpoint and migration defects
  were both assumed-working) and
  [ADR-0002](../adr/0002-test-tiers-must-not-provision-their-own-preconditions.md) (the fixture's
  private migration). *Links added when the ADRs were written; the reflection above is unchanged.*

## 2026-07-25 — project-scaffolding (post-merge finding)

- **Worked:** Nothing new; this entry exists to record a defect found after the merge.
- **Didn't:** The squash commit that landed on main failed commitlint —
  `body-max-line-length` and `footer-max-line-length`, both exceeded because the body was written
  unwrapped. The rule is correct and the message was wrong, but the timing is the real problem:
  **a squash commit message is authored at merge time on the platform, so the local commit-msg
  hook never sees it.** The only gate that checks it runs on main, after the merge is already
  irreversible. Every branch commit passed; the one commit that becomes main's history did not.
  This is the same shape as the kit's rule about signal-suppressing actions — a gate placed after
  the action it is meant to govern cannot govern it.
- **Next time:** Wrap squash bodies at 100 characters, and validate the intended squash message
  against commitlint *before* merging rather than discovering it on main. The `/ds:sync` wrapper
  (Phase 3) should own this: it already sets the squash subject and body explicitly, so it is the
  single place that can check them while the merge is still preventable.
- **Time invested:** human ~0 h, agent ~0.1 h (source: manual)
- **ADR:** candidate — *validate every message and artifact that will become main's history
  before the merge, not after*. Second occurrence of "the gate ran too late" in this project;
  graduate it in Phase 3 and implement it as a `/ds:sync` precondition.

## 2026-07-25 — ai-delivery-layer

- **Worked:** Spec-first held again — the enforcement honesty in `design.md` (D6: these gates are
  agent-enforced Markdown, not machine-enforced) got settled as text before anyone could mistake
  the commands for airtight. The previous change's post-merge finding shipped here as a *gate*
  rather than a note: `/aio:sync` now lints the squash subject and body before merging, and it
  was probed both ways (a 140-character body line refused, a wrapped one accepted). Probing
  every mechanism instead of trusting it kept paying — the session-mapping hook was verified by
  watching it attribute its own branch, and the E2E lane found a genuine kernel defect.
- **Didn't:** I shipped **two speculative fixes before I could see the error**. The lane went
  intermittently red, and rather than fix the diagnostics first I inferred a cause twice — a
  database health check, then Npgsql retry-on-failure — and pushed each as a fix. Both were
  reasonable, neither was the bug, and each cost a full CI round. Worse, the diagnostic I *had*
  built was itself broken twice over: the log watch keyed on the resource's declared name (the
  stream is keyed by runtime ResourceId, so it silently yielded nothing) and started after
  `StartAsync`, missing the startup backlog. Three rounds passed before the failure could
  explain itself — at which point it named itself immediately: `Sender` was registered as a
  singleton, so it resolved handlers from the **root** provider and scoped `DbContext`s degraded
  to root-cached instances, one context shared across concurrent requests. Telemetry also
  produced nothing usable: `usage.jsonl` never appeared because the collector holding :4317 on
  this machine belongs to another project, so the times below are hand-estimated.
- **Next time:** When a lane goes red intermittently, **fix the diagnostics first, then
  diagnose, then fix** — never ship a hypothesis as a fix. A red run that cannot explain itself
  is a tooling defect, and it outranks the bug it is hiding. Corollary applied here: prefer the
  framework-level guard over the instance fix — `ValidateScopes`/`ValidateOnBuild` are now
  unconditional in every environment rather than left to Development's default, so this entire
  class of bug fails at startup instead of surfacing as an intermittent 500 under load.
- **Time invested:** human ~0.5 h (spec review, the `/ds:*` → `/aio:*` rename call, sync
  confirmations), agent ~1.8 h, cost not measured (source: **manual** — `collect-usage` found no
  `usage.jsonl`; the OTLP port is held by a foreign collector, so this change produced no
  attributable telemetry despite the mapping hook working correctly)
- **ADR:** **written in the `ceremonies` change as its first task** —
  [ADR-0001](../adr/0001-verify-claims-by-exercising-them.md) and
  [ADR-0002](../adr/0002-test-tiers-must-not-provision-their-own-preconditions.md). Both patterns
  reached their second occurrence in this change, and the kit's own post-mortem records what
  happens when graduation slips — patterns recurring ten times while everyone waits for a
  tidier moment. They were named here in full so Phase 3 only had to format them:
  1. **Verify claims by exercising them, never by reading configuration.** Phase 1: the host was
     assumed to have an endpoint and to apply migrations; neither was true. Here: health was
     assumed to mean "can serve", and the log watch was assumed to work because it compiled.
  2. **A test tier that provisions its own preconditions hides their absence from the
     application.** Phase 1: the functional fixture migrated privately, concealing that the app
     never did. Here: an all-sequential suite structurally could not observe a concurrency bug.
     Corollary: tests must exercise the app's own paths, and at least one must run in parallel.

## 2026-07-25 — ceremonies

- **Worked:** The ADRs went **first**, and they cite real incidents plus the check that now
  catches each class — so they graduate toward gates instead of becoming wall art. The Definition
  of Ready cites `RULE-001..007` rather than copying them, so a backlog-rule change propagates
  without editing it. And the 40-line onboarding limit did exactly what design D4 predicted: the
  file arrived at 49 lines and three rounds of trimming each deleted a *duplicated* fact rather
  than a useful one, landing at 40. That is a design prediction observed working, not asserted.
- **Didn't:** This change went smoothly, and that is itself the finding. It was pure documentation
  with **no executable surface**, so CI could only run lint and spec-validate — nothing here is
  verified by anything that runs. That is precisely the situation
  [ADR-0001](../adr/0001-verify-claims-by-exercising-them.md) warns about, written in this very
  change: the 49-line overrun was caught by a human reading a number, and a broken link would
  have been caught the same way or not at all. Separately, telemetry produced nothing for the
  **third consecutive change** — zero sessions mapped, no `usage.jsonl`, because another
  project's collector still holds port 4317 on this machine.
- **Next time:** Give ceremony docs a machine check wherever one is cheap. A link-resolution pass
  and an `ONBOARDING.md` line-count assertion in the lint lane would have caught the overrun
  before review rather than during it — the scripted sweeps I ran by hand should be steps in CI.
- **Time invested:** human ~0.3 h (spec review, sync confirmations), agent ~0.7 h, cost not
  measured (source: **manual** — `collect-usage` found zero mapped sessions and no `usage.jsonl`)
- **ADR:** none. The telemetry gap is on its third occurrence, but the fix is **operational, not
  architectural** — one collector per project, on a port this repo owns. An ADR saying "use a
  free port" would be ceremony without content. Recorded here as a **standing defect to fix in a
  `lane:spec-less` change before Phase 5**, when loop metrics start mattering. If the fix turns
  out to need a real decision, it graduates then.

## 2026-07-25 — design-system

- **Worked:** Fetching the reference's actual stylesheet instead of describing it from a
  screenshot turned the whole change from guesswork into measurement — and the palette derived
  that way proved **sufficient for the real interface with nothing added**: when the running app
  was later inspected, its active-nav fill and text were exactly the `--brand-soft` and `--brand`
  values already in the token file. Every gate was probed in both directions before being
  trusted, which is why five deliberate violations and one clean tree all behaved as specified.
- **Didn't:** I nearly shipped an accessibility failure **by being faithful**. The reference's
  dark brand gives white button text 3.98:1, under the 4.5:1 AA threshold; copying it accurately
  would have copied the defect. Worse, my first contrast measurement reported a confident
  `1.00` for everything because it parsed `oklch()` values as `rgb()` — a wrong answer that looks
  like an answer is far more dangerous than an error, and I only caught it because 1.00 was
  absurd on its face. Two more self-inflicted collisions followed: Prettier and the generator
  both claimed the generated adapter, and Prettier's line-wrapping of the canonical font stacks
  silently broke the token parser so the font tokens vanished from generated output with no
  error. The drift gate caught that one, which is the gate doing exactly its job.
- **Next time:** When adopting anything from a reference, **verify the property that actually
  matters** — contrast, accessibility, performance — rather than assuming a shipped product
  already did. And give every measurement a known-answer sanity check before trusting its output:
  had I measured a black-on-white control first, the broken parser would have announced itself in
  seconds instead of surviving into a decision.
- **Time invested:** human ~0.6 h (spec review, supplying the app screenshot, sync confirmations),
  agent ~2.0 h, cost not measured (source: **manual** — `collect-usage` found zero mapped
  sessions and no `usage.jsonl`; **fourth consecutive change with no attributable telemetry**)
- **ADR:** [ADR-0003](../adr/0003-a-derived-artifact-has-exactly-one-owner.md) — *a derived
  artifact has exactly one owner*. Second occurrence of the shape: Phase 1's `wwwroot` was tracked
  by git while the build rewrote it, and here Prettier and the generator both claimed `tokens.ts`.
  Graduated in the change that noticed the recurrence, per the rule.

## 2026-07-25 — fix-telemetry-collector-port (spec-less lane)

- **Worked:** The spec-less lane did exactly its job on first use — issue, branch, PR, CI, retro,
  and no bundle to archive because there was no behavioural delta to archive. The lane is now
  exercised rather than merely documented. And once guessing had failed, **bisecting our config
  against a minimal one found the real cause in minutes**: same config with the viewer removed
  wrote the file immediately.
- **Didn't:** I fixed the *reported* problem and nearly declared victory. The port was moved, the
  collector started, and telemetry still did not land — because the actual defect was elsewhere:
  the **optional Grafana viewer's retry queue was silently disabling the durable sink**. That is
  the precise opposite of what the `usage-telemetry` spec promises ("dashboards are disposable
  viewers", "losing the dashboard loses nothing"). The spec was right; the implementation had
  contradicted it for four consecutive changes without producing a single symptom, because the
  failure mode is **silence**. The original preflight had the same shape: it asked "is the port
  occupied?" when the question was "is *our* collector running?" — and an occupied port answered
  the wrong question convincingly.
- **Next time:** For anything whose failure mode is silence, **the acceptance test is the
  observable artifact, not the configuration change**. "The port is now free" is not "telemetry is
  captured". This change is only finished because a synthetic payload was pushed through and the
  bytes were read back out of `usage.jsonl` — had I stopped at "the container is running", the
  fix would have shipped still broken, and the next four changes would also have measured nothing.
- **Time invested:** human ~0.1 h (the call to fix this before Phase 5), agent ~0.8 h, cost not
  measured (source: **manual** — this change could not measure itself: it is the change that makes
  measurement possible, and its own work predates the fix. The four earlier changes stay `manual`
  permanently; nothing can recover telemetry that was never written.)
- **ADR:** none — recorded as a **first** occurrence of *an optional component must not be able to
  disable a required one*. It is a genuinely different shape from
  [ADR-0003](../adr/0003-a-derived-artifact-has-exactly-one-owner.md) (two owners for one
  artifact) and from the "gate ran too late" family. Graduate it if it recurs; writing an ADR from
  a single instance would be guessing at the general rule.

## 2026-07-26 — github-connector-backlog-mirror

- **Worked:** The concurrency test [ADR-0002] demanded **found a real race on its first run** —
  eight parallel refreshes colliding on the `(ProjectId, VendorId)` unique index; the fix is a
  narrow unique-violation catch, not a loosened constraint. Stubbing E2E at the **HTTP boundary**
  (a local GitHub API stand-in behind `Backlog:GitHub:BaseAddress`) kept real Octokit on the
  tested path with no token in CI. And the second module finally proved the architecture's
  standing claims instead of asserting them: the host discovered Backlog with zero edits to
  `Program.cs`, and the analyzers + ArchTests ran against two genuine modules.
- **Didn't:** Two bootstrap defects masked each other through every green build: nothing set
  `ASPNETCORE_ENVIRONMENT` under `aspire run` (launchSettings omitted it on purpose, the AppHost
  by omission), so the Server ran as Production and skipped migrations; fixing that exposed that
  terminal `UseSpa` swallowed `/api/*` in real Development, with Vite answering `200 index.html`
  for everything. Both surfaced only when the human deleted the data volume — and my own first
  verification was fooled by the same shape, reading "API 200" as success while Vite's fallback
  was the thing answering. Separately: rendering the page found copy defects no automated tier
  can see ("1 Stories"; an unconfigured project claiming "no open Stories in this repository"),
  and this change could not measure itself — no session telemetry landed for it.
- **Next time:** Acceptance checks must be **unfakeable by a wrong-but-healthy component** —
  assert the JSON body, or a `POST` → `201` with the created entity, never a bare 200.
- **Time invested:** not measured (source: **manual** — `.telemetry/usage.jsonl` holds no
  sessions for this change; `sessions.jsonl` carries only the startup probe).
- **ADR:** [ADR-0004](../adr/0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) —
  *a verification asserts the observable artifact, not a proxy signal*. Second occurrence of the
  pattern the telemetry retro recorded first (port-free ≠ bytes captured; here, 200 ≠ endpoint
  executed), so it graduates per the rule.

## 2026-07-26 — atlas-shell-adoption

- **Worked:** The design README's "recorded, not implemented" section did exactly what it was
  written for: the entire shell was built from measurements taken before any screen needed them —
  no re-deciding, no fresh screenshots. And
  [ADR-0004](../adr/0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) **paid for
  itself the day after it was written**: canvas-rasterized contrast measurement caught two AA
  failures (dark active-nav at 3.55:1, section labels at 2.75:1) on pages an eyeball pass had
  already approved.
- **Didn't:** The proposal stated a measurement-dependent claim as a fact — "no new token is
  expected" — and the proof failed it twice (`--ls-caps`, `--brand-text`). The spec itself
  invented a metric ("trigger-labelled") that nothing can compute until Automations exist,
  caught only at implementation and amended in place. And the kit prune deleted `.stack`/`.row`
  along with the genuinely dead classes — the rebuild caught it, but removal needed the same
  usage-grep rigour as addition.
- **Next time:** a proposal claim that depends on future measurement is written as a
  **hypothesis with a verification step**, never as a fact — "verify the palette suffices and
  record what is missing", not "the palette needs nothing".
- **Time invested:** not measured (source: **manual** — the second consecutive change with no
  session telemetry; the SessionStart mapping hook is raised as its own spec-less issue).
- **ADR:** none — *a proposal stated a hypothesis as a fact* is a **first** occurrence;
  graduate it if it recurs.

## 2026-07-26 — azure-dev-infrastructure

- **Worked:** The migration gate proved itself under real failure, **twice** — two broken deploys
  stopped before the portal revision moved, and the site was never down. The `ISecretResolver`
  seam paid off exactly as #7 promised: one line changed per host, zero module edits, and zero
  KeyVault packages reachable from a module even transitively. And
  [ADR-0004](../adr/0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) is what made
  "done" mean something — the acceptance check was a `POST` returning 201 with the created
  entity and a `GET` reading it back, not a health endpoint any fallback page could satisfy.
- **Didn't:** **Four defects, none visible from the configuration.** Key Vault and ACR names are
  globally unique and `kv-aio-dev` was already a stranger's. A system-assigned identity
  deadlocks against its own `AcrPull` grant — the app sat `InProgress` for seventeen minutes
  waiting on a permission Terraform was waiting on the app to enable. The migration image needed
  the `aspnet` base despite serving nothing. And nothing bridged a secret *name* to EF Core's
  connection string, because BR-010 resolves per use while EF reads once. Each apply that failed
  left partial state behind, which made every retry slower than the last.
- **Next time:** front-load the cheap reality checks in an infra change — name availability,
  provider registration, identity ordering — instead of meeting each one mid-apply with
  half-created resources on the ground.
- **Time invested:** not measured (source: **manual** — third consecutive change with no session
  telemetry; [#34](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/34) tracks
  the gap).
- **ADR:** [ADR-0005](../adr/0005-a-claim-that-depends-on-verification-is-written-as-a-hypothesis.md)
  — *a claim that depends on verification is written as a hypothesis until verified*. Second
  occurrence of the shape [atlas-shell-adoption] recorded first ("no new token is expected"),
  here as a Dockerfile comment asserting "this process serves nothing" that only running it
  disproved. A different artifact from the first instance, which is what makes it general.

## 2026-07-26 — dispatch-substrate

- **Worked:** The grill caught, before a line of code, that Storage Queue redelivery **is** an
  automatic retry and BR-004 forbids it. That conflict would otherwise have shipped as an
  occasional mystery second job — no error, no failing test, just a Run that ran twice. The test
  guarding the resolution simulates a crash rather than a happy path, so it would fail on an
  at-least-once queue; every other test in that file would pass, which is the point. And the
  storage API version was **probed** rather than guessed: Azurite answers 400 for a version it
  cannot serve and 403 for one it can.
- **Didn't:** Four defects, none visible to `terraform validate`, `az ... show`, or any test.
  Two were mine twice over. I read warning text from a **failed** `az storage message put` as
  success — the precise failure
  [ADR-0004](../adr/0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md) was written
  about, committed two changes after writing it — and then blamed KEDA for correctly doing
  nothing with an empty queue. Then I deployed **two** scale-rule shapes that ARM accepted,
  rendered cleanly, and silently never fired, guessing both times, while
  `error parsing azure queue metadata: no connection setting given` sat in Log Analytics
  throughout. The docs settled it in one fetch once I stopped guessing.
- **Next time:** when a deployed component does not behave, **read its system logs before forming
  a second hypothesis.** The first guess is cheap; the second, made without evidence that is
  already sitting in a log table, is how an hour and a wrong design claim happen.
- **Time invested:** not measured (source: **manual** — fourth consecutive change with no session
  telemetry; [#34](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/34)).
- **ADR:** none. "Read the logs before guessing again" is a **first** occurrence of a distinct
  shape: ADR-0001 says exercise the claim, ADR-0004 says assert the artifact, ADR-0005 says do
  not state a hypothesis as fact — none of them says *where to look when the artifact is wrong*.
  Graduate it if it recurs.
