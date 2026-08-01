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

## 2026-07-26 — telemetry-verification (spec-less lane)

- **Worked:** Diagnosis came from exercising, not reading. Feeding the mapping hook a synthetic
  payload proved in one command that the script is correct and simply never invoked — which
  redirected the search from "the code is wrong" to "the wiring never runs". The environment
  check then found the state nobody had considered: telemetry **enabled with no endpoint**,
  exporting to the OTLP default port, which on this machine belongs to another project's
  collector. Worse than off, because everything looks configured.
- **Didn't:** My own verifier's first draft **passed two checks it should have failed** —
  enabled-without-endpoint read as fine, and a probe row I had just written by hand counted as a
  real mapped session. Both were caught only because the output looked implausibly green. Writing
  a checker is no protection against the failure the checker exists to catch.
  And the deeper defect was procedural: `collect-usage` said *"if telemetry is missing, the entry
  says so (manual)"*, and that documented shrug absorbed four consecutive losses without anyone
  noticing.
- **Next time:** a capability whose failure mode is **silence** ships with its verifier in the
  same change — not after the fourth loss. The design system did this correctly with its drift
  gate; telemetry did not, and the difference is four irrecoverable measurements.
- **Time invested:** not measured (source: **manual** — and this is the change that explains why;
  the fix applies at next process start, so it could not measure itself).
- **ADR:** none new — and that is the finding. This is the **same subsystem failing the same way
  twice**: `fix-telemetry-collector-port` produced
  [ADR-0004](../adr/0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md), and ADR-0004
  applied to the *pipeline as a standing capability* rather than only to the change that touched
  it would have caught this on day one. The rule was right; its scope was read too narrowly.

## 2026-07-26 — automation-configuration

- **Worked:** Reading the corpus before designing saved the change from a wrong turn. BC-001
  already said *"Automations and their validation"* belong to Project Configuration, so the
  instinct to create an Automations module died in thirty seconds rather than in review. The
  grill's real output was narrower and more useful: BR-003 says triggers must not *intersect* and
  never defines intersection, and that one undefined word would otherwise have been settled by
  whatever the first implementation happened to do.
- **Didn't:** The subsumption case — a state-less trigger matching everything a specific one does
  — is the whole rule, and it is exactly the case a unique index on `(label, state)` **silently
  permits**. That index was the obvious first instinct and would have shipped a gate that looked
  enforced and was not. It is now written into `context.md` as a warning, because the next reader
  will have the same instinct. Separately, the screen needed a checkbox the kit did not have; the
  tempting move was a bare native control, which reads as unfinished beside tokenised inputs.
- **Next time:** when a rule is stated in prose and a database constraint *nearly* expresses it,
  write down which cases the constraint would miss **before** choosing. "Nearly" is where the
  silent gap lives.
- **Time invested:** not measured (source: **manual** — fifth consecutive. The fix from
  telemetry-verification landed on `main` in this same session but applies at next process
  start, so this change still could not measure itself).
- **ADR:** none. "A constraint that nearly expresses a rule is a gap, not an implementation" is a
  **first** occurrence — related to ADR-0004 but distinct: that one is about verifying outcomes,
  this is about choosing the enforcement mechanism. Graduate it if it recurs.

## 2026-07-26 — module-integration-events

- **Worked:** The spike-first task order (ADR-0005) paid for itself twice in one afternoon. The
  headline claim — CAP redelivers after a mid-handler crash — was proven rather than assumed,
  but the throwaway pair also surfaced two behaviours no documentation states plainly: the
  fallback processor's 240-second default lookback makes redelivery *look* broken in any short
  test, and a message that exhausts its retries dies **silently** unless a threshold callback is
  registered. Both findings shaped the production composition directly. The rollback functional
  test asserts the artifact itself (no `cap.published` row after an uncommitted transaction),
  which is ADR-0004 applied where it matters most — the entire point of an outbox is a negative.
- **Didn't:** Working-directory drift between shell calls (`cd src` persisting) created a stray
  `src/src/` tree twice this change — and cleaning it up revealed #16 had already **committed**
  one by the same mechanism, unnoticed by every gate since. A misplaced-but-buildable file is
  invisible to CI. Separately, patching a file from memory failed because CSharpier had reflowed
  the committed text; the patch had to be rewritten against what was actually on disk.
- **Next time:** proving a negative ("the no-op poll delivers nothing") needs a **fence** — a
  real committed event published after the silence being asserted, so its arrival bounds the
  wait. Sleeping a fixed interval is the flake; the fence made all three negative assertions
  deterministic. Reuse the pattern wherever at-least-once delivery meets a "nothing happens"
  scenario.
- **Time invested:** not measured (source: **manual** — sixth consecutive. The
  telemetry-verification fix applies at next process start; this session began before it landed
  and has continued through compaction, so it still cannot measure itself).
- **ADR:** none new. The stray-tree finding is tooling hygiene, not workflow; the fence pattern
  is a first occurrence — graduate it if a second negative-assertion flake appears.

## 2026-07-26 — story-automation-matching

- **Worked:** The loop closed on the first full test run — and it did so because every piece it
  stands on had been proven in its own change first: the event substrate's rollback and delivery
  semantics (#41), the queue's wire pinning (#16), the overlap gate (#14). The only integration
  defect the tier found was a composition gap, not a behaviour gap: nothing had ever made the
  Server the dispatch *producer*, so `IRunDispatcher` was simply absent from its container. DI
  validation caught it at host boot — a failure mode measured in minutes. The fence pattern from
  the #41 retro was reused verbatim for every negative assertion, first reuse of a retro finding
  within twenty-four hours of writing it down.
- **Didn't:** The Server gaining a startup requirement (`ConnectionStrings__queues`) silently
  obsoleted the deployed portal's environment — the code change was green everywhere while the
  infrastructure it implied was missing. It was caught by asking "who else composes this?"
  rather than by any gate; nothing in CI relates a host's configuration demands to what the
  Terraform actually provides. The apply itself was left to the operator (human-applies policy),
  so the portal has a window where deploying a new image would crash at startup until
  `terraform apply` runs.
- **Next time:** when a change adds a *fail-at-startup* configuration requirement to a host,
  grep the infra for that host's env block in the same sitting — the requirement and the
  provision must land in the same PR, and the operator note must say "apply before next deploy".
- **Time invested:** not measured (source: **manual** — seventh consecutive; same standing cause
  as the previous entries, the fix has still not seen a fresh process start).
- **ADR:** none new. "A host's startup requirements and the infra that satisfies them belong to
  the same change" is a first occurrence; graduate it if a second config-drift window appears.

## 2026-07-26 — run-visibility

- **Worked:** The strict read slice stayed strict — one GET, no schema change, no Contracts
  widening — because design D1 pushed the automation join to the client, where an endpoint
  already existed. The exact-response-shape test (assert the JSON's field *names*, not the
  deserialised record) is the cheap insurance the empty-value decision needed: an invented
  cost-of-zero would deserialise away invisibly and ship. Visual verification against a locally
  booted stack caught nothing broken, which is itself evidence the kit's composition patterns
  hold — the section is the fourth consumer of the same table/pill/empty-value idioms.
- **Didn't:** The browser pane's screenshot capture would not track scrolling (pane hidden), so
  the themed run-table screenshots are top-of-page only; content verification fell back to the
  page text, which is honest but not visual. And the per-Story filter's click path needs a
  connected backlog the local stack didn't have — stated in tasks 2.3 rather than faked.
- **Next time:** when a page section is only reachable through data another module produces,
  seed that data through the API in the local verify script from the start — the backlog
  connector stub exists in the functional tier but has no local-boot equivalent, which is why
  the click path went unexercised.
- **Time invested:** not measured (source: **manual** — eighth consecutive; same standing cause).
- **ADR:** none new.

## 2026-07-27 — label-write-back

- **Worked:** DEC-027's "equivalent" became structural for free: because the endpoint finishes
  by calling the same `BacklogSynchroniser` the poller uses, the mirror update and the
  `StoryChanged` event needed zero new machinery — the portal-probe test (label via PUT → Run +
  queue message, through the real relay) passed on its first run. The #20 retro's "next time"
  was applied within one change: the local verify seeded the connector row and mirror stories
  directly, which made the browser check of the affordances *and* the visible write-failure
  state a five-minute job.
- **Didn't:** The first UI cut had no error state for a refused write — the mutation failed
  silently, which reads as a broken button. Caught during self-review before the browser check,
  but "every mutation needs a visible failure state" was already the connector card's pattern;
  it should not have needed rediscovering two features later. GitHub's 404-on-remove being
  ambiguous (label absent vs issue gone) forced a semantics choice — resolved toward the
  idempotent no-op because the desired end state holds either way, recorded in design D3.
- **Next time:** when adding a mutation hook, add its visible failure state in the same edit —
  the pattern is established; the omission is the recurring risk.
- **Time invested:** not measured (source: **manual** — ninth consecutive; same standing cause).
- **ADR:** none new. "Mutation hooks ship with their failure state" is a first explicit
  statement of an existing pattern; graduate it if a third silent mutation appears.

## 2026-07-27 — run-now

- **Worked:** BR-013 ("bypasses detection only") turned out to be a *design instruction*, not
  just a constraint: it forced the RunCreator extraction, and the extraction was proven
  behaviour-preserving the cheapest possible way — the existing matching suite ran untouched
  and stayed green. The outcome-as-data shape (Dispatched / QueuedAtCap / AlreadyActive /
  TwoPhaseRefused) let two callers keep opposite voices without duplicating a single rule:
  the handler stays silent where at-least-once makes silence correct, the endpoint answers
  the human with the rule's name. The browser check caught the whole arc: click → dispatched
  (DispatchedAt only set after a real Azurite enqueue), click again → the BR-001 copy.
- **Didn't:** cwd drift struck twice more this cycle (a compound command ran from the wrong
  directory and half-executed) — third change in a row. It costs a retry each time, never
  correctness, but the pattern is now established beyond doubt.
- **Next time:** every multi-command Bash block starts with an absolute `cd` — no exceptions,
  including "quick" one-liners chained onto docker or python heredocs.
- **Time invested:** not measured (source: **manual** — tenth consecutive; same standing cause).
- **ADR:** none new. The cwd-drift discipline is session tooling, not workflow; recorded here
  because the retro is where the pattern's cost is visible.

## 2026-07-27 — agent-runtime-seam

- **Worked:** Writing the spec scenario before the code caught a real defect the suite would
  otherwise have blessed: "a terminal Run frees its Story" failed on first run because the
  BR-001 pre-check predated terminal states and counted every Run as blocking — the index was
  right and the pre-check lied. One state filter later, the pre-check mirrors the index it
  fronts, with a comment binding the two lists together. The hypothesis discipline (ADR-0005)
  also earned its keep in reverse: the CLI could not be run in the authoring session, so
  design D2 says HYPOTHESIS in capitals, the parser is written to degrade to "usage unknown"
  (which BR-011 makes safe), and the in-container half — pinned 2.0.44 answers --version in
  the built image — is recorded as exactly the half that was proven.
- **Didn't:** The old worker's log event ids (3001/3002) silently collided with the Runs
  module's MatchingLog — the uniqueness ArchTest scans module assemblies plus BuildingBlocks
  and never looks at hosts, so the "unique across the solution" claim in the worker's own
  comment was false from the day #17 merged. Renumbered here; the gap in the ArchTest's scan
  remains and is the second silent-scope finding of its kind (telemetry's checker had the
  same shape: a verifier whose blind spot was where the defect lived).
- **Next time:** when a guardrail asserts "across the solution", make its assembly list say
  so — hosts included — or rename the claim to what it actually scans.
- **Time invested:** not measured (source: **manual** — eleventh consecutive; same standing
  cause).
- **ADR:** none new, but the guardrail-scope finding is a candidate on its second appearance;
  ADR-0004's family already covers the principle (a verifier must look where the failure
  lives).

## 2026-07-27 — agent-implements-pr

- **Worked:** run-visibility's em-dash decision paid out exactly as designed: OutputLink was a
  data change — one migration, one API field, one cell — not a UI reshape, and the
  exact-response-shape test flagged the field's arrival as a deliberate edit rather than
  letting it slip in unreviewed. Separating the ceremony from the Agent (design D1) made the
  entire flow testable without a credential: the fake workspace scripts each stage, and BR-005
  runs through the runtime's *real* kill path via a sleeping script behind a documented
  command seam.
- **Didn't:** The first draft of the timeout test tripped CA1416 (SetUnixFileMode on Windows)
  — the platform guard now states the honest scope: the job image is Linux, and there is no
  Windows equivalent worth faking. The deployed proof is still ahead: no credentialed
  end-to-end run has produced a real PR yet; the functional tier proves everything up to the
  seams, and the first deployed run owns the rest (stated, per ADR-0005's discipline).
- **Next time:** when a change's last mile needs a credential the session must not hold, write
  the deployed-verification step into the issue that owns deployment rather than leaving it as
  a retro footnote.
- **Time invested:** not measured (source: **manual** — twelfth consecutive; same standing
  cause).
- **ADR:** none new.

## 2026-07-27 — opencode-runtime

- **Worked:** The grill did what the decision register said it would: OPN-004 closed by
  observation, twice — the real CLI on the authoring machine, then the free-model run repeated
  inside the built worker image with a clean environment, which converted the design's one
  stated hypothesis into fact before the PR merged (a first: every other change shipped with
  at least one hypothesis outstanding). The second runtime also proved the seam the cheap way:
  the executor edit was subtraction (one runtime dependency became a selector), and both
  runtimes ended up sharing one process runner, so BR-005's kill semantics now cannot drift.
  The owner's "there are free models for testing" became a default (`deepseek-v4-flash-free`)
  and a guarantee (the free path performs no vault lookup — asserted by recording every name
  the host resolves).
- **Didn't:** Two id collisions in a row while appending DEC-042 → DEC-043 → DEC-044: the
  locked-decisions file is not numerically ordered, and "read the last entry" is not "read the
  max id". A one-line grep for the max would have avoided both. Also the loop's own trigger
  was ambiguous ("about 29") — one clarifying question fixed it, which is the cheap kind of
  wrong.
- **Next time:** allocating any sequential id (DEC, ADR, OPN) starts with grepping the max
  across the whole file, never the tail — the same rule write-adr already applies to ADRs.
- **Time invested:** not measured (source: **manual** — thirteenth consecutive; same standing
  cause).
- **ADR:** none new.

## 2026-07-27 — story-detail

- **Worked:** The finding that reordered the backlog — the Agent's prompt carried a headline and
  no requirement — turned a "reading convenience" issue into the highest-leverage one available,
  and fixing it was one field: the same mirrored body serves the detail page and the prompt.
  Putting the body in `UpdateFrom`'s comparison means an *edited requirement* now announces
  itself as a `StoryChanged`, which is exactly the change matching should react to. The XSS
  claim was asserted where it is actually a fact — a real browser checking that
  `window.__pwned` was never set — rather than trusted to the sanitiser's reputation.
- **Didn't:** Adding a `.prose` class to the canonical kit, I reached for `--fs-18` and
  `--lh-relaxed`; neither exists. The adherence gate would have caught literals, but *inventing
  a token name that resolves to nothing* fails silently — the CSS just renders unstyled. I only
  noticed because I grepped the token files to check. Separately, a five-field tuple in the E2E
  stub fought the formatter twice before becoming the record it should always have been.
- **Next time:** when writing kit CSS, grep the token files for the variables first — a
  `var(--nonexistent)` is invisible to every gate we have and looks fine in review.
- **Time invested:** not measured (source: **manual** — fourteenth consecutive; same standing
  cause).
- **ADR:** none new. The unresolvable-token gap is a real hole in the design gates; if it
  recurs, the drift validator should assert every `var(--x)` in the kit resolves.

## 2026-07-27 — story-documents

- **Worked:** Keeping the seam's vocabulary product-shaped paid immediately: `FindLinkedChange`
  rather than `GetPullRequest` means #29's Azure DevOps connector answers it from work-item
  relations without pretending to speak GitHub. Reading at the change's head SHA (not the branch
  name) made "the branch moved on" correct by construction, and the stub records which ref the
  read used, so that property is observed rather than trusted. The third duplicate of "resolve
  connector, implementation and credential" was the right moment to extract `ConnectorAccess` —
  two copies is a coincidence, three is a helper.
- **Didn't:** Working-directory drift put the entire change bundle in
  `src/frontend/openspec/changes/` — the fourth incident this session and the first to reach a
  commit, because nothing checks where an OpenSpec bundle lands. Worse, the fix commit swept
  twenty implementation files under a "move the bundle" subject; I split it into four honest
  commits, but only because I read the stat output. The standing "start every block with an
  absolute cd" note from the run-now retro plainly is not sticking as a note.
- **Next time:** `openspec validate <change>` should be run from the repo root immediately after
  the bundle is written — it fails loudly on a bundle that is not under `openspec/`, which is
  the cheap gate this session kept not having.
- **Time invested:** not measured (source: **manual** — fifteenth consecutive; same standing
  cause).
- **ADR:** none new, but cwd drift is now a **fourth** occurrence and has cost a stray committed
  tree (#16), a stray directory (#41), and now a misplaced bundle plus a mislabelled commit. It
  has graduated past "tooling hygiene" — the next occurrence should produce a hook or an ADR
  rather than another retro line.

## 2026-07-27 — approval-gate

- **Worked:** Making the lane split at *execution* rather than creation (design D1) meant the
  gate needed no fifth state, no new index, and no change to the cap query — approval is an
  `ApprovedAt` stamp plus a re-enqueue, and the worker routes on the record it already reads.
  Writing the three easy-to-break rules into one test (BR-006 untimed, BR-002 no cap slot,
  BR-001 Story still held) was worth more than three separate ones: they are only interesting
  *together*, because the natural bug is treating the two phases as one long Run. And insisting
  the approved Plan ride into phase 2's instruction turned approval from a UI gesture into a
  contract.
- **Didn't:** The new tests found that the BR-001 pre-check's hand-copied state list had never
  learned about `Cancelled`, so a rejected Run held its Story forever — the **second** drift of
  that same copy (the first was terminal states in agent-runtime-seam). Fixing the instance
  twice was the mistake; it is now one `RunStates.Active` array that also generates the index's
  SQL filter, so a future state cannot be added to one and forgotten in the other.
- **Next time:** when a database constraint and application code must agree on a set, generate
  one from the other. Two hand-maintained copies of the same list is not duplication to tidy
  later — it is a defect with a delay fuse, and this one went off twice.
- **Time invested:** not measured (source: **manual** — sixteenth consecutive; same standing
  cause).
- **ADR:** none new. "Derive the constraint and the query from one definition" is closely
  related to ADR-0003 (one owner per derived artifact) and is arguably an instance of it —
  worth folding into that ADR's examples rather than writing a new one.

## 2026-07-27 — run-file-changes

- **Worked:** The grill's finding that the data was *already fetched and discarded* turned a
  feature into a projection: `ListChangeDocuments` had been calling `PullRequest.Files` and
  keeping only markdown names, so this change removed a round-trip rather than adding one.
  Typing the omission (`Binary` / `TooLarge`) instead of returning an empty patch made the
  honest behaviour the only expressible one — there is no code path that can produce a
  truncated diff, because the seam has nowhere to put one. And the previous retro's "grep the
  tokens first" became a check across the *whole* kit rather than the new classes alone; it
  passes today, which is worth knowing since nothing had ever verified it.
- **Didn't:** Four `ShouldContain` nullability errors in three consecutive changes now — the
  same fix each time (`!` after a nullable projection). It costs one build cycle every time and
  is entirely predictable from the record type's own nullability.
- **Next time:** when a test projects nullable columns into a record, write the assertions with
  the null-forgiving operator as you type them; the compiler will demand it regardless.
- **Time invested:** not measured (source: **manual** — seventeenth consecutive; same standing
  cause).
- **ADR:** none new.

## 2026-07-27 — local-agent-loop

- **Worked:** The change existed because a resource had been mis-wired for four changes with
  nothing watching, so the fix that matters is not the database reference — it is the two E2E
  tests that now assert the *composition itself*. When driving `aspire run` by hand proved
  unreliable from this session, moving the exercise into the E2E tier turned a one-time manual
  observation into something CI re-checks forever; that is strictly better than what task 3.1
  originally asked for. Keeping the drain pass byte-identical between local and deployed, and
  letting only the *trigger* differ (timer vs KEDA), meant the local loop proves the real
  execution path and nothing false about scaling.
- **Didn't:** Three separate verification failures stacked in one afternoon, and each nearly
  ended in a wrong conclusion. (1) A Docker Hub outage reddened CI; diagnosing it as a flake was
  right, but stopping there would have been wrong — the re-run is what exposed the real bug
  underneath. (2) `gh run rerun --failed` printed nothing and did nothing; I only noticed
  because the run list had no new entry, having nearly reported "re-running" while nothing was
  queued. (3) My new E2E tests lacked `[Trait("Category", "E2E")]`, so they ran in the job that
  has no Playwright browser — invisible locally because I ran `dotnet test` unfiltered while CI
  runs `--filter "Category!=E2E"`. And cwd drift buried the OpenSpec bundle under
  `src/frontend/` for the third time in this project.
- **Next time:** when a change adds tests to a tier CI treats specially, run **CI's exact
  command** locally before pushing — `dotnet test --filter "Category!=E2E"` here. "It passed
  locally" is only evidence if the local invocation is the same one.
- **Time invested:** not measured (source: **manual** — eighteenth consecutive; same standing
  cause).
- **ADR:** none new — but the cwd-drift finding finally produced its guard instead of another
  line here: `.husky/pre-commit` now refuses a staged OpenSpec bundle outside the root, and the
  guard was proven against the real case before committing. The retro promise from run-now is
  discharged.

## 2026-07-27 — automation-editing

- **Worked:** Writing acceptance criterion 5 — "in-flight work is untouched" — before the code
  found a defect the CRUD would otherwise have shipped: `IAutomationCatalog.Detail` filtered on
  `Enabled`, so disabling an Automation failed any Run already executing, the exact opposite of
  what UC-006 says. Nothing in the issue's original text would have caught it; the criterion
  did, because it described a behaviour rather than a feature. Extracting one `OverlapGuard`
  for create/edit/enable also kept #14's subsumption finding from needing a second
  implementation to rediscover it.
- **Didn't:** Nothing went wrong mechanically this cycle, which is itself the observation worth
  recording: it is the first change in a while where the retro's standing lessons (run CI's own
  filtered command, grep before assuming, guard rather than promise) were applied *before* the
  mistake rather than after it.
- **Next time:** keep writing criteria as behaviours the system must exhibit under change
  ("editing X does not disturb Y"), not as features to add. Two of the last three genuine
  defects came out of exactly that phrasing.
- **Time invested:** not measured (source: **manual** — nineteenth consecutive).
- **ADR:** none new.

## 2026-07-27 — run-cancellation

- **Worked:** The guard added one change earlier caught cwd drift at commit time — the fourth
  occurrence in this project, and the first that never reached a commit. Three retros had
  promised that guard; building it finally converted a recurring apology into a property. On
  the change itself, refusing to fake BR-012 was the right call: "terminate the job" is not
  something a portal holding no handle on a KEDA-started job can do, so the design says
  cooperative cancellation and the residual gap is written down in two places rather than left
  for someone to discover.
- **Didn't:** The mid-flight test failed on first run and was right to. My cancellation check
  sat *after* `Invoke` returned — but `Invoke` publishes, so a cancelled Run would still have
  opened a pull request while showing `Cancelled`. The design text said "before publishing" and
  I implemented it one call level too high; only a test that asserted the *consequence*
  (`Published == false`) rather than the state caught it.
- **Next time:** when a design says "before X", put the check in the frame that performs X, not
  in its caller. A boundary described in prose lands in the wrong place surprisingly easily.
- **Time invested:** not measured (source: **manual** — twentieth consecutive).
- **ADR:** none new.

## 2026-07-27 — run-cost-visibility

- **Worked:** The grill corrected the *issue* rather than the code. #18 had already persisted
  tokens and cost and already nulled a missing report, so UC-020's storage half shipped changes
  ago; had the issue been implemented as written, someone would have rebuilt it. Reading the
  code before believing the backlog is what turned a feature into a display change. The
  distinction that carries the work — `0.00` reported versus nothing reported — only became
  load-bearing when #30 introduced free models, and it would have been invisible to anyone
  writing this issue before that.
- **Didn't:** A fifth `ShouldContain`/`Single` nullability error, same shape as the previous
  four. The lesson from the run-file-changes retro ("write the null-forgiving operator as you
  type") has not stuck because it is a habit, not a gate.
- **Next time:** the pattern is mechanical enough to notice while writing: any assertion chained
  off a nullable-returning helper in these functional tests needs `!`. If it recurs a seventh
  time, make the helpers return non-nullable and throw instead — the compiler is asking for
  something the test always wants anyway.
- **Time invested:** not measured (source: **manual** — twenty-first consecutive).
- **ADR:** none new.

## 2026-07-27 — agent-actions

- **Worked:** Batching three issues into one change was right: they shared a mechanism, and
  writing one design meant the "unusable answer" rule (no invented estimate, no guessed state)
  was decided once and applied three times rather than three times with drift. Asking the owner
  where an estimate lives — rather than inventing a Projects v2 integration — kept a
  three-action change small.
- **Didn't:** The replace-the-estimate test failed and was right to. The first implementation
  read a Story's current labels from the **Mirror**, which is a poll behind, so a second
  estimate before the next refresh left both labels on the Story. It would have worked most of
  the time in production, which is the worst way to be wrong. BR-008 already says the vendor is
  the source of truth; I read our copy because it was closer to hand.
- **Next time:** before a write that depends on current state, ask which copy of that state is
  authoritative *at this instant* — the Mirror is authoritative for reading a backlog and never
  for deciding a write that follows another write.
- **Time invested:** not measured (source: **manual** — twenty-second consecutive).
- **ADR:** none new. "Read the vendor when a write depends on a previous write" is a specific
  instance of BR-008 rather than a new rule.

## 2026-07-27 — webhook-ingest

- **Worked:** BR-015 decided the design rather than being checked against it afterwards. Once
  "webhook and polling events must be identical" is taken seriously, parsing the payload is
  visibly the wrong answer — it builds the second path the rule exists to prevent — and
  "trigger the same reconciliation" makes the property structural. The test then demonstrates
  it instead of asserting it: the payload names no stories, yet the mirror fills, which is only
  possible if the reconciler did the work. Every test passed on the first run, which for a
  change with five refusal cases is worth noting.
- **Didn't:** Nothing went wrong. Worth recording *why*: this is the first change where the
  hardest decision (payload-as-data versus payload-as-hint) was settled in the grill with the
  rejected option written down, so implementation had nothing left to discover.
- **Next time:** keep writing the rejected alternative into the issue. Three changes now
  (#38's path convention, #23's control-plane kill, this one's payload parsing) went smoothly
  because the tempting-but-wrong design was named and dismissed before any code existed.
- **Time invested:** not measured (source: **manual** — twenty-third consecutive).
- **ADR:** none new.

## 2026-07-27 — azure-devops-connector

- **Worked:** the seam paid for itself exactly where it was supposed to. Adding a whole second
  vendor touched no caller: not the poller, not the reconciler, not matching, not the API. The
  result worth keeping is the guardrail suite passing *with two implementations present* — until
  now "no vendor SDK escapes its implementation" was a rule enforced against a single vendor,
  which proves very little. Closing OPN-003 also turned out to be mostly a matter of refusing to
  decide: the honest answer for state vocabulary was to keep passing the vendor's own value
  through, which is what the seam already did, and having two real vendors in hand is what made
  that defensible rather than lazy.
- **Didn't:** the change was one inspection away from shipping unreachable. Everything was green
  — build, 23 unit tests, the guardrails — and `ConfigureConnector` still hardcoded
  `const BacklogVendor vendor = BacklogVendor.GitHub`, so no Azure DevOps Connector could be
  configured. Worse, checking the same pattern elsewhere found that **#30 already shipped this
  bug**: the portal's runtime picker is still `disabled` with a comment saying "one runtime until
  OPN-004 closes", and OPN-004 closed in that change. opencode has been unreachable from the
  portal since it merged. Second occurrence, so it graduates: **ADR-0006**. My first fix was also
  wrong in an instructive way — it fell back to GitHub on an unparseable vendor string, which
  turns a typo into a Connector that verifies an Azure DevOps organisation against github.com.
  Silent fallback is worse than the hardcoding it replaced.
- **Next time:** before implementing behind an existing seam, trace the path from the form
  control or HTTP request down to the seam and list every place the first implementation's
  uniqueness was assumed. It takes minutes and it is the only step that would have caught either
  incident, because both defects lived in code the change never modified.
- **Time invested:** not measured (source: **manual** — twenty-fourth consecutive).
- **ADR:** [ADR-0006 — A capability is not added until a user can reach it](../adr/0006-a-capability-is-not-added-until-a-user-can-reach-it.md).

## 2026-07-27 — enable-runtime-picker (spec-less, DEC-025)

- **Worked:** ADR-0006 was written and then immediately paid for itself — the ADR named the
  check, and the check found that the incident it was written about was still live in `main`.
  The new E2E assertion was verified the only way an omission-catching test can be: the bug was
  re-introduced and the test went red on exactly the right assertion, then removed and it went
  green. A test that has never failed for the reason it exists is a test nobody has checked.
- **Didn't:** this should not have been a separate change. opencode shipped unreachable in #30
  and stayed that way through four subsequent merges, every one of them green. The gap was not
  the fix, which is one word — it was that nothing in the pipeline asks "can a person get to
  this?", and four reviews did not think to.
- **Next time:** treat a comment naming an open `OPN-*`/`DEC-*` as a stale-marker. Both incidents
  had one sitting directly above the defect ("one runtime until OPN-004 closes", "GitHub is the
  only vendor until OPN-003 closes") and in both cases the decision had already closed. Grepping
  for those references when a decision closes is cheap and mechanical — done here, and it found
  three more: `Story.State`, `Automation.TriggerState`, and the `AgentRuntime` enum's summary,
  which still said "One value" with two values in it. None was a bug, but all three told the next
  reader a decision was pending when it was made.
- **Time invested:** not measured (source: **manual** — twenty-fifth consecutive).
- **ADR:** none new — this change is the first application of
  [ADR-0006](../adr/0006-a-capability-is-not-added-until-a-user-can-reach-it.md).

## 2026-07-27 — deploy-from-ci (spec-less, DEC-025)

- **Worked:** the blocker turned out to be better than the thing it blocked. `terraform apply` is
  denied to me at the permission layer and the owner had no terminal, which looked like a dead
  end — but "nobody can reach dev" is a much better problem to solve than "one person can". The
  fix removes the terminal from the path entirely rather than widening a permission, and it
  reuses `deploy.sh` instead of restating it, so the release ordering still has one owner. The
  plan/deploy job split came from taking the gate seriously: an Environment approves a whole job,
  so a single job would have asked for approval of something not yet computed.
- **Didn't:** D7 had been quietly false for a while before anyone noticed — CI *should* have no
  credentials was written as CI *has* none and applying is human, in three places, and the second
  half stopped being a safeguard the moment the human lost terminal access. A posture that
  depends on an unstated assumption about the operator is not a posture. Also found `tfplan`
  missing from `.gitignore`: the section right above it exists entirely to keep the subscription
  id out of a public repo, and a saved plan carries it under a filename that looks innocuous.
- **Next time:** when a rule says "X is a human action", write down *which* human and *with what
  access*. Both D7 and the OPN-002 Entra blocker assume owner capabilities that were never
  verified; one of them has now cost a full stop.
- **Time invested:** not measured (source: **manual** — twenty-sixth consecutive).
- **ADR:** none new. DEC-046 supersedes design D7; the pipeline is labelled unexercised per
  ADR-0005, which is the existing rule doing its job rather than a new one.

## 2026-07-27 — ci-identity-script (spec-less, DEC-025)

- **Worked:** the owner pushed back on my output rather than accepting it — "¿y si hacemos un
  sh?" — and they were right. Turning the README's command list into a script surfaced three
  defects that prose had been hiding: a hardcoded storage account name that the sibling script
  derives from a hash, no existence checks, and no subscription guard on the one action that
  hands CI power over a resource group. None of those were visible as *text*; they became obvious
  the moment the question was "what happens on the second run".
- **Didn't:** I wrote the manual steps first and only scripted them when asked. `infra/` already
  had two idempotent, confirming, guard-checking scripts — the convention was established and I
  documented against it instead of following it. Writing instructions for a human to execute is
  the reflex; it is also the version that cannot be tested.
- **Next time:** if a sequence of commands is going into a doc, ask whether it belongs in a
  script *in that repository's existing shape* before writing the prose. The tell here was that
  the answer to "how do I do X" was six numbered steps rather than one command.
- **Time invested:** not measured (source: **manual** — twenty-seventh consecutive).
- **ADR:** none new. This is the existing `infra/` convention being applied, not a new rule.

## 2026-07-27 — one-approved-deploy-job (spec-less, DEC-025)

- **Worked:** the first real run failed in the most useful possible way — an auth error whose
  message contained the whole diagnosis, on the very first step that touched Azure, before
  anything could be changed. And the repair was subtraction. Both instincts (add a second
  credential, add a second identity) would have shipped a standing unattended path to the
  subscription; deleting the unattended job removed the category. The identity the owner had
  already created needed no change, which is the tell that the credential design was right and
  only the workflow was wrong.
- **Didn't:** I invented a requirement — "the approver should see the plan first" — and let it
  drive the architecture without checking what it cost. It cost the one property the whole
  design existed for. Worse, the flaw was visible in the YAML: a job with no `environment` and a
  credential scoped to one cannot both be correct, and I wrote them ten lines apart. `terraform
  validate` and a YAML parse cannot catch a claim about *which identity a job gets*, so nothing
  in CI was ever going to tell me.
- **Next time:** when a workflow authenticates, write down the token subject each job will
  present and compare it to the credential, before running anything. It is one line of reasoning
  per job and it is the only check that would have caught this without burning a deploy.
- **Time invested:** not measured (source: **manual** — twenty-eighth consecutive).
- **ADR:** none new. This is ADR-0005 working as intended — the pipeline was labelled unexercised,
  the first exercise found the defect, and nobody was surprised.

## 2026-07-27 — ci-identity-subject (spec-less, DEC-025)

- **Worked:** three deploy attempts, three genuine defects, each surfaced by running the thing
  and none by review — which is ADR-0005's whole argument, now with receipts. The failures also
  arrived in a good order: auth before plan before apply, so nothing was half-changed at any
  point. And two of the fixes were *removals*: deleting the unattended job, and deleting the
  script's assumption that it knew GitHub's subject format.
- **Didn't:** the pattern across all three is the same and it is mine. I asserted things I could
  have asked: the subject format (GitHub publishes it at
  `actions/oidc/customization/sub`), the role needed for Key Vault (Terraform's own resources
  say it manages secrets, so of course it reads them), and which token a job without an
  `environment` receives. Each was one API call or one careful read away. The script also
  contained the same class of bug I keep writing ADRs about: it checked the credential's *name*
  and reported success, so a wrong subject would have been reported as fine forever.
- **Next time:** for anything involving a credential, write the exact string each side will
  present and compare them on paper before running. Three deploys and roughly an hour went to
  discovering, one layer at a time, that two strings differed.
- **Time invested:** not measured (source: **manual** — twenty-ninth consecutive).
- **ADR:** none new. ADR-0004 already covers "existence is not correctness"; this is its third
  instance and it is worth watching — if a fourth appears, the rule needs a check rather than
  another retro line.

## 2026-07-27 — deploy-pipeline-exercised (spec-less, DEC-025)

- **Worked:** the pipeline is green and the portal answers `201` to a real write, which exercises
  app, database and vault together — the check this repo insists on precisely because a `200`
  from any URL proves nothing. Four attempts, four defects, all found by running. The order they
  arrived in was kind: auth, auth, plan, then success, so nothing was ever half-applied.
- **Didn't:** I wrote two predictions into the README about what would fail — `ARM_USE_OIDC` and
  an *AcrPush* grant — and **both were wrong**, while the three things that actually failed were
  not on the list. Predicting is still right; the lesson is that a written prediction is a
  hypothesis to be checked off, not a shortlist that makes other causes less likely. Keeping them
  in the file after they were disproved would have been worse than not writing them.
- **Next time:** when a doc says something is unverified, treat the moment it *becomes* verified
  as part of the same change, not as tidying. This entry exists because "has never run" outlived
  its truth by exactly one run — the ADR-0006 failure mode, caught in minutes only because that
  ADR was written this morning.
- **Time invested:** not measured (source: **manual** — thirtieth consecutive).
- **ADR:** none new.

## 2026-07-27 — automation-defaults

- **Worked:** the grill asked what the button creates, and the answer moved the whole change. "Also
  the labels" sounded like a detail and was the actual work: the seam could apply a label to a
  Story but never create one in a repository, so a trigger nobody had used yet was invisible in
  the vendor's own interface. The feature would have shipped looking complete and been useless
  for the case it exists for — a project nobody has labelled anything in yet. The test that pins
  it asserts the *absence* of a change: labels reach the repository while the single Story keeps
  an empty label list.
- **Didn't:** I put an acceptance criterion in the issue citing BR-002 as an Automation cap. BR-002
  caps concurrent **Runs**; no Automation cap exists anywhere. Had I not checked, the honest
  reading of my own issue would have been to build the limit — inventing a product constraint
  inside a change about seeding defaults, which is precisely what RULE-005 forbids. Citing a rule
  by number felt like rigour and was the opposite: I never opened the file.
- **Next time:** when an issue cites a `BR-*`, read the rule's sentence into the issue rather than
  its number. A number cannot be wrong on inspection; a sentence can.
- **Time invested:** not measured (source: **manual** — thirty-first consecutive).
- **ADR:** none new. ADR-0006 was applied rather than extended: the E2E reachability test now
  covers this button, so the capability cannot ship unreachable.

## 2026-07-28 — conversational-runs

- **Worked:** the design was mostly recognition, not invention. "A Run waiting on a human" already
  existed as the approval gate; generalising it meant the container lifecycle, the untimed wait
  and the requeue-to-resume all came free, and the migration regenerating BR-001's index from
  `RunStates.Active` — the mechanism built after two drift incidents — did its job silently on
  the first state added since. The marker insight (one PAT means the agent and the human can be
  the same account, so authorship cannot separate question from answer) came from reading DEC-030
  rather than from a bug, which is the cheap time to find it.
- **Didn't:** a Python edit asserted on its second anchor *after* building the new interface text
  but before writing the file, so the whole edit silently never happened — and the build stayed
  green for twenty minutes because the implementations were just extra public methods no contract
  demanded. I caught it only because "0 implementations in the stubs and a green build" cannot
  both be true. A multi-step scripted edit that can fail between steps must write after each
  step or verify after the last; "the script ran" is not "the file changed" (ADR-0004's shape,
  in tooling).
- **Next time:** after any scripted edit to an interface or contract, grep the file for the new
  member before building — one line, and it catches the half-applied edit before the compiler's
  green can lie about it.
- **Time invested:** not measured (source: **manual** — thirty-second consecutive).
- **ADR:** none new.

## 2026-07-28 — grill-action

- **Worked:** #78's foundation held without a single change — the grill consumed `AskAndWait`,
  the resume checker and the marker exactly as shipped, which is what a Foundation/Product split
  is supposed to buy. The chain test is the one worth keeping: a second Automation triggered by
  the ready label fires through refresh and ordinary matching, proving grill→next-action needs
  no orchestration code — DEC-027's both-sides labelling doing work it was designed for two
  months before this feature existed. And the first-line verdict contract (READY or questions,
  verbatim) meant the failure mode of a rambling model is a human reading slightly odd
  questions, never a wrong state.
- **Didn't:** `UpdateTo` with optional parameters was a trap I set and then caught in the same
  hour — the existing edit path would have silently nulled a grill's settings on any edit,
  because C# fills omitted optional arguments with their defaults. The #29/#30 unreachability
  class again, but as data loss: everything compiles, every test passes, and the capability
  quietly breaks on the first unrelated edit. Caught only because I asked "who else calls this?"
  after changing the signature.
- **Next time:** when adding optional parameters to a mutator, check every existing caller by
  hand — an optional parameter is an invisible edit to all of them. Defaulting new state to
  "preserve" is safer than defaulting to null when any caller mutates existing rows.
- **Time invested:** not measured (source: **manual** — thirty-third consecutive).
- **ADR:** none new. DEC-048 records the catalogue revision.

## 2026-07-28 — propose-action

- **Worked:** the cheapest change of the three, exactly as the slicing predicted — propose is
  implement with a different prompt and PR framing, and the refusals were the only new logic.
  Putting both refusals before `Prepare` made them assertable as absences: the test's evidence
  is a workspace that never existed and an instruction list that stayed empty, which is stronger
  than any assertion about what did happen. The three-issue chain (#78 foundation → #79 consumer
  → #80 independent sibling) never blocked on itself: propose touched nothing the other two
  built.
- **Didn't:** nothing failed. The reason worth recording: every hard decision was already made
  and written down before this change started — the wait machinery in #78's design, the verdict
  contract in #79's, the pipeline reuse in this one's proposal. Third consecutive change where
  implementation discovered nothing the grill and the specs had not.
- **Next time:** keep sizing the last slice of a chain to be the boring one. The temptation is
  to save something interesting for the end; the end is where fatigue lives.
- **Time invested:** not measured (source: **manual** — thirty-fourth consecutive).
- **ADR:** none new.

## 2026-07-28 — delete-automation

- **Worked:** the design came from reading history rather than deciding fresh. "Should deleting
  be allowed?" was already half-answered by BR-014 (Runs record their Automation, forever) and
  fully answered by #14's finding that the executor resolves the Automation *mid-Run* — the
  reason `Detail` stopped filtering on `Enabled`. A hard delete is that bug with no undo, so the
  refusal wrote itself. The test that proves it is the one that matters: after a refused
  deletion, the in-flight Run still runs to Succeeded.
- **Didn't:** nothing went wrong, but one thing was closer than it looked. The Runs module had
  never published a contract — it was a pure leaf — and I nearly put the usage query in the
  Projects schema as a counter to avoid creating the assembly. That would have been a second
  source of truth for something one `COUNT(*)` answers exactly, invented purely to dodge a
  five-line project file.
- **Next time:** when a rule seems to need inventing, grep the retro log and the business rules
  first. Three of this change's four decisions were already made; I only had to find them.
- **Time invested:** not measured (source: **manual** — thirty-fifth consecutive).
- **ADR:** none new.

## 2026-07-28 — defaults-full-catalogue

- **Worked:** the owner caught this, not the tests, and that is the finding. Every test of the
  defaults button asserted four Automations — so growing the catalogue twice left the button
  stale while the suite stayed green, because the tests encoded the number rather than the
  relationship. The fix asserts one Automation *per catalogue action*, which fails the next time
  someone adds a seventh and forgets. Also: seeding propose on the grill's output rather than on
  its own name kept a single truth about the ready label, and turned six triggers into a
  pipeline.
- **Didn't:** #79's issue said "adding \`ai:grill\` to the defaults is a trivial follow-up once
  this exists" — I wrote that, shipped #79 and #80, and never did it. An out-of-scope note is a
  promise nobody tracks; it should have been an issue at the moment I wrote the sentence. Two
  changes went by, and the shop-window feature advertised a catalogue the product had outgrown.
- **Next time:** when a proposal's out-of-scope section says "trivial follow-up", open the issue
  in the same breath. The cost is thirty seconds and it converts a sentence nobody re-reads into
  something the backlog carries.
- **Time invested:** not measured (source: **manual** — thirty-sixth consecutive).
- **ADR:** none new.

## 2026-07-28 — dev-unattended (spec-less, DEC-025)

- **Worked:** the change itself is one API call and one paragraph. What is worth keeping is the
  diagnosis: this file has now carried a stale claim about the deploy setup three times (#74's
  "never run", #75's reviewer sentence, this one), and it is not carelessness. The environment's
  reviewers live outside version control **on purpose**, so the gate can be tightened without a
  pull request — which means no diff ever forces the prose to keep up. The fix is not "be more
  careful"; it is to mark the paragraph as unverifiable-by-commit and name the command that
  checks it.
- **Didn't:** I corrected this same sentence three days' worth of changes ago and did not think
  to ask why it had gone stale in the first place. Fixing an instance twice before looking for
  the mechanism is one time too many — the second occurrence is the signal, which is exactly the
  graduation rule ADRs use.
- **Next time:** when a document states something whose source of truth is outside the
  repository, say so in the document and give the command that reads the real value. Prose that
  cannot be checked by CI should at least tell the reader how to check it.
- **Time invested:** not measured (source: **manual** — thirty-seventh consecutive).
- **ADR:** none new — the note in the file is the mechanism, and a rule about one paragraph does
  not need an ADR yet. If a third document develops the same problem, it does.

## 2026-07-28 — dispatch-worker-database (spec-less, DEC-025)

- **Worked:** the diagnosis, and only because the logs were shaped to allow it. Two lines —
  `Claimed run X` then `pass complete: 1 claimed` with nothing between — narrowed the fault to
  "the executor never reached its own guards" in one read, because every early return in
  `Execute` logs. Comparing the three workloads' environment variables side by side then made
  the cause a table rather than a theory: the portal and the migration job have the database,
  the dispatch job does not.
- **Didn't:** two things, and the second is worse. **One:** the deployed worker has been unable
  to execute anything since it was deployed, and nothing noticed — the job reports *Succeeded*,
  because claiming works and the failure is silent. Every dispatched Run in dev is orphaned in
  `Queued` with its Story blocked, and BR-004 means nothing will retry. **Two:** I tried to close
  the gap with an E2E assertion, wrote one, and it passed against a deliberately broken worker —
  twice, for two different reasons. The second attempt rested on "the worker should not be
  Exited", which is false: locally the worker legitimately exits after one pass unless run mode
  sets a poll interval. I removed it rather than ship an assertion whose premise I could not
  establish.
- **Next time:** a test that cannot be made to fail on purpose is not evidence yet, and the
  honest move when the mutation passes is to keep digging or delete the test — not to keep it
  because it is green. The existing local-loop test carries a docstring claiming it proves the
  worker "got past composition"; it proves the process reached Running, which a crashing process
  also does. That claim needs revisiting with an assertion that can actually fail.
- **Time invested:** not measured (source: **manual** — thirty-eighth consecutive).
- **ADR:** none new. This is ADR-0004's third instance in a week (existence as a proxy for
  correctness) and ADR-0006's fourth (complete but unreachable). If either recurs again the rule
  needs a gate, not another retro line.

## 2026-07-28 — deploy-the-dispatch-worker (spec-less, DEC-025)

- **Worked:** comparing the running image tags found in one command what two rounds of log
  reading had not. The portal was on today's commit, the worker on one from days earlier, and
  the worker's silence — `Claimed` then `pass complete`, nothing between — is precisely the
  behaviour of the build that predates `executor.Execute`. The deploy now ships the worker and,
  more importantly, reads back what is *running* and refuses to claim success unless both images
  carry the tag it just pushed.
- **Didn't:** I diagnosed #90, found a genuinely missing env var, fixed it, and reported the
  symptom as explained — **and it was not the cause**. I verified by comparing configuration
  between workloads instead of watching a Run execute, which is the exact substitution ADR-0004
  exists to forbid, made by the person who has cited that ADR four times this week. The owner
  had to run a second Run to show me I was wrong.
- **Next time:** when a fix is meant to change observable behaviour, the verification is the
  observable behaviour. "The configuration now matches" is a reason to expect success, never
  evidence of it. Had I dispatched a Run after #90 rather than reading env vars, this would have
  been one investigation instead of two.
- **Time invested:** not measured (source: **manual** — thirty-ninth consecutive).
- **ADR:** none new, and that is now a deliberate call worth revisiting: ADR-0004 has been cited
  in four retros this week and violated in this one. The next occurrence should add a gate rather
  than a fifth retro line — the deploy's image read-back is the first such gate.

## 2026-07-28 — mit-license (spec-less, DEC-025)

- **Worked:** the smallest change of the week carries the largest scope: one file makes the
  ambition legal, and DEC-049's second half turns a conversation (Orbion, Dapr, self-host) into
  an evaluation criterion the corpus can enforce. Locking "MIT" and "self-hostability is a goal"
  as one decision was deliberate — the license without the goal is paperwork.
- **Didn't:** the repository was public for a month without a license, which means a month of
  "open source" that legally was not. Nobody noticed because nobody outside tried to use it —
  the same silence that hid the stale worker image. Externally-visible claims need external
  eyes or explicit checks; we keep relearning this with different nouns.
- **Next time:** when a repo goes public, LICENSE is part of the going-public change, not a
  follow-up. It is one file; there is no excuse for the gap.
- **Time invested:** not measured (source: **manual** — fortieth consecutive).
- **ADR:** none new. DEC-049 is the artifact.

## 2026-07-28 — waiting-inbox

- **Worked:** the borrowed shape survived contact with our rules and got better for it. Orbion's
  inbox is a list; ours had to answer "when does an entry LEAVE?", and BR-013/BR-014 forced the
  interesting answer — a failure exits when a newer Run exists for its Story, derived by query
  because both re-trigger paths would forget a flag. The six functional tests are mostly about
  subtraction, which is the property that keeps inboxes alive.
- **Didn't:** the typed i18n catalogue rejected a `Record<_, string>` lookup table — annotating
  the record erased the literal types the catalogue exists to check. `as const satisfies` is
  the pattern; one compile error, thirty seconds, but worth recording because every future
  reason-vocabulary map will hit it.
- **Next time:** when borrowing a UI shape from another product, ask what rule of OURS governs
  its lifecycle before building it. The inbox's value turned out to live in the exit conditions,
  which Orbion's version does not need and ours cannot skip.
- **Time invested:** not measured (source: **manual** — forty-first consecutive).
- **ADR:** none new.

## 2026-07-28 — frontend-mock-mode (spec-less, DEC-025)

- **Worked:** the exclusion is asserted at the artifact, both ways. The build greps the emitted
  bundle for the marker and fails if present; the mutation check builds `--mode mock` and
  verifies the marker IS there. Vite's build-time MODE replacement plus a dynamic import gives
  dead-code elimination of the whole module — but the assertion is what makes that a property
  instead of a belief (ADR-0004, applied for once *before* an incident).
- **Didn't:** three small stumbles, all config-shaped: `noUncheckedIndexedAccess` on a fixture
  array, eslint's browser globals rejecting a Node script, and prettier needing a pass over new
  files. None interesting alone; together they are the tax of a strict frontend, which is the
  point of having one.
- **Next time:** when adding a Node-side script to a browser-linted package, put it under an
  ignored `scripts/` from the start.
- **Time invested:** not measured (source: **manual** — forty-second consecutive).
- **ADR:** none new.

## 2026-07-28 — connector-health

- **Worked:** the narrowest-read principle produced the best assertion of the change: the
  response excludes even the secret NAME, and the test greps for the field's absence. Also the
  #7 lesson (materialise before ToString in EF projections) was applied at write time instead of
  being re-learned from a failing ordinal test — the first time one of the numbered lessons
  preempted its own recurrence.
- **Didn't:** nothing failed. Smallest change of the set, exactly as sliced.
- **Next time:** keep pairing a visual borrow (Orbion's dots) with a rule of ours (BR-008's
  deliberate staleness) — the pairing is what turned a cosmetic pill into four states with a
  reason to exist.
- **Time invested:** not measured (source: **manual** — forty-third consecutive).
- **ADR:** none new.

## 2026-07-28 — live-run-following

- **Worked:** the transport debate (four options, two conversations with the owner, SignalR
  matured and then deliberately not chosen) paid off as a *smaller* implementation: Postgres
  chunks meant BR-014 needed no reconciliation story, the crash test is one assertion, and
  every habitat DEC-049 cares about works without a new resource. Deciding against the exciting
  option, in writing, with the upgrade path recorded, is what let the boring option be chosen
  guiltlessly. The stub runtime forwarding lines meant the tests drive the real writer.
- **Didn't:** two formatting stumbles (IDE0055 needing csharpier before build; a Python edit
  aimed at pre-format code). Trivial, but the second is a recurring shape: scripted edits
  against files the formatter will rewrite should run AFTER formatting, or anchor on
  formatting-stable text.
- **Next time:** when a lag budget is promised, name its constants in code the way this change
  did (FlushInterval + the poll literal) — an inspectable budget beats a measured-once claim.
- **Time invested:** not measured (source: **manual** — forty-fourth consecutive).
- **ADR:** none new. DEC-050 carries the decision.

## 2026-07-28 — self-host-distribution

- **Worked:** ADR-0001 earned its keep six times in one change. Six defects between "the publish
  pipeline succeeded" and "a stranger's machine runs this", every one found by exercising and
  invisible to reading: the build-only validation, publish-mode bicep from the storage emulator,
  the path-hashed volume name, image placeholders expecting a deploy step, the random host port,
  and — findable only by an actual boot — the missing database and the missing health gate,
  because Aspire quietly does both under `aspire run`. The final boot also proved #90's
  refuse-to-start guard in a third habitat for free.
- **Didn't:** I started implementing before writing the OpenSpec bundle and had to backfill it —
  the propose/implement rhythm slipped in the excitement of exercising `aspire publish`. Nothing
  was lost because the bundle preceded the PR, but the order exists so the design is argued
  before it is sunk cost.
- **Next time:** when adopting a generator (aspire publish, any scaffolder), boot its output
  before trusting its success message — a generator's green means "I wrote a file", never "the
  file works". Two of the six defects lived exactly in that gap.
- **Time invested:** not measured (source: **manual** — forty-fifth consecutive).
- **ADR:** none new. DEC-049 gains its first exercised artifact.

## 2026-07-28 — adopt-foundations

- **Worked:** the collision hunt before any code. Diffing both systems' `:root` vocabularies
  mechanically found `--border`/`--info` shadowing that no visual review would have attributed
  (both values are plausible grays), and classified the third collision (`--font-sans`) as
  harmless for a stated reason — `@theme inline` inlines values into utilities — that doubled
  as the mechanism carrying Outfit to migrated surfaces without touching the kit's font. The
  atlas- prefix rename was provably render-identical; coexistence held on first boot, and the
  one toggle driving both dark hooks kept a half-migrated page from splitting into two themes.
- **Didn't:** CI came back red on a pre-existing flaky backend test — the label-chain
  functional test asserted the matched Run synchronously while matching rides CAP's background
  dispatch (2-of-3 failures locally on untouched code). Not this change's defect, but this
  change paid for it. The fixture already owned the right shape (DeliveryProbe's deadline
  poll); the test simply didn't use it.
- **Next time:** when two styling systems must share a document, diff their custom-property
  vocabularies before writing code — a `:root` name collision is silent, theme-dependent, and
  invisible in code review, and five minutes of comm(1) beats an afternoon of "why is this
  border a slightly different gray".
- **Time invested:** not measured (source: **manual** — forty-sixth consecutive).
- **ADR:** none new. DEC-051 carries the decision.

## 2026-07-28 — project-pulse

- **Worked:** the derived-never-stored shape (#94's) fit a second consumer without friction —
  the pulse is one read slice and one Contracts addition (`VendorStoryIds`), zero schema. The
  hand-derivable bar (D2) shaped the tests before the code: seeding runs through the domain
  API with crafted timestamps made every expected value a sum a reviewer can check on paper.
  D3 settled the strip-styling contradiction by rule, not taste: the day-old one-screen-one-
  system spec outranked the issue body's "strip on shadcn" note, and restyling one strip twice
  is cheaper than carving an exception into a fresh design contract.
- **Didn't:** the design validator flagged `#108` in a JSX comment as a three-digit hex colour
  — the comment-exclusion regex knows `//` and `*` but not `{/*`. Reworded the comment rather
  than widening the regex mid-change; the validator owes JSX comments a pattern.
- **Next time:** when a spec and an issue body disagree, say so in the design doc and let the
  spec win explicitly — the sentence "the spec outranks the issue's note" cost one line and
  pre-empted the review question.
- **Time invested:** not measured (source: **manual** — forty-seventh consecutive).
- **ADR:** none new.

## 2026-07-28 — dashboard-tabs

- **Worked:** the E2E reachability suite paid for itself twice in one change. First it caught a
  real product bug on its first run — with operate as "the unmarked default", clicking it
  cleared the URL parameter and the derived landing bounced an unconfigured project back to
  settings, so operate was unreachable; the absence of the parameter had come to mean two
  things at once. Then, when it failed again in CI, instrumenting the test to dump the DOM
  instead of theorising found the second defect in one line: the empty state still read
  "Configure a Connector **above**", true on the single-scroll page and false the moment the
  connector moved to its own tab. Also refused shadcn's Radix Select on purpose — it renders
  divs, and the tests read `option` elements out of `#vendor`/`#runtime`, so adopting it would
  have silently broken the very assertions whose job is to catch a relocated control.
- **Didn't:** the failing test passed in the full suite and failed 3-of-3 in isolation — state
  from other tests was making it pass. A test that only passes with company is not evidence;
  running the single test in isolation is what turned "flaky in CI" into a five-minute
  diagnosis.
- **Next time:** when a change moves UI, grep the copy catalogue for directional words
  ("above", "below", "on the left", "at the top"). Placement changes silently invalidate
  instructions that no compiler, linter or type checker can see.
- **Time invested:** not measured (source: **manual** — forty-eighth consecutive).
- **ADR:** none new. ADR-0006 earned a second citation.

## 2026-07-28 — kanban-board

- **Worked:** inverting the issue's own dependency note paid twice. #110 assumed a drag library;
  acceptance criterion 6 already required a drag-free path for phones and keyboards, so once
  that menu existed dnd-kit bought only touch dragging — which the issue itself excluded. Zero
  new dependencies, and the drag-free path turned out to be the only one Playwright can drive,
  so the criterion that looked like an accessibility chore is what made the chain assertion
  testable at all. Extending the GitHub stub to accept label writes turned criterion 2 from a
  mock's opinion into a fact: the label really lands at the far end and ordinary reconciliation
  carries it back.
- **Didn't:** the design validator read `#110` in a JSX comment as a three-digit colour for the
  second time in one chain — worked around in #108 by rewording, which is precisely the dodge
  that let it recur. Fixed properly this time (an all-digit reference outside a value position
  is not a colour), and verified with a probe file asserting the three real colours still fail.
  The graduation rule says the second occurrence is the fix; obeying it late still cost a CI
  round trip.
- **Next time:** when a test can only exercise one of two paths a feature offers, make that path
  the primary one in the design rather than the fallback — the untestable gesture is then sugar
  on top of something proven, not the other way round.
- **Time invested:** not measured (source: **manual** — forty-ninth consecutive).
- **ADR:** none new.

## 2026-07-28 — automation-output-label

- **Worked:** widening the grill's private field instead of adding a second one beside it. The
  rename forced every call site into view, and EF's `RenameColumn` made "a grill configured
  before the change still works" true by construction rather than by a data fix-up. Moving the
  write to one place also revealed that its most interesting branch — the vendor refusing the
  label, which fails the Run rather than claiming success — had **never been exercised**,
  because the vendor stub could only ever succeed. It has a test now.
- **Didn't:** the API field rename (`readyLabel` → `outputLabel`) broke a functional test in the
  worst way available: the payload field stopped binding, so the value was silently dropped and
  the grill fell back to its default. The test caught it, but a request carrying an unknown
  field deserves a louder answer than a default; nothing in the stack currently objects.
- **Next time:** when a rename crosses the HTTP boundary, grep the *payload literals* in tests
  and clients, not only the C# identifiers — the compiler cannot see a JSON property name, and
  the failure mode is a silent fallback rather than an error.
- **Time invested:** not measured (source: **manual** — fiftieth consecutive).
- **ADR:** none new.

## 2026-07-28 — workflow-canvas

- **Worked:** building the picture found two silent API traps that reading the code had not.
  The automations response returned neither the output label — so the canvas could not derive a
  single edge — nor the rubric path; and since the update endpoint replaces the whole
  Automation, any caller editing from the list would have cleared the rubric path on every save,
  with no error anywhere. A feature that needs to *read* what an endpoint lets you *write* is a
  good way to discover the two disagree.
- **Didn't:** the layout took three rounds with the owner (downward chains, then a slot for the
  undecided step, then left-to-right with the rule as separator) because the propose described
  the balloon's *meaning* carefully and its *placement* not at all. The design also asserted a
  balloon "between two Automations" that cannot exist — an absence has no two ends — which only
  became obvious with the component on screen.
- **Next time:** when a change is mostly a picture, put a rough sketch in the propose, even in
  ASCII. The three rounds were not disagreement about the product; they were the cost of
  discovering the arrangement in code instead of on paper.
- **Time invested:** not measured (source: **manual** — fifty-first consecutive).
- **ADR:** none new.

## 2026-07-28 — signalr-log-window

- **Worked:** questioning the inherited design instead of implementing it. #106 arrived carrying
  #96's shape — the pod pushes into the hub — whose own summary called its authentication "the
  design wrinkle". Asking where else the same event could come from found Postgres already
  announcing it: `NOTIFY` on commit, the portal listening, the worker untouched. The per-Run
  vault credential was not solved, it stopped existing, and "the stream is a witness, never a
  participant" went from a property maintained by care to one nobody can break. Measured 2ms
  against a budget of 1000.
- **Didn't:** the test needed a SignalR client the test project did not have, exactly as #115's
  refusal case needed a vendor stub that could refuse. Twice in one day a branch was untestable
  because the harness could not express the situation — cheap to fix both times, but it means
  "is this assertable?" belongs in the propose, not in the implementation.
- **Next time:** when an issue inherits a matured design, re-ask what problem each part solves
  before building it. The wrinkle was labelled in the issue text and still nearly got built,
  because inherited designs read as decided.
- **Time invested:** not measured (source: **manual** — fifty-second consecutive).
- **ADR:** none new. DEC-050's recorded upgrade is taken, by a different route than it named.

## 2026-07-28 — local-owner-identity

- **Worked:** the guard was wrong and booting said so immediately. The propose gated the local
  owner on "a Production environment", and the self-host compose sets no environment name — so
  ASP.NET calls it Production and the container would not start. A second false positive was
  waiting behind it: every container binds every interface, so the wildcard bind cannot be the
  signal either. What actually distinguishes a deployment from somebody's machine is a managed
  secret store, which only Terraform can configure. Both false positives now have a test named
  for what they are.
- **Didn't:** the propose asserted the signal without checking it against the habitats the repo
  already runs in. Three habitats are documented in SELF-HOSTING.md with a table; a minute
  reading that table would have caught "Production" before it was written into a design doc, a
  spec and a task list, all of which then had to be corrected.
- **Next time:** when a change branches on the environment, enumerate the habitats first and say
  what each one looks like from inside the process. The table exists; the design did not consult
  it.
- **Time invested:** not measured (source: **manual** — fifty-third consecutive).
- **ADR:** none new. OPN-002's half (b) is closed.

## 2026-07-29 — archive-project

- **Worked:** the single creation path paid off again. `RunCreator` already existed as the one
  place matching and Run now both create, so "an archived project starts nothing" is one check
  neither caller can skip — the same shape #115's hand-off used, and for the same reason. The
  architecture test also earned its keep by refusing the first design: the refusal's wording went
  into a Contracts assembly, which carries interfaces and data and never behaviour, and the test
  said so before review did.
- **Didn't:** three defects surfaced only by clicking through the app, all in the seam between a
  changed read model and the screens that consume it. The worst: the detail page derives its
  project from the list, so an archived project lost its own name and offered to archive itself
  again. Also found, preexisting and older than this change: the mock never matched routes with
  a query string, so the story-filtered runs list had been silently falling through since it
  shipped.
- **Next time:** when a read model gains a filter, list the screens that read it *before*
  writing the filter, and ask which of them needs the unfiltered view. The compiler cannot see
  "this page needs the row the list now hides" — it type-checks perfectly and renders a fallback.
- **Time invested:** not measured (source: **manual** — fifty-fourth consecutive).
- **ADR:** none new.

## 2026-07-29 — sync-action

- **Worked:** the action refuses to know how to close a change. It reads the connected
  repository's own close-out document and follows it, so this repository's retro-archive-sync-lint
  ritual stayed in a markdown file instead of leaking into C# — the shape DEC-048 established when
  the grill read a project's own definition of ready. The test that proves it is the one that
  would fail the moment anybody hardcoded a step: a project pointing the setting at its own file
  gets exactly that file's text in the prompt. Both refusals also landed before the workspace, as
  #80 established, each asserted with `Workspace.Prepared.ShouldBeFalse()` rather than by reading
  the code.
- **Didn't:** a seventh entry in the seeded catalogue broke four suites that assert its size, and
  repairing them made it worse. `Skipped.Count.ShouldBe(4)` was replaced with `5` across a file,
  which hit `AProjectSeededBeforeTheSetGrew_Should_ReceiveOnlyTheAdditions` — a test that
  pre-seeds exactly four Automations and whose `4` had nothing to do with the catalogue. One
  honest failure became a more confusing one. Separately, the first pass at the sync branch was
  written against remembered API names — `LinkedChange` for `ChangeFiles`, `RuntimeSelection` for
  `AgentRuntimeSelection`, a `workspace.Discard` that does not exist — and cost a compile cycle
  that reading the signatures would have avoided.
- **Next time:** never replace a bare literal everywhere, especially a number in a test file.
  Numbers in tests are coincidences more often than they are the same fact; anchor each edit to
  the test's name and open every match. Graduated to **ADR-0007** — this is the second occurrence
  of an edit applied by pattern rather than to a site somebody read (the first silently dropped
  `outputLabel` from a payload and left the suite green).
- **Time invested:** not measured (source: **manual** — fifty-fifth consecutive). Capture is still
  broken, not absent: `node .config/otel/verify-telemetry.mjs` fails two checks — *exporter
  enabled AND pointed here* (`OTEL_EXPORTER_OTLP_ENDPOINT` is unset, so exports go to the OTLP
  default port rather than ours) and *usage.jsonl has data* (zero bytes, nothing ever captured).
  Sessions are mapped correctly; only the metrics never arrive.
- **ADR:** [ADR-0007](../adr/0007-an-edit-lands-on-a-site-that-was-read.md) — an edit lands on a
  site that was read, never on a pattern.

## 2026-07-29 — store-secret-value

- **Worked:** the seam's shape did the arguing. `ISecretStore` has one method and no read at all —
  not a read that throws, not one behind a flag — so "a stored value never comes back out" is a
  property of the type rather than a rule somebody has to remember. Making it a sibling of
  `ISecretResolver` instead of two more methods on it means the dependency list of every component
  says out loud whether it can write a credential, and exactly one slice can. The round-trip
  ordering paid off too: verifying with the value read back from the store, not the one in the
  request, is what would catch a store that silently dropped the write.
- **Didn't:** the E2E lane caught what nothing else could, and it caught it six minutes late. The
  Connector form put the secret-name field behind a mode selector, and three E2E tests filled that
  field directly — no compiler, no functional test and no design validator sees a Playwright
  selector. Repairing it exposed a second defect underneath: the portal's HTTP client read the
  problem body's `detail`, and `ApiResults` sends validation errors under `errors` with no detail
  at all, so every refusal that names what to fix — a store that cannot write, a caller who is not
  an Admin — was being replaced with "Could not save the Connector." A third gap only surfaced
  from asking where this could be exercised: under `aspire run` no habitat had a writable store,
  so pasting a token would have worked in Azure and nowhere the author could reach — ADR-0001's
  failure, one layer out.
- **Next time:** when a form field moves behind a condition, grep the E2E selectors for its label
  before running anything. `GetByLabel("Secret name")` is as much a consumer of that markup as a
  component is, and it is the only consumer nothing type-checks.
- **Time invested:** not measured (source: **manual** — fifty-sixth consecutive). Capture is still
  broken, not absent, and unchanged from the previous entry: `node .config/otel/verify-telemetry.mjs`
  fails *exporter enabled AND pointed here* (`OTEL_EXPORTER_OTLP_ENDPOINT` unset) and
  *usage.jsonl has data* (zero bytes).
- **ADR:** none new. Design D4 was revised during implementation — files rather than a table — and
  the reasoning is recorded in the change's own `design.md` rather than promoted, because it is one
  decision inside one slice and nothing recurred.

## 2026-07-29 — vault-write-for-workload

- **Worked:** the product told us exactly what was wrong and what to do about it. #124's refusal
  path — a store that throws with a remedy rather than failing opaquely — turned a deployment
  misconfiguration into a screenshot that named the missing role. That is the whole argument for
  putting the remedy inside the refusal rather than in a runbook, and it paid off within hours of
  shipping.
- **Didn't:** the permission gap was foreseeable at design time and nobody looked. #124 added the
  ability to write to Key Vault and its task list never asked whether the deployed identity could.
  The habitats were enumerated for *identity* in #119 and for *storing* in #124's design, but the
  Terraform role assignments were not read in either.
- **Next time:** when a change gives the product a new capability against a managed service, grep
  `infra/` for the role assignments covering that service before writing the task list. The code
  seam and the IAM grant are two halves of the same permission, and only one of them is visible to
  the compiler.
- **Time invested:** not measured (source: **manual** — fifty-seventh consecutive). Unchanged from
  the previous entry: `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed
  here* and *usage.jsonl has data*.
- **ADR:** none new. This is the first occurrence of the code/IAM split being missed; a second
  would graduate it.

## 2026-07-29 — parallel-ci-lanes

- **Worked:** measuring first turned a two-option argument into a one-line change. The issue asked
  whether to merge the lanes or parallelise them, and the step timings answered it without a
  debate: merged is serial and therefore ~250s, artifact-shared is ~270s, side by side is ~200s.
  Writing the numbers into the workflow's own comment means the next person to consider
  re-serialising it has the evidence in front of them rather than the intuition that waiting saves
  something.
- **Didn't:** the issue asserted that the two lanes build differently — build-test with
  warnings-as-errors, e2e with a plain `dotnet build` — and made a criterion out of fixing it.
  `TreatWarningsAsErrors` is in `src/Directory.Build.props` and applies to both. The claim came
  from reading the two workflow files against each other and not reading what they inherit, which
  is the same shape of mistake as reading a form's markup without its consumers.
- **Next time:** before asserting that two build invocations differ, check the props they inherit.
  A flag absent from a command line is not a flag absent from the build.
- **Time invested:** not measured (source: **manual** — fifty-eighth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new.

## 2026-07-29 — verify-connector-permissions

- **Worked:** the ordering of one `switch` was the whole fix, and getting it right needed knowing
  that a rate limit is also a 403. `ForbiddenException` had to go after `RateLimitExceededException`
  and before the generic `ApiException`, or the one 403 that is not about permissions would have
  started reporting as a missing permission. A test names each of the four causes, so the ordering
  cannot be rearranged by accident.
- **Didn't:** changing a seam broke a stub in another module's test project, and I noticed only
  because that suite printed no result rather than a failure. The loop that ran the suites grepped
  for `Passed!|Failed!`, and a compile error produces neither — so a green-looking sweep had a
  silent hole in it. The same shape as trusting an empty check rollup.
- **Next time:** when a sweep over several test projects reports nothing for one of them, treat the
  absence as a failure and read the output. A missing result is not a pass, and a grep that only
  matches the two happy words cannot tell them apart.
- **Time invested:** not measured (source: **manual** — fifty-ninth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new.

## 2026-07-29 — reap-abandoned-runs

- **Worked:** the bug was proved before it was fixed, and the proof was falsifiable rather than
  suggestive. The Run was dispatched at a known time with a known 30-minute timeout; reading it back
  at 40.7 minutes still `Executing` with no failure reason left exactly one explanation, because a
  live process would have cancelled itself. That turned "maybe the token is wrong" — the first
  hypothesis, and a reasonable one — into a measurement nobody has to argue with.
- **Didn't:** the gap was foreseeable from the code and nothing had looked. The executor's own
  comment says an eternal `Executing` would hold the Story hostage, so the risk was understood when
  it was written; what was missed is that the `catch` protecting against it cannot run when the
  process is gone. A `catch` is not a guarantee about processes, only about exceptions.
- **Next time:** when a rule is enforced inside the thing it constrains, ask what enforces it when
  that thing disappears. BR-005 lived in a `CancelAfter` inside the agent's own process, which is
  the one place guaranteed to be missing in the case worth protecting against.
- **Time invested:** not measured (source: **manual** — sixtieth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. Whether "a rule enforced from inside its own subject" recurs is worth watching;
  one occurrence stays here.

## 2026-07-29 — catalogue-and-workflow

- **Worked:** the whole change was one sentence, and the sentence was already in the code. Membership
  of the workflow is `buildChains(...).filter(chain => chain.length > 1)` — because that function
  already walked a root plus everything reachable, a chain of one means nothing arrives and nothing
  leaves. The distinction two issues had been reaching for was derivable from data that had been
  there since #116; nobody had asked the question. Getting it stated as DEC-053 and in the glossary
  matters more than the filter: the same confusion had already been re-merged twice.
- **Didn't:** two mistakes of the same family, both caught outside the compiler. Extracting the list
  markup into a value, I bounded the block by the first line that stripped to `)}` and cut it in the
  middle of a ternary — the file stopped parsing, which was cheap. Then I wrote the plural keys as
  `"{count} steps"`, inventing a placeholder this catalogue does not have, and it type-checked
  perfectly and rendered "2 {count} steps" in the browser. Both were edits made against an assumed
  shape rather than a read one, which is what ADR-0007 is about.
- **Next time:** before using a formatting helper, read it. `tCount` already prefixes the number, so
  its keys are bare nouns; two lines of source would have said so. The same applies to structural
  edits: bound a JSX block by its indentation, never by the first line that looks like its end.
- **Time invested:** not measured (source: **manual** — sixty-first consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new; the two slips are ADR-0007's territory and it already names the check.

## 2026-07-29 — drag-the-human-step

- **Worked:** the owner's correction landed before any code existed, and it was a correction of
  meaning rather than of wording. The first proposal had the block write `requiresApproval` on the
  following step; the block is actually the *previous* step's output label going away, because what
  a person reviews is what a step produced — "to call a proposal good, somebody has to approve it".
  Both waits already existed in the product and the canvas already drew them apart; the proposal
  fused them. Catching it at propose cost a rewrite of three files instead of an implementation.
- **Didn't:** two things I could not assert and one I nearly claimed anyway. Playwright cannot drive
  an HTML5 drag and there is no frontend unit runner, so the gesture itself has no tier. The
  temptation was to write a test that clicks something adjacent and call it covered. What actually
  fixed it was routing the explicit control through the same function as the drop, so the assertable
  path and the unassertable one are one path — but that only occurred to me after writing the test
  and finding it proved nothing about `placeBlock`.
- **Next time:** when a gesture cannot be driven by any tier this repository has, do not look for a
  test that approximates it. Make the gesture a caller of something that can be driven, and say in
  the evidence which part remains unasserted.
- **Time invested:** not measured (source: **manual** — sixty-second consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. If a second gesture turns out to be undrivable, "an untestable gesture must be
  sugar over a tested path" is the shape that would graduate.

## 2026-07-29 — reap-from-the-phase

- **Worked:** reading somebody else's grill of the same area found my own bug. #144 was filed against
  the run-execution window and, working through its claims, `Run.StartedAt` stopped meaning what
  #140's requirement assumed. The reaper timed the approval wait that BR-006 declares untimed. Also
  worth keeping: the regression test was confirmed red before the fix — the expression reverted, the
  failure watched, then restored — because a regression test nobody has seen fail proves nothing.
- **Didn't:** the defect was in the requirement I wrote, not only in the code. *"Its start, plus its
  Automation's timeout"* is readable, plausible and wrong, because `StartedAt` does not mean "the
  start of what is currently running". The implementation followed the sentence faithfully and
  inherited its error, and #140's suite was green throughout because the missing scenario — an
  approved Run surviving a long wait — was missing from the spec too. A green suite over a wrong
  requirement is the most expensive kind of green.
- **Next time:** when a requirement names a timestamp, write down what that field is set by. Half a
  line — "`StartedAt`, set by `MarkPlanning`" — would have made the sentence obviously wrong before
  any code existed, because the next words were "plus the timeout" and the field predates a wait
  that has no timeout.
- **Time invested:** not measured (source: **manual** — sixty-third consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. If a second requirement turns out to be wrong in the same way — correct English
  over a misread field — "a requirement that names a field names what sets it" is the shape.

## 2026-07-29 — run-execution-resilience

- **Worked:** the ceiling turned an unkeepable rule into a keepable one, and that reframing was the
  whole change. BR-005 said "Admin-configurable" while the platform budget was ten minutes, so the
  rule could not be honoured by any value of the Terraform setting — there was nothing to fix until
  the timeout had an upper bound. Once it did, the three sites could be bound to each other by
  comment, which is the only guard available when a contract spans a C# constant, a Terraform value
  and a written rule. Also worth keeping: the two live-log tests failed the moment the hub's payload
  changed shape, having been bound to `string[]`. They were the check that the contract moved.
- **Didn't:** the proposal asserted that the client "already handles a redelivered push by sequence",
  and it did not — the handler concatenated unconditionally, so subscribing before the first read
  would have traded a missed line for duplicated text. Third time today a claim of mine about
  existing behaviour was wrong: the others were `TreatWarningsAsErrors` (assumed absent, was
  inherited) and `tCount` (assumed a placeholder it does not have). Every one came from reasoning
  about code I had not opened at the point of writing the claim.
- **Next time:** a design that depends on existing behaviour names the file and line where that
  behaviour lives, before the design is written. "Resolved by sequence" should not have survived
  contact with `useRuns.ts:151`, and it would not have if the sentence had been required to cite it.
- **Time invested:** not measured (source: **manual** — sixty-fourth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **Also didn't:** the archive refused, for the second time in this session, because a MODIFIED
  requirement dropped scenarios the current spec holds — four of them here, and the same shape as
  `sync-action`'s refusal. Rewriting a requirement is not writing a new one: the delta replaces the
  whole block, so every scenario has to be carried whether or not this change touches it. The tool
  caught it both times, which is the only reason it cost minutes rather than a silent loss of four
  assertions.
- **ADR:** none new, but two shapes are now at their second occurrence. A design claiming an existing
  behaviour it did not verify (#146's requirement named a field without naming what set it; this one
  claimed the client deduped by sequence). And a MODIFIED requirement dropping scenarios. The first
  would graduate as "a claim about existing behaviour cites where that behaviour lives"; the second is
  already enforced by the archive, which is the better answer — a gate beats a rule.

## 2026-07-29 — actionable-failure-inbox

- **Worked:** the valuable part of this change was not either capability, it was noticing that the
  inbox and the pulse each held a copy of "waits on a human" under a comment promising the two would
  never disagree. Adding the dismissal to one copy and not the other would have shown a Member "1
  waiting" above an empty page, and the comment could not have stopped it. Extracting the predicate
  and asserting the list and the count *together* replaced a promise with a mechanism. Also kept: a
  test asserting the read model's exact field set failed the moment `dismissedAt` appeared, which is
  the whole reason it asserts a set rather than a subset.
- **Didn't:** I wrote `entry.Reason` in a test and got a null back — the inbox's field is
  `WaitingFor` and its value is `"failure"`. That is the **fourth** time in one session that I
  asserted the shape of existing code without opening it: `TreatWarningsAsErrors` (assumed absent,
  inherited), `tCount` (assumed a placeholder it lacks), "the client dedupes by sequence" (it
  concatenated), and this. Every one cost a cycle, and every one was a file I could have read in
  five seconds.
- **Next time:** treat "I know what that returns" as the signal to open the file. Three of the four
  were in code I had edited earlier in the same session, which is exactly when the belief feels
  safest and the memory is stalest.
- **Time invested:** not measured (source: **manual** — sixty-fifth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. The claim-without-reading shape is now at four occurrences in one session and is
  worth graduating next time it appears — the check would be that a claim about existing behaviour
  cites the file and line where that behaviour lives.

## 2026-07-29 — live-conversation-decision

- **Worked:** the decision fell out of rules already locked, so no spike was needed and the ADR could
  say why. BR-006 says a human wait is untimed; DEC-013 says nothing idles; a paid process waiting on
  an untimed wait has no cost bound. That is an argument, not a measurement, and recognising it saved
  proposing an experiment whose result would not have changed the answer. Recording the rejected
  options mattered as much as the choice — the hybrid is explicitly the option to revisit *if* (a)
  proves slow and OPN-002 closes, which is a condition somebody can check rather than a door quietly
  shut.
- **Didn't:** the analysis nearly skipped the prerequisite. The decision reads as "no new
  architecture", which is true and made it easy to miss that the Connector cannot post a comment
  today — it reads comments and writes labels and state. A decision that looks free because it changes
  no architecture can still owe a slice, and naming it is part of closing the question honestly.
- **Next time:** when a decision claims "nothing new is needed", list the seams the chosen path
  touches and check each one does what the path assumes. Here it was one method that does not exist.
- **Time invested:** not measured (source: **manual** — sixty-sixth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** [ADR-0008](../adr/0008-a-live-conversation-costs-a-pass-per-message.md) — a live
  conversation costs a pass per message, because an untimed wait cannot idle. Closes OPN-005 as
  DEC-055.

## 2026-07-29 — human-step-column

- **Worked:** rewriting the issue before proposing was the whole difference. Its first version carried
  the same premise error #137's first draft had — the column tied to `requiresApproval` — and
  proposing on it would have built a board that draws two different waits identically. Catching it at
  the issue cost a rewrite; catching it at implementation would have cost the slice. The corrected
  premise also made criterion 5 free: the column derives from an absent output label, so closing the
  chain removes it with no code.
- **Didn't:** `BoardAutomation` was a hand-written subset of four fields, and I widened the interface
  field by field until the compiler had told me four times that the board now needs the whole record.
  It writes an Automation as of this change, and the update resends everything — so the subset was
  wrong the moment the board stopped being read-only, and the four errors were one fact repeated.
- **Next time:** when a read-only view starts writing, replace its narrowed type rather than growing
  it. The first missing field is the signal that the narrowing has expired, not a field to add.
- **Time invested:** not measured (source: **manual** — sixty-seventh consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new.

## 2026-07-29 — unique-automation-triggers

- **Worked:** the fix removed a filter rather than adding one. `OverlapGuard` fetched candidates with
  `TriggerLabel == candidate.TriggerLabel`, which Postgres evaluates case-sensitively, so a
  differently-cased sibling was never fetched and the rule could not see the conflict it exists to
  catch. The file's own comment already said it wanted the domain rule to decide in memory; the query
  was quietly doing half the deciding. And the race test is the one that could not have passed before:
  six concurrent identical saves now yield one Automation and five of the rule's own refusals.
- **Didn't:** three more claims-without-checking here, and the seventh of the session was the sweep
  itself — a hand-written list of test projects that missed
  `AiOrchestrator.Modules.Projects.UnitTests`, so I reported green over a red suite and CI corrected
  me. The others: `TriggerOverlaps` is `Error.Conflict` (409, not 400) and the routes are
  `/enable`/`/disable`, not `/enabled` with a body.
- **Worth keeping separately:** the repository had made the same mistake about a vendor. A unit test
  asserted GitHub label names are case-sensitive because "folding case would invent a rule the vendor
  does not have". Three `gh api` calls disprove it — `bug`, `BUG` and `Bug` all resolve to `bug` — so
  the comment invented the absence of a rule that exists, and matching silently never fired for
  differently-cased triggers for as long as it stood.
- **Next time:** the rule is now an ADR rather than a note, because seven occurrences in one session is
  a pattern and not a slip. Cite the file and symbol; exercise an external system instead of reasoning
  about it; enumerate a set with a command.
- **Time invested:** not measured (source: **manual** — sixty-eighth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** [ADR-0009](../adr/0009-a-claim-about-existing-behaviour-cites-where-it-lives.md) — a claim
  about existing behaviour cites where that behaviour lives. Sibling to ADR-0007: that one governs the
  edit, this one governs the sentence.

## 2026-07-29 — repo-defined-actions

- **Worked:** the safety argument shaped the design instead of being bolted onto it. Frontmatter is
  ignored because a `model:` line would let a file in somebody's repository choose what this product
  spends and a `tools:` line would let it grant itself powers the Automation withheld; the write surface
  is one comment because the prompt is untrusted text that may ask for anything. Both are stated in the
  spec so a later reader cannot mistake them for omissions. ADR-0009 also paid for itself twice here:
  the executor's action routing turned out to be an *exclusion* list, so the new action reached
  `RunSimpleAction` without an edit — checked, not assumed — and asking what a `/`-only traversal check
  would miss produced the backslash cases the tests now cover.
- **Didn't:** asked whether the issue had changed, and answered from a proxy — comment count and
  `updatedAt` — without reading the body. The body already specified the prompts directory, so the
  proposal was validated, committed, pushed and reported as complete while missing scope its own issue
  named. ADR-0009 was one day old and its exact failure mode, applied to an artifact instead of code.
  Two smaller ones the same shape: an edit called a `RunAgent` method that does not exist (ADR-0007's
  case), and a fourth positional parameter on `ConnectorContext` broke three `Deconstruct` sites that
  have no use for a prompts directory — both caught by the compiler, which is the cheap end.
- **Next time:** when the question is *"did this change?"*, read the thing, not its metadata. A comment
  count, an `updatedAt`, an empty check rollup — each is a fact about the artifact that is not the
  artifact, and each reads as reassurance.
- **Time invested:** not measured (source: **manual** — sixty-ninth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. This is [ADR-0009](../adr/0009-a-claim-about-existing-behaviour-cites-where-it-lives.md)'s
  rule reaching past code and vendors to the issue itself, not a second pattern. If a proxy is trusted
  over the artifact once more, it graduates on the next occurrence rather than this one.

## 2026-07-29 — edit-automation-form

- **Worked:** the change was shaped by one concrete trap found by reading the code rather than by a
  general wish for an edit form. The update endpoint is a full replace and create's submit sent
  `timeoutMinutes: null`, so the obvious implementation — reuse create's submit — would have reset every
  Automation with a configured timeout to the default on every edit, silently, because the row would go
  on rendering a number. The canvas already worked around it by passing the value explicitly, which is
  what proved the trap real instead of theoretical. That turned a vague task into a specific decision:
  make the timeout visible, because a value resent on somebody's behalf is one they should be able to
  see. The E2E asserting a 45-minute timeout survives a label-only edit is the test that would have
  caught the wrong version.
- **Didn't:** the four new E2E tests failed on their first run, all four on "waiting for the Edit button
  to be visible" — the E2E host serves the **built** SPA from `wwwroot`, and only the source had
  changed. Two minutes to find out, and the failure looked exactly like a broken selector, which is the
  expensive part: the first hypothesis was wrong about *my own code* rather than about the environment.
  CI builds the frontend before its E2E step; nothing does locally.
- **Next time:** `pnpm build` before running E2E locally, and read a whole-suite failure of *new* tests
  as an environment claim before a code claim. Four failures with one identical message is a fact about
  the setup, not four facts about the tests.
- **Time invested:** not measured (source: **manual** — seventieth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. The stale-bundle trap is a first occurrence; if it costs a second run it graduates.

## 2026-07-29 — edit-connector-keeps-credential

- **Worked:** the fix followed from *where* the rule lived rather than from what it said. The
  exactly-one-credential rule sat in the request `Validator`, which FluentValidation runs before the
  handler — so it was evaluated where the database is not, and "neither" was refused as a property of
  the request when it is really a question about the world. Splitting it along that seam (not both stays,
  not neither moves) made the rest fall out, including why reuse must still re-probe: an edit can change
  what the credential is asked to read.
- **Didn't:** the first version of the E2E passed and proved nothing. It set the Connector up by naming
  a secret, and in that mode the form re-sends the Connector's own secret name — so it never sent
  "neither" and never touched the path the test existed to cover. A green test shaped like coverage is
  worse than no test. Caught by asking what the old code would have done with this exact page, then
  confirmed by restoring the old submit guard and watching it go red.
- **Also worth recording:** this defect was created by #150 earlier the same day. Shipping the prompts
  directory gave Settings something worth editing and immediately made the form unusable for editing it.
  Nothing in #150's own tests could have caught it — they exercised the API, and the block was a
  client-side submit guard on a form #150 did not change.
- **Next time:** for every new test, state what would make it fail. If the answer is "nothing I can
  name", it is not a test yet. Reverting the fix to see red is cheap and settles it in one run.
- **Time invested:** not measured (source: **manual** — seventy-first consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. "Verify a test can fail" is a second occurrence of the same instinct ADR-0001
  records for infrastructure claims — exercise it rather than reason about it — so it belongs to that
  ADR's family rather than to a new number. If it recurs as a *green* test again, it graduates.

## 2026-07-29 — readable-run-output

- **Worked:** reading the code before designing changed what the work was, twice. The default runtime's
  silence looked like missing streaming infrastructure and was one flag — `HeadlessProcess` had always
  raised `OutputDataReceived` per line. And the spec already *required* incremental output, so AC-1 was a
  defect against an existing requirement rather than new scope, which is why the delta modified that
  requirement instead of adding a rival one.
- **Didn't — and this is the one worth keeping:** I designed the no-terminal-event case wrong and caught
  it only because an existing test went red. My version trusted the exit code and reported success with
  unknown usage, which reads as generous degradation until the consequence is traced: a simple action's
  reply becomes a **comment on somebody's Story**, so a "success" whose reply is raw stream text would
  publish noise into a customer's backlog. The pre-existing judgement — unreadable output is a broken
  contract — was right, and the test defending it was the only thing that said so. I had also already
  written the wrong rule into the spec delta, so the fix was two files, not one.
- **Also:** two tasks in my own `tasks.md` named a tier this repository does not have. There is no
  frontend unit runner — `testing-strategy`'s four tiers are all .NET — and the E2E tier cannot produce a
  Run with stored log chunks. Both are recorded as not done with the reason, and the interpreter is
  covered by browser observation instead. Writing tasks against an imagined test tier is the same
  unchecked-claim habit ADR-0009 names, applied to the plan rather than to the code.
- **Next time:** when a change loosens a failure into a success, trace what the success then *does*. The
  question is not "is this input recoverable" but "what does the product write when it recovers".
- **Time invested:** not measured (source: **manual** — seventy-second consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. Both findings belong to families already recorded — ADR-0009 for the imagined test
  tier, and the existing-test-as-defence lesson is what ADR-0001's instinct protects.

## 2026-07-29 — collapsible-sidebar

- **Worked:** the change turned out to be smaller and more interesting than the issue described, and only
  because its premises were checked. Wiring the shell to the canonical width variables is what makes
  collapsing a one-line choice, and it closed a 24px drift nobody had seen: `AppShell` hard-coded
  `16rem` (256px) while `--sidebar-w-expanded` said 280px. A value nothing consumes cannot be wrong in
  any way that shows, which is exactly how that sat there. Reverting the wiring to prove the E2E width
  assertions go red took one run and settled that they discriminate.
- **Didn't — and it is the same mistake twice in one day:** I wrote into the proposal *and* the PR body
  that neither sidebar variable was defined anywhere, and told the owner so. They were both defined, in
  `docs/design-system/tokens/layout.css`, exactly as the issue said. I had grepped `src/frontend` for CSS
  and concluded from its silence; the canonical layer lives under `docs/`. Earlier today the same shape
  cost #150 a missing scope — a proxy read instead of the artefact — and here it cost a false claim
  published to a PR before implementation caught it.
- **Also:** a stale comment in `AutomationsSection.tsx` claimed a remembered preference that was removed
  by #136, and that comment is where the issue's "the mechanism exists in two places" came from. A wrong
  comment propagated into a grilled issue and out into a plan. It is deleted; the pattern graduated on
  its genuine second occurrence.
- **Next time:** when concluding *"X is not defined anywhere"*, the search has to cover everywhere X
  could be — and for anything in the design system, that means the canonical layer under `docs/`, not
  `src/`. A negative claim needs a search whose scope is stated; an absence found by looking in one place
  is not an absence.
- **Time invested:** not measured (source: **manual** — seventy-third consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new, but this is now the **third** occurrence of ADR-0009's shape today — a claim about
  what exists, believed from an incomplete look. The ADR already covers it; what it does not yet say is
  that a *negative* claim needs its search scope stated. If that recurs, amending ADR-0009 with that
  sentence is the change to make.

## 2026-07-30 — entra-app-script (spec-less, #167)

- **Worked:** the owner's two questions reshaped the artifact before it ran, and both times the spec
  already held the answer. "It uses a BFF, no?" — the same-origin requirement (wwwroot, relative calls,
  no CORS) means the first version's public SPA client was the wrong shape, and the correction to a
  confidential client with a vaulted secret is in the history rather than squashed away. And the local
  redirect URI was read from launchSettings (http://localhost:5080) after the first draft invented
  https://localhost:7443 — a redirect URI that does not match to the character fails sign-in with no
  message at all.
- **Didn't:** I predicted the cross-tenant run would break at the vault step, and it did not — the
  owner's login reached both the personal tenant and the infra subscription, and the secret landed in
  kv-aio-dev on the first try. A prediction stated as a warning cost nothing this time, but it was
  reasoning about `az`'s context model instead of exercising it (ADR-0001's line, again). I also spent
  words on the personal-versus-corporate tenant distinction after the owner had already scoped the
  question to "is this viable" — the limit belonged in DEC-058's scope note, once, not in the
  conversation three times.
- **Next time:** when a bootstrap's environment differs from the assumed one, run the smallest probe
  first (`az account show`) instead of predicting which step fails.
- **Time invested:** not measured (source: **manual** — seventy-fourth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. The wrong-shape-first finding is ADR-0009's family (a claim believed over the
  spec that was one file away); the prediction-versus-probe finding is ADR-0001's.

## 2026-07-30 — entra-sign-in

- **Worked:** the two-mode contract held the whole way because composition keys on configuration
  presence — the lesson IdentityComposition already carried. All 364 tests passed without any of them
  learning that auth exists, which is DEC-058's second half doing its job: the seam from #119 absorbed
  an identity provider without a single consumer changing. And reading the routes before writing the
  /api gate caught the two carve-outs that would have shipped broken: webhooks are signed, not
  sessioned, and the hub lives outside /api but must not be open.
- **Didn't:** three claims failed on first contact, all caught by executing. WriteAsJsonAsync overwrites
  a pre-set content type (the header rides the call). Setting OpenIdConnectOptions.Configuration does
  not stop the discovery fetch — the handler consults the ConfigurationManager, and the 500 its
  IOException caused is how the static manager earned its comment. And I asserted an unsigned webhook
  answers 401 when it answers 200 with no matching connector — the test now pins what was observed.
  Separately, the owner's first entra-app.sh run had no DEPLOYED_ORIGIN, and the script's create-only
  redirect handling silently stranded the deployed environment; it is declarative now, because a
  bootstrap whose re-run cannot converge is one that strands the first environment it was run without.
- **Next time:** when wiring a gate over a path space, enumerate what already lives there by command
  before choosing the predicate — the webhook and hub carve-outs were found that way and would not have
  been found by reasoning about "the API".
- **Time invested:** not measured (source: **manual** — seventy-fifth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. The three wrong claims are ADR-0009's family, each caught by its rule — exercise,
  don't reason.

## 2026-07-30 — entra-instance-default (hotfix, #170)

- **Worked:** the diagnosis was three commands — probe the URL, read the container log, find
  `IDW10106` — because every layer said something true: the problem body carried a traceId, the log
  named the missing option, and the option named the library that wanted it.
- **Didn't — and this is the finding:** the wiring test was green over the exact gap that shipped. It
  set `AzureAd:Instance` itself, so it exercised a configuration more complete than the deployed one.
  That is the same shape as #160's E2E that passed without touching the reuse path: a test whose setup
  quietly supplies what the system under test is missing proves the setup, not the system. The library
  refusing per request — inside the auth middleware — is what turned one missing option into a total
  outage including health probes.
- **Next time:** a wiring test's configuration must be **exactly** what the deployment carries —
  copied from the Terraform env block, not written from memory of what the library wants. Anything the
  test adds beyond that list is a gap the test can no longer see.
- **Time invested:** not measured (source: **manual** — seventy-sixth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new; second occurrence of the green-test-shaped-like-coverage finding (#160 was the
  first). A third graduates it.

## 2026-07-30 — sign-in-first-deploy-gaps (hotfix, #172)

- **Worked:** the first real deploy was the test no tier could be — it found both gaps in one run.
  The verify step failing IS the deploy contract asserting itself: /api/health behind the gate broke
  the smoke check, and the challenge URL's response_type=id_token disproved the code-flow design
  claim at the provider's own door, before any person hit AADSTS700054.
- **Didn't:** my first red-check of the new health test proved nothing — the mutated build failed on
  FORMATTING, so the test never ran, and set -e swallowed the evidence. A discriminator check must
  watch the mutated build succeed before reading the test result; a red pipeline is not a red test.
  And the spec itself carried the wrong flow: "authorization code flow, redeemed server-side" was
  written from how I believed Microsoft.Identity.Web works, not from observing it — ADR-0009's
  failure, this time landed in an archived spec.
- **Next time:** for auth changes, the deploy IS part of the test plan: schedule the first deploy
  before calling the change done, because the provider and the release pipeline assert contracts no
  local tier reaches.
- **Time invested:** not measured (source: **manual** — seventy-seventh consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. Third occurrence of green-over-the-gap is NOT this (the wiring test's Instance
  gap was #170's, already counted); the discriminator-that-never-ran is a first.

## 2026-07-30 — forwarded-proto (hotfix, #174)

- **Worked:** the challenge URL itself was the diagnosis — `redirect_uri=http%3A%2F%2F` in the
  Location header names the missing X-Forwarded-Proto processing without a single log line. And the
  gap was testable locally all along: one request with the header the ingress actually sends, asserting
  the scheme in the challenge. Verified red without UseForwardedHeaders, green with it — with the
  mutated build watched to 0 first, which is #172's discriminator lesson applied one day later.
- **Didn't:** three deploys to get one sign-in chain right (#170 Instance, #172 issuance + health,
  #174 scheme). Every gap was between the app and its habitat — provider contract, release contract,
  ingress contract — and none was reachable by reasoning; each surfaced only when the deployed
  artifact met the real counterpart.
- **Next time:** the #172 retro said the deploy is part of the test plan for auth; this adds the
  refinement that the *first* deploy should be expected to fail more than once, and budgeted as a
  probe rather than treated as a release. Chain: probe-deploy, read what the habitat says, fix, then
  the release deploy.
- **Time invested:** not measured (source: **manual** — seventy-eighth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new; the habitat-contract family now has three members in one day, all under #12's
  umbrella. If the NEXT slice that touches a habitat contract ships a gap of this shape, that is the
  graduation point.

## 2026-07-30 — session-cookie-lax (hotfix, #176)

- **Worked:** the owner's one-line report ("me pide login todo el rato") was fully diagnostic — a
  clean loop with no error page has exactly one shape in this setup, because a correlation failure
  would have thrown and a config gap would have 500'd. The mechanics were nameable from the armchair
  and the fix is one enum value.
- **Didn't — and this one stings:** I documented the trap and then fell into its mirror. The README,
  the code comment and DEC-058 all said "Strict for the session, Lax-ish defaults for the handshake",
  and the first half was wrong for the one navigation that matters most: the post-login redirect is
  initiated from the provider's cross-site form post, and Strict cookies do not ride it. I reasoned
  "every request carrying it is same-origin" and never asked who INITIATES the first request after
  sign-in. Worse: no tier could have caught it — HttpClient ignores SameSite — so the only detector
  was a human with a browser, and that is what found it.
- **Next time:** for any cookie policy decision, trace the first navigation after each cross-site
  hop, not just the steady state. And when a rationale lands in a DEC, the DEC inherits the claim's
  risk — corrections are new entries (DEC-059), which is fine, but cheaper never written wrong.
- **Time invested:** not measured (source: **manual** — seventy-ninth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. Fifth habitat-contract gap under #12's umbrella in one day (Instance, health
  gate, id-token issuance, forwarded scheme, cookie policy). The #174 retro already named the rule —
  the first deploy is a probe — and this extends it to: the first HUMAN sign-in is part of that
  probe, because SameSite semantics have no machine-reachable tier.

## 2026-07-30 — mobile-identity-block (hotfix, #178)

- **Worked:** the one-nav-two-containers rule already in the shell named both the defect and the fix.
  The identity block had been written once, into one container; extracting UserBlock and rendering it
  in both is the same medicine the nav items took long ago. Verified in the browser at 375px before
  committing.
- **Didn't:** two small stumbles, both self-inflicted. I asserted the E2E environment shows the
  stopgap's label and it composes the local owner — instrumenting the assertion to print what the
  sheet actually said settled it in one run (ADR-0009: observe, don't assume). And an edit script's
  replace was a silent no-op because csharpier had reflowed the target text — the wrong assertion ran
  twice before I noticed. A replace without an assert on "did it match" is an edit that can lie.
- **Next time:** python edit scripts assert their replaces matched, every time — the no-op cost two
  test runs here and cost a broken temp-edit earlier this week (#130's mock).
- **Time invested:** not measured (source: **manual** — eightieth consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new.

## 2026-07-30 — persist-key-ring (hotfix, #180)

- **Worked:** Log Analytics settled in one query what four hypotheses could not. The exception was
  `Unable to unprotect the message.State` on the four *real* callbacks, while the `State is null`
  entries were bare URL visits — two different failures in the same log, and reading the timestamps
  is what separated the owner's problem from my own probes. Before that I had checked the cookie
  attributes verbatim rather than trusting a hypothesis, which ruled out correlation properly.
- **Didn't:** I mangled my own evidence once — a `sed` meant to shorten cookie values ate the
  `secure` attribute, and I nearly concluded "SameSite=None without Secure" from output my own
  command had corrupted. Caught it by re-reading unfiltered. Redacting evidence before reading it is
  how a diagnosis becomes fiction.
- **Also:** this is the sixth habitat gap under #12 and the first that no amount of local care could
  have prevented — an in-memory key ring is invisible until two processes must share it, and
  `min_replicas = 0` plus a revision per deploy guarantees they must. Worth naming as a class:
  anything encrypted by one instance and decrypted by another needs a persisted, wrapped ring, and
  the containerised default is that it has neither.
- **Next time:** for any authentication work on a scale-to-zero host, provision the key ring in the
  same change as the provider — not after the first failure. It is not an optimisation; it is the
  difference between sign-in working once and working twice.
- **Time invested:** not measured (source: **manual** — eighty-first consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new, but the habitat-contract family is now large enough to be worth an ADR of its
  own once #13 lands: six gaps in one slice, every one at a boundary between the app and something
  it does not run.

## 2026-07-30 — strict-with-landing (hotfix, #182)

- **Worked:** the owner said "we need SameSite Strict" and pointed at ds-connect, and reading that
  codebase replaced a worse design of mine before it was written. I was about to build an interstitial
  landing hop to smuggle a Strict cookie past the callback redirect. ds-connect's ADR-0001 needs no
  hop: challenge with Bearer so protected calls answer 401, serve the SPA shell **anonymously**, and
  let the SPA navigate to sign-in itself. Then the only cross-site-initiated navigation needs no
  cookie, and Strict costs nothing.
- **Didn't — and it is the same mistake twice, one layer apart:** #176's loop was caused by
  `RequireAuthorization` on the SPA fallback, and I diagnosed it as "Strict is wrong here" and relaxed
  the cookie. I treated the symptom and wrote the wrong rationale into DEC-059. The cause was my own
  earlier decision to gate the shell — a decision I had made without asking what the shell actually
  needs, since a public bundle needs nothing. Two DEC entries now exist to walk that back.
- **Also:** the deploy had been broken since #180's apply failed — creating a Key Vault key needs
  Crypto Officer, which neither CI nor the operator holds. Shipping persistence unwrapped with the
  residual risk written into the Terraform and its own follow-up (#183) is the honest trade; silently
  dropping the wrapping argument I had made an hour earlier would not have been.
- **Next time:** when a security setting appears to break a flow, ask what the flow legitimately needs
  before loosening the setting. "Strict breaks login" was false — "requiring a session for a public
  bundle breaks login" was true, and only one of those sentences points at the fix.
- **Time invested:** not measured (source: **manual** — eighty-second consecutive). Unchanged:
  `node .config/otel/verify-telemetry.mjs` fails *exporter enabled AND pointed here* and
  *usage.jsonl has data*.
- **ADR:** none new. Worth noting for the habitat-contract ADR that #12 keeps earning: two of the
  seven gaps were fixed by reading another repository rather than the docs, which is an argument for
  looking at a working implementation before designing an auth flow from primitives.

## 2026-07-30 — project-roles (#13)

- **Worked:** the breaking Contracts change was the right call at the right size. `Principal` carried
  one global `Role` while BR-009's roles are per project, so "this caller's role" had no answer — and
  three call sites is cheap. Every future feature asking "may they?" would otherwise have asked the
  wrong question and got a plausible answer. Making the check a pipeline decorator with **default
  deny** is what turned BR-009 from a sentence into a mechanism: an operation added without thought is
  refused, and a reflection sweep names it before a human meets the refusal.
- **Didn't — I wrote a hole and caught it while writing the design.** The permission reader asked the
  principal whether it was the habitat's "sole occupant" and derived that from its id. The provider
  habitat calls its pre-sign-in caller `anonymous`, exactly as the provider-less habitat calls its only
  caller: one value, two opposite meanings, and an unauthenticated caller would have held Admin
  everywhere. The pipeline's 401 stood in front of it, so nothing was reachable — but a permission
  model whose correctness rests on a carve-out list in unrelated middleware is one bad exemption from a
  breach. This is the seventh habitat-inference defect in two slices; ADR-0010 is the graduation the
  previous entry promised "once #13 lands".
- **Didn't — my own slice opened the run-log hub.** It dispatches nothing, so it declared nothing and
  the decorator never saw it. That was harmless only while being authenticated implied being permitted,
  and this change is precisely what ended that: the slice that scoped every other read of a Run would
  have left its **live** stream of an agent's raw output open to any signed-in caller with a Run id.
  Found by re-reading ds-connect at the owner's suggestion — not by any test I had written, and not by
  reasoning about my own change's blast radius, which is where it should have been found.
- **Didn't — two test failures I caused and one I inherited.** A shared fixture's stub was restored
  *before* each test of the three classes that mutated it and never after the last, so a Member role
  leaked into the next class and reddened eight Backlog tests in a module I had not touched; the reset
  now rides with the database reset. An E2E assertion read visibility once instead of waiting, passed
  here and failed in CI on its first run. And a mutation check reported "3 passed" from stale binaries
  because the mutated build had failed — the second time that trap has been stepped in, and the reason
  the next mutation was only believed after confirming the build reached zero errors.
- **Also:** the owner's "look at how ds-connect resolves permissions" replaced my vocabulary as well as
  finding the hub. BR-009 says *operations name permissions, roles are bundles*; I had shipped
  operations naming the bundle. Identical behaviour under DEC-034's two fixed bundles, and one table
  versus twenty-nine declarations when custom roles arrive. Second time in three slices that reading a
  sibling codebase beat designing from the rule text — worth noticing as a habit, not just a rescue.
- **Next time:** when a change makes two previously-equivalent things different — here authentication
  and permission — enumerate every surface that relied on the equivalence *before* implementing, not
  after. The hub was reachable from that question in one step, and no test would have asked it.
- **Time invested:** not measured (source: **manual** — eighty-third consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** **ADR-0010 — a habitat contract is asked, never inferred.** Seven occurrences across #12 and
  #13, the last caught in review rather than in production.

## 2026-07-31 — output-label-set (#165)

- **Worked:** exercising the migration instead of trusting it. EF scaffolded the type change as
  `DropColumn` + `AddColumn` and even warned that it "may result in the loss of data" — applied as
  generated it would have discarded **every hand-off configured in the deployment**, every workflow
  edge, and left a perfectly correct schema behind. No test of the new shape would have noticed,
  because the new shape works fine empty. The test that matters spins its own container, migrates to
  the version before the change, writes a row the old way, migrates to head, and looks. Verified red
  by removing the copy step — which is to say, by reproducing exactly what EF had written.
- **Didn't — the E2E proved nothing and I nearly shipped it.** It asserted that the source's trigger
  label appeared twice, reasoning that a branch row repeats it. The Automations tab renders the
  catalogue *and* the canvas, so every trigger already appears twice: the assertion passed with branch
  rows switched off entirely. Found only by running the mutation, not by reading the test. This is
  **ADR-0004 again** — a verification asserted a proxy signal rather than the artifact — and the fix
  is the shape that ADR implies: the branch chip now carries its own accessible name, and the test
  asserts on the name the feature owns.
- **Didn't — the first mutation attempt lied too.** Removing the branch push left `node.branches`
  populated, so the serialization note still rendered and the run came back green. A mutation that
  does not actually remove the behaviour is a green that means nothing, which is the same trap as
  #13's "3 passed" from a build that had failed. Twice in two slices now: the mutation itself needs
  the same scepticism as the test.
- **Also:** widening the self-trigger rule to a set exposed that it compared with `Ordinal` while the
  vendor and BR-003 both compare case-insensitively (DEC-056). `AI:Implement` as the output of an
  `ai:implement` trigger walked straight past the guard — the exact loop the rule exists to prevent,
  spelled differently. Fixed while the rule was being rewritten anyway.
- **Also:** the canvas's contract says a chain is one row that must not wrap, which a branch cannot
  obey. Rather than redesign a surface #136/#142 own, each extra hand-off became a row of its own
  opened by a chip naming the step it left — two edges, two rows, one reading direction.
- **Next time:** an assertion that would pass for a reason unrelated to the feature is not an
  assertion. Before writing one over rendered text, ask what *else* puts that text on the page —
  and prefer a name the feature owns to a string it merely contains.
- **Time invested:** not measured (source: **manual** — eighty-fourth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. ADR-0004's second recurrence is recorded above rather than graduated again — the
  decision already says this; what failed was applying it.

## 2026-07-31 — portal-conversation (#166)

- **Worked:** two platform facts were checked instead of recalled, and both changed the design. The
  provider was asked for its own schema — `azurerm` 4.81 models no session pool — which is why the
  stack gained `azapi` for exactly one resource. Then `terraform validate` rejected identity-based
  registry auth at `2024-02-02-preview`, which sent me to `az provider show` for the version list and
  landed the pool on the **stable** `2025-01-01` that accepts it. Neither came from documentation, and
  the second was a version I would otherwise have picked wrongly and discovered at apply time — in the
  owner's subscription, not mine.
- **Worked:** the owner's answer to "one slice or two" was worth asking for. The shape they picked
  (a warm container per conversation) is not an optimisation — a cold start per message is ten seconds
  on every reply — and building the cheap version first would have shipped something that met every
  acceptance criterion and was unpleasant to use.
- **Didn't — I ticked two tasks that were false, and caught it only on re-reading.** 5.5 claimed an
  E2E that "sends a message and reads the reply and its cost"; mine deliberately does not, because
  sending clones a repository and calls a model. 5.1 claimed an Automation still runs on a Story with
  an open conversation — the design's *headline* claim — with no test behind it at all. The first is
  now written as the scoped thing it is; the second is a real test. Ticking a checklist from memory of
  intent rather than from what exists is the same failure as asserting a proxy signal (ADR-0004), one
  layer up.
- **Didn't — the third false mutation of the session.** Removing the panel from the tab left an
  unused import, so the build failed and the "2 failed" it printed proved nothing about the test. This
  has now happened with a formatting break (#13), an incomplete mutation (#165) and an unused import
  (here). The rule that keeps being relearned: **check the mutated build reached zero errors before
  reading the result.** It is written into the loop prompt now, which is where it should have been
  after the first time.
- **Also:** EF turned every conversation message into an update of a row nobody had written. A child
  reached through a tracked parent's navigation is treated as existing when its key is already set,
  and `BaseEntity` sets a GUID v7 in its constructor — every other aggregate here escapes it by being
  `Add`-ed explicitly. Worth remembering the next time a collection is reached only by navigation.
- **Next time:** when a slice ends with tasks to tick, re-read each one against the artifact rather
  than against the intention. Two of eighteen were wrong, and both were wrong in the flattering
  direction.
- **Time invested:** not measured (source: **manual** — eighty-fifth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. ADR-0010's rule (a habitat fact is asked, never inferred) did its job twice here
  and is cited above rather than restated.

## 2026-08-01 — prompt-only-catalogue (#162)

- **Worked:** reading the executor before trusting my own proposal. The first draft removed the
  rubric path "with the grill" — and #150 had quietly made that same column how a RepositoryPrompt
  names its file, so the removal would have broken the one action that survives. Caught at
  proposal-review, renamed instead of removed, and the correction is its own commit. ADR-0009's
  rule (a claim about existing behaviour cites where it lives) is what caught it, applied to my own
  artifact.
- **Worked:** deleting tests instead of adapting them, and saying which was which. Five ceremony
  suites and the defaults suite went outright. Two more asserted guarantees the change removes —
  "phase one publishes nothing", the published link — and were rewritten to what is still true: the
  plan is stored, the work does not proceed unapproved, the link is null. An adapted test for a
  removed behaviour asserts nothing and reads as coverage.
- **Didn't — my retirement sweep missed one of seven actions.** The list I swept omitted Estimate,
  and eight test payloads kept sending it; the 400s looked like missing prompts until one was read
  properly. A sweep whose input list is typed from memory has the same failure mode as a checklist
  ticked from memory (#166's retro): wrong in the flattering direction, found by the leftovers.
- **Didn't — two sweeps, two shapes missed.** The first prompt-path sweep matched only literal
  runtime strings; payloads passing `runtime` as a variable and a helper already naming its own
  prompt both slipped through, one producing a duplicate key. Mechanical edits over test corpora
  need a verification pass of their own — the failures list, re-read, was that pass.
- **Also:** the dormancy is now stated in three places rather than discovered in any — the design
  (D5), ARCHITECTURE.md's resume section rewritten from "exists" to "stands dormant", and DEC-062's
  cost list. Nothing reaches AwaitingInput; a prompt can ask by commenting but cannot pause its Run.
- **Also:** every Run resolves the project PAT twice now — the prompt read and the runtime — and
  the resolution-order test states it rather than hiding it. Names only, still (BR-010).
- **Next time:** when a change makes N things into one, enumerate the N from the code, not from
  recall, before sweeping — the enum was right there and the sweep list was typed by hand anyway.
- **Time invested:** not measured (source: **manual** — eighty-sixth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. ADR-0004 and ADR-0009 each did their job once here and are cited above.

## 2026-08-01 — prompt-scratchpad (#189)

- **Worked: counting the criteria instead of arguing the shape.** The issue left "a conversation or
  its own thing" open. Nine of its twelve acceptance criteria turned out to be existing properties of
  `HoldConversation.Say`, each citable to a line — so the decision was a table, not a preference, and
  the change landed with no new slice, no new permission, no aggregate and no migration.
- **Worked: reading both call sites rather than only the one being changed.** That is where the two
  findings the issue did not carry came from. A Run framed a Story with its number, state, labels and
  a bounded description; a conversation sent title and body unbounded — so the scratchpad, built on
  the second, would have tried prompts against a different input than the Run gives them. And the
  message cap was 10,000 characters against a largest-real-prompt of 9,741: the surface would have
  refused what it exists to author, measured before it was written rather than discovered by a user.
- **Didn't — I wrote a test that the change it tests does not affect.** The length test first
  asserted only that a 9,741-character message was accepted. The *old* 10,000 cap allowed that too,
  so it passed under the reverted bound and proved nothing. The mutation check found it; reading it
  had not. New rule, and the one worth carrying: when a test defends a *number*, assert the edge the
  number moved to, not a value that sat inside the old one as well.
- **Also: a task marked done for a reason, not a result.** 4.4's permission coverage is not a test I
  wrote — the criterion is met by adding no request at all, so the refusal is already policed by
  `ProjectRoles_Should_Constraint` and exercised by `ProjectRoleAssignment_Should_Constraint`. The
  task file says that in full rather than carrying a tick that would read as a test somebody could go
  and find. Same for 4.5's E2E, which cannot assert a reply in a habitat that would call a real model.
- **Also: one kit addition.** `shared/ui/textarea.tsx` — a prompt is a document, and the kit had no
  multi-line input. Same border, ring and invalid state as `Input`, because a field that looked like
  a different product for being taller is the drift the design contract exists to stop.
- **Next time:** for every test defending a threshold, ask "what value would fail under the old
  behaviour" before writing the assertion — that question is what the mutation check ended up asking
  for me, one round later than it needed to.
- **Time invested:** not measured (source: **manual** — eighty-seventh consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. This is the second consecutive change where the mutation check caught something
  reading did not (#162's three false reds, this one's false green) — ADR-0004 already carries that
  rule, and applying it is what worked both times, so it graduates nothing new.

## 2026-08-01 — starter-prompts (#190)

- **Worked: measuring the reference implementation instead of citing it.** The issue named
  `ds-connect`'s six commands as the model. Grepping them for what they read showed five of six
  depend on documents a fresh repository does not have — a definition of ready, a retro log, the
  `openspec/` layout. That measurement turned "ship the six" into a real product decision (tier them,
  label them by prerequisite) and it took one command to find. The issue had been open with the wrong
  premise; nobody had opened the files.
- **Worked: settling the stated difficulty by argument, then checking it.** The issue called
  agent-writes-versus-portal-offers "the whole difficulty". Three checkable facts settled it — the
  content is deterministic and already held, the no-overwrite criterion is structural in one shape
  and an unenforceable prompt instruction in the other, and an agent's pull request is not less human
  work than a commit. Posted with the argument and confirmed rather than assumed.
- **Didn't — I nearly shipped a second vacuous assertion, one change after learning the lesson.**
  The first "asking for the set writes nothing" test asserted an empty document dictionary, which is
  just "nobody seeded one". Caught while rereading, not by a mutation. The real position is that the
  guarantee is held by the type — the handler's only seam has no write on it — so the test asserts
  what a type cannot: the reads are one per starter and nothing else. #189's retro said to ask "what
  value would fail under the old behaviour"; the generalisation is **ask what state would make this
  assertion false, and if none would, delete it.**
- **Didn't — the catalogue had a collision I only found while writing its test.** Both tiers ship an
  `implement.md`, so they resolved to one path: taking both was impossible and the presence report
  would have marked two entries for one file. The manifest carries an explicit saved name now, and a
  test pins that no two starters land on the same path — the fix is the invariant, not the rename.
- **Also: the frontmatter stripper moved to BuildingBlocks.** The criterion is that a starter behaves
  identically run by this product or a local agent CLI; that is only testable against the *same*
  routine the Run path calls. Second time in two changes that a promise about fidelity forced a
  private helper to become shared — #189 did it for the Story description.
- **Next time:** when an issue names a reference implementation, read it before writing the proposal.
  Both of this change's real decisions came out of the six files the issue pointed at, and neither
  was visible from the issue's own description of them.
- **Time invested:** not measured (source: **manual** — eighty-eighth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. The vacuous-assertion finding is the second occurrence and would normally
  graduate — but ADR-0004 already carries "assert the artifact", and what is new is a sharper test
  for it, recorded here rather than as a fourth restatement of the same rule.

## 2026-08-01 — session-pool-deployable (#193)

- **Worked: running the command before handing it over.** The owner asked for the terraform-and-
  deploy script to bring #166's session pool up. Writing it, I ran `terraform plan` against the real
  state — and it fails. The pool has never existed, and the committed config could not have created
  it. Had I written the steps from the files alone, as I nearly did, the first thing the owner would
  have seen is a schema error I could have found in one read-only command.
- **Worked: eliminating rather than guessing the cause.** The error named a `name` field against
  `^[a-z][a-z0-9]*$`. The obvious suspect was the container's hyphenated name; renaming it did not
  clear the error, which is what proved the offender was the environment variables —
  `AZURE_CLIENT_ID` and `Secrets__KeyVaultUri`, neither of which can be lowercased. One extra plan
  turned a plausible fix into a demonstrated one.
- **Didn't — the pool was missing from a pattern three other workloads already follow, and nobody
  compared.** Placeholder image, `ignore_changes`, rolled by `deploy.sh`, asserted at the end: the
  portal, the migration job and the dispatch job all do this. The pool did none of it, and #166's
  own comment rationalised the gap ("a pool has no revision to roll") rather than noticing it. The
  check that would have caught it is mechanical — when adding the Nth of a kind, diff it against the
  other N−1.
- **Also: #92's lesson had a hole in it.** `deploy.sh` reads back the *running* image and refuses to
  claim success on a mismatch — for two of the three workloads it deploys. An assertion that covers
  most of what it deploys reads exactly like one that covers all of it.
- **Also, named rather than buried:** applying moves `operator_queue_data` from the CI deploy
  identity to whoever applies, because it is scoped to the current client config. Harmless — CI's
  grants come from `ci-identity.sh` and `deploy.sh` never touches the queue — but it oscillates with
  whoever applied last, and that belongs in an issue rather than in a plan somebody reads at speed.
- **Next time:** treat "give me the command" as "run the read-only half of the command". Plans,
  `--dry-run`, `--what-if` and `state list` cost nothing and are the difference between a script and
  a guess.
- **Time invested:** not measured (source: **manual** — eighty-ninth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. ADR-0004's "assert the artifact" already covers the missing pool assertion; this
  is a second instance of it rather than a new rule.

## 2026-08-01 — deploy-red-duplicate-grant (#196)

- **Didn't, and this is the headline: I merged five changes onto a deploy pipeline that was red, and
  never looked.** `deploy.yml` runs on every push to `main` — plan, apply, `deploy.sh`, verify — and
  it has failed on all five runs since #166 landed. Every one of those merges reported green,
  because what I watched was `gh pr checks`, which is pre-merge CI. The deploy is a different
  workflow that runs after, and nothing surfaces its result to whoever merged. Green PR checks were
  never evidence that the thing deployed.
- **Worked: the owner's question, not my process, is what found it.** "So the dev deploy will run
  this?" — a question I could only answer by reading `deploy.yml`, which is when the five red runs
  became visible. I had already told them the release needed a local `terraform apply`; the workflow
  does that itself. Answering from the file rather than from memory corrected both.
- **Worked: fixing the first fault exposed the second.** #193 got the pipeline past Plan, and Apply
  then failed on a duplicate role assignment — `session_acr_pull` granting AcrPull to a principal
  that `dispatch_acr_pull` already granted it to. Sequential faults only surface sequentially, which
  is an argument for fixing a red pipeline immediately rather than batching.
- **Also: the duplicate's own comment explained why it was redundant.** *"A session gets the dispatch
  identity's entitlements rather than the portal's"* — it **is** that identity, so it holds what that
  identity holds. A comment that states the premise of its own deletion is a strong signal, and it
  survived review twice.
- **Next time — the concrete change:** `/aio:sync` watches PR checks before merging and stops there.
  It should watch the deploy run the merge triggers, too. A merge that lands a red deploy is not a
  finished change, and the current loop cannot tell the difference. Filed rather than assumed, so
  the fix is a decision rather than a reflex.
- **Time invested:** not measured (source: **manual** — ninetieth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none yet, and this is the second occurrence of "assert the artifact, not the exit code"
  at the *pipeline* level rather than the command level — ADR-0004 covers the command. If the sync
  change above lands, that is where the rule belongs; recording the second occurrence here so the
  graduation rule has its count.

## 2026-08-01 — one-warm-session (#198)

- **Worked: watching the deploy this time.** #196's retro said a merge that lands a red deploy is not
  a finished change. Doing it immediately is what surfaced this: the apply got past the duplicate
  role assignment and ARM refused `readySessionInstances = 0` on the next line. Three faults stood
  between the committed pool and a working one, each reachable only once the previous cleared, and
  batching them would have taken three more days of red.
- **Worked: escalating a decision rather than defaulting it.** Zero was a *stated* decision with a
  cost argument (DEC-061). The platform refusing it is not a licence to pick a number — an
  always-billed container in dev is ongoing money. Asked, with the smaller-box and no-pool
  alternatives priced honestly, and the owner chose the full size for a reason worth recording: an
  agent cloning a repository and running a model is not a small workload.
- **Didn't — I have been trusting `terraform plan` as though it validated against Azure.** It does
  not. azapi's embedded schema accepted `readySessionInstances = 0`; only the PUT refused it. In the
  same week the same schema *rejected* env var names ARM would have accepted (#193). It is wrong in
  both directions, and I treated a green plan as evidence twice.
- **Next time:** for `azapi` resources specifically, say "the plan passed" and never "this will
  apply". The escape hatch's validation is a convenience, not a contract, and `ARCHITECTURE.md` now
  says so where the seam is described.
- **Also:** DEC-063 revises DEC-061's cost clause only. One conversation is still one container is
  still one project's PAT — the isolation argument the shape was chosen for is untouched, and it is
  worth stating that a revision is narrow rather than letting a reader assume it reopened everything.
- **Time invested:** not measured (source: **manual** — ninety-first consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. The plan-is-not-a-promise finding is specific to one provider and lives in
  `ARCHITECTURE.md` beside the seam; promoting it to an ADR would generalise a claim that only holds
  for `azapi`.

## 2026-08-01 — named-operators (#195)

- **Worked: the decision was already made, one variable over.** The issue offered two shapes for
  "who may reach the dispatch queue by hand" — a list variable, or move it out of Terraform. This
  repository had already answered the general question: `bootstrap_admins` carries a comma-separated
  list of object ids from a repository variable, with "empty is a real and honest state, not a safe
  default" written into its own description. Reading the neighbouring variable settled it in one
  step, with an argument stronger than a preference: consistency with a shape this codebase already
  defends.
- **Worked: keeping the lists separate, and saying why in the code.** Reusing `bootstrap_admins`
  would have been fewer moving parts and wrong — adding a portal administrator would silently grant
  them Azure data-plane access. Two different powers, two lists, and the comment states that so the
  next person to notice the duplication does not "simplify" it.
- **Didn't — nothing, and that is worth recording as the exception.** This change had no false
  green, no vacuous assertion and no mid-flight discovery. What made the difference is that it was
  scoped by an issue written *after* the fault was measured in a real plan, rather than from a guess
  about what might be wrong. A well-measured issue is most of an easy change.
- **Also: the wiring is deliberately incomplete.** `deploy.yml` does not pass
  `TF_VAR_operator_object_ids`, because putting an empty repository variable behind a deliberate
  empty default would look like configuration and behave like nothing. The PR says exactly what the
  two lines are when somebody wants the grant.
- **Next time:** before proposing options for "how should this fact be carried", grep for the fact's
  nearest neighbour. This repository answers most such questions somewhere, and matching it is a
  better argument than reasoning from first principles about a value nobody disputes.
- **Time invested:** not measured (source: **manual** — ninety-second consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none. Applying an existing convention is what an ADR is *for* — recording one here would
  restate `bootstrap_admins`' own reasoning in a second place.

## 2026-08-01 — sync-watches-the-deploy (#202)

- **Worked: the retro from #196 became a change rather than a sentence.** It said "a merge that lands
  a red deploy is not a finished change, and the current loop cannot tell the difference" and named
  the fix. That fix is now step 11. A retro finding that stays a finding is a finding nobody acts on.
- **Worked: making it a report rather than a gate.** By the time sync could watch a deploy, the merge
  is irreversible and the issue is already `status:done` — a gate there would block nothing and
  imply the change could be held back. What a red deploy needs is to be *seen*, so sync says both
  true things: the change merged, and the deploy failed.
- **Didn't — #190's design D4 was tested within the hour and I overrode it.** The starter set's copy
  of this workflow is uncoupled from the command on purpose, with drift as the accepted cost and no
  match test. The first edit after that decision was one where the drift mattered: a starter that
  teaches the weaker loop is worth less than the two-file edit saved. Synced by hand, with the
  reasoning in the commit. D4 is not wrong — it is about not coupling them *mechanically* — but "the
  cost is acceptable" was easier to write before it arrived, and it arrived immediately.
- **Also, the measurement worth keeping:** the four session-pool faults were sequential, each only
  reachable once the previous cleared. That is the real argument for watching, stronger than
  "failures should be visible" — a red pipeline does not just hide one fault, it hides the queue of
  faults behind it.
- **Next time:** when a decision accepts a cost, note what the first occurrence of that cost would
  look like. D4 said drift was acceptable without saying how anyone would notice it, and the answer
  turned out to be "when somebody edits the original for a reason that also applies to the copy".
- **Time invested:** not measured (source: **manual** — ninety-third consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none. This is ADR-0004's "assert the artifact" applied to the pipeline rather than a new
  rule, and the third occurrence of that shape — recorded so the graduation count is honest, but the
  rule already exists and is already cited.

## 2026-08-01 — workload-profile-environment (#200)

- **Worked: measuring the blast radius before proposing anything.** Adding the block on a scratch
  branch and planning gave `6 to add, 0 to change, 4 to destroy` with the exact resources named —
  which turned "we might need to rebuild dev" into a decision with a survives-and-does-not list. The
  owner chose from facts rather than from my summary of a risk.
- **Worked: refusing to commit it while it was still a question.** Merging is what applies it, so
  committing before the decision would have rebuilt dev at whatever moment the next merge landed.
  The gap between "I know the fix" and "the fix is authorised" is exactly where a deploy-on-merge
  pipeline turns a plan into an event.
- **Didn't — four faults, one at a time, over a day.** azapi's schema, a duplicate role assignment,
  `readySessionInstances = 0`, the environment type. Every one was a plan or an apply away from
  being known on the day #166 merged, and none was found until somebody asked whether the deploy
  ran. The sequence is the lesson: a red pipeline hides the queue behind the first fault, and each
  fix bought exactly one step of visibility.
- **Also: the unautomated step is the one that will look like a bug.** After the hostname changes,
  sign-in fails until `entra-app.sh` re-registers the redirect URIs — Entra matches them to the
  character. It is now in `infra/README.md` beside the block that causes it, because a consequence
  documented in a commit message is a consequence nobody reads twice.
- **Next time:** when a resource is declared but has never been applied, treat it as unverified
  regardless of how carefully it was reviewed. #166's pool passed spec review, code review and CI,
  and was wrong in four independent ways — because none of those gates run a plan against real
  state, and the one that does runs after the merge.
- **Time invested:** not measured (source: **manual** — ninety-fourth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new, but the count is worth stating: this is the fourth occurrence this session of
  "a green gate that does not exercise the artifact proves nothing" (ADR-0004). #202 turned it into
  a workflow step rather than a fifth restatement.

## 2026-08-01 — refresh-oidc-before-release (#205)

- **Worked: watching the deploy caught it the same hour.** #202 landed one merge before the rebuild,
  so the failure was seen immediately instead of at the next person's next question. That is the
  first return on the change, and it arrived within the hour of shipping it.
- **Worked: reading the error rather than assuming it was mine.** `deploy.sh` had two commands in it
  I had written without verifying against a live pool, and the natural guess was that one of them
  broke. It was neither — `AADSTS700024`, the job's own sign-in had expired mid-run. Checking the
  log before touching the suspect saved a wrong fix, and the pool then confirmed both commands were
  right.
- **Didn't — five faults, and every one of them was reachable from a plan or an apply on the day
  #166 merged.** The provider schema, a duplicate role assignment, a ready-instance count, the
  environment type, and now a job outliving its credential. None was found by spec review, code
  review or CI, because none of those exercises the artifact against Azure. The deployed pool was
  reviewed carefully and wrong five times.
- **Also: the pool exists, and two things I had written unverified turned out correct.** The
  read-back query path in `deploy.sh`, and the ARM shape of the container template. Recorded in
  `conversations.tf` rather than left as a comment saying "unverified" that nobody would revisit —
  a caveat that outlives its uncertainty is its own kind of stale.
- **Next time:** an environment rebuild is not just slow, it is long enough to cross timeouts nobody
  measured. Before a change that replaces infrastructure, ask what in the pipeline is bounded in
  minutes — credentials, job timeouts, health-check retries — and whether the new duration fits.
- **Time invested:** not measured (source: **manual** — ninety-fifth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none. This is a workflow fix with its reason at the site; the general lesson is the same
  "exercise the artifact" ADR-0004 already carries, now with a workflow step (#202) enforcing it.

## 2026-08-01 — catalogue-inside-the-module (#207)

- **Didn't — I shipped an embedded resource pointing outside the project and never built an image.**
  #190's catalogue lived at `prompts/starter/` with a `..\..\..\..` glob. Every Dockerfile copies
  `src/`; none copies `prompts/`. All four images broke at `dotnet publish`, and I had run the
  solution build, the unit tests, the functional tests, the E2E suite, the design gate and CI — none
  of which builds a container. The one check that mattered was `docker build`, and it takes two
  minutes.
- **The structural finding: no workflow builds the images that ship.** `ci.yml` and `build-test.yml`
  have no `docker build` and no `Dockerfile` reference. The images are first built during the
  deploy, after the merge — so any change that compiles can still be unbuildable, and only a release
  finds out. Left open in #207 rather than folded in, because it is a pipeline change and this was a
  fix.
- **Worked: choosing the fix that removes the class.** `COPY prompts/` in four Dockerfiles would
  have worked and left four places to remember. Moving the catalogue inside the project makes it
  travel by construction, and resource names did not change, so the 51 tests passed at the same
  count — which is itself the evidence the move was inert.
- **Worked: verifying inside the artifact.** All four images built, and the resource name was read
  out of the portal image's own assembly rather than inferred from a green compile. That is exactly
  the check #190 was missing, run at the point where it would have failed.
- **Also: the archived design gained a D3a rather than an edit.** The bundle is history; correcting
  the original in place would erase the fact that the decision was made and was wrong.
- **Next time:** an `EmbeddedResource`, `Content`, or `None` item whose path leaves the project
  directory is a build-context question, not a project question. Ask "which build contexts include
  this path" before "does it compile".
- **Time invested:** not measured (source: **manual** — ninety-sixth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none. Sixth occurrence this session of "a green gate that does not exercise the artifact
  proves nothing" (ADR-0004). The rule is not missing; the gates are, and #202 and #207 are the two
  places that is being fixed.

## 2026-08-01 — the session pool reached a real environment (session close-out)

Not a change of its own — the record of what it took, because the count is the finding.

**Seven faults stood between #166's declared session pool and a running one**, each reachable only
once the previous cleared:

1. The azapi provider's embedded schema rejected environment variable names ARM accepts (#193).
2. `deploy.sh` never rolled the pool's image, and the pool was absent from the running-image
   assertion #92 exists to enforce (#193).
3. A duplicate `AcrPull` grant returned 409 and stopped every apply (#196).
4. `readySessionInstances = 0` — a *stated decision*, DEC-061 — is not a value Azure accepts (#198).
5. The environment was Consumption-only; sessions require workload profiles, and Azure does not
   convert in place, so the fix was a rebuild (#200).
6. The rebuild took eighteen minutes and the OIDC client assertion lasts five, leaving the release
   unauthenticated (#205).
7. An embedded resource outside the project directory was absent from all four Docker build
   contexts (#207) — mine, from #190, shipped the same day.

**Every one was reachable on the day it merged**, from a `terraform plan`, an apply, or a two-minute
`docker build`. None was caught by spec review, code review, 399 functional tests, 29 E2E tests, the
design gate, or CI — because not one of those exercises the artifact against Azure or builds a
container.

**What actually found them:** the owner asking "so the dev deploy will run this?". Five red deploy
runs had accumulated while every PR reported green, because `/aio:sync` watched PR checks and
stopped there.

**The two changes that make it structural rather than a lesson:** #202 put the deploy watch into
`/aio:sync`, and it paid within the hour — fault 6 was seen immediately instead of at the next
question. #207 left "CI must build the images" open as the remaining hole, which is where fault 7
would have been caught.

**Verified now, in the artifact:** portal, dispatch worker and session pool all running
`…:54403658a6bc3eb31346dc925dc13e665bede74a`; `/api/health` → 200; the portal carries
`Conversations__SessionPoolEndpoint`, so conversations and the prompt scratchpad execute in a session
container holding the project's credential rather than in the portal — which is what DEC-030 and the
whole session boundary exist for, and the first time it has actually been true.

**Still open and owner-only:** #183 (a Key Vault Crypto Officer grant), and re-registering the Entra
redirect URIs against the new hostname — sign-in is broken until then, and that is the rebuild's
documented consequence rather than a fault.

## 2026-08-01 — local-code-source

**Time (manual — telemetry capture is broken in this worktree session, and that is a finding, not
a footnote):** `verify-telemetry.mjs` fails three checks here — `OTEL_EXPORTER_OTLP_ENDPOINT`
unset in the shell the worktree session inherits, `usage.jsonl` empty, and the SessionStart hook
never fired, so `.telemetry/sessions.jsonl` does not exist in the worktree. Everything measured
about this change is therefore unmeasured. Roughly one focused session: proposal through green
suites.

**What worked:** deciding locus as a *workspace* fact, not a routing fact (design D1) — the queue,
worker and Aspire wiring have zero diff, and the whole feature sits behind one existing seam plus
one new sibling interface. The derived-factory trick (`WithWebHostBuilder` +
`Identity:Mode=LocalOwner`) let self-host and cloud postures be tested against one shared
container stack, and the cloud-absence test is just the untouched fixture. Reusing the BR-001
pre-write refusal shape for BR-016 meant the dirty-tree rule needed no new machinery anywhere.

**What didn't:** the executor's cancellation-race `ReloadAsync` silently discarded the unsaved
locus audit fields — a fact recorded in memory and wiped one line later. Only the functional
test's `workingFolder` assertion caught it; nothing about the code looked wrong. Also the
telemetry gap above: worktree sessions inherit neither the OTel endpoint nor the session-mapping
hook, so any change built in a worktree loses its measurements.

**One change next time:** persist an audit fact in the same `SaveChanges` moment it becomes true,
never batched with a terminal state that a reload can precede (applied here; worth keeping as the
pattern). And wire worktree sessions into telemetry — endpoint + SessionStart hook — before the
next change is built in one; if a second worktree change loses its measurements the same way,
that is the graduation point for an ADR.

## 2026-08-01 — default-automations-setup

**Time (manual — same broken worktree telemetry as local-code-source, same session, same three
failing checks):** a short session; the change is one use case, one manifest extension and three
tests.

**What worked:** putting the wiring in the manifest instead of the handler — the "which
Automations, wired how" question became catalogue content the existing enumeration test could
guard, and the handler shrank to convergence mechanics. Convergence-not-insertion as the
contract made every hard case (odd-cased existing trigger, lost uniqueness race, second
invocation) the same answer: skip and say so.

**What didn't:** nothing structural; the only friction was remembering that a lost
`DbUpdateException` race leaves the loser in the change tracker — it has to be detached or the
next `SaveChanges` in the same loop replays the failure.

**One change next time:** this is the second change in one session whose measurements were lost
to the worktree telemetry gap named in the previous entry — by that entry's own graduation rule,
the ADR is now due. Writing it should ride with whichever change next touches the telemetry
plumbing rather than blocking this one.

## 2026-08-02 — prompt-picker

**Time (manual — worktree telemetry still broken; `node .config/otel/verify-telemetry.mjs` fails
three checks: the exporter is enabled but `OTEL_EXPORTER_OTLP_ENDPOINT` is unset, `usage.jsonl`
has never received bytes, and the SessionStart mapping hook has never fired in a real session):**
one session; a seam read, a query use case, a form field and their tests.

**What worked:** modelling degradation as *data* rather than as an error (design D3) — "no
Connector", "the vendor refused" and "nothing there yet" all arrive as a `reason` on a 200, and
the form renders the plain textbox it always was. That kept discovery non-load-bearing by
construction: no try/catch in the component, no way for a picker failure to block configuration.
Distinguishing an absent directory (`null`) from a vendor refusal (the error) at the seam meant
the caller never had to guess which of the two it was holding.

**What didn't:** `ErrorOr`'s two `From` overloads are ambiguous for a nullable reference type —
`ErrorOrFactory.From<IReadOnlyList<string>?>(null)` does not compile, and the fix (a typed local,
then `From(local)`) had to be applied at four sites across two connectors and two fixtures. Also:
adding one seam method touches every fake, including two in modules that never call it.

**One change next time:** when a seam gains a method whose success value is nullable, write the
typed-local form immediately rather than discovering the overload ambiguity per call site. And
the telemetry gap has now cost three consecutive changes — ADR-0011 records it as the graduation
point the `local-code-source` entry called for, with the fix owed to whichever change next
touches the telemetry plumbing.

## 2026-08-02 — install-starter-prompt

**Time (manual — the same broken worktree telemetry ADR-0011 now records; three failing checks,
unchanged since the previous entry):** one session, riding directly on the seam widened above.

**What worked:** reusing the Run ceremony's publish pipeline rather than writing a second one.
The install path is clone → write one file → commit → push → draft PR, and every stage-named
refusal (`Workspace.CloneFailed`, `PushFailed`, `PullRequestFailed`) came for free with the voice
implement already speaks. Re-checking presence through `IDocumentReader.ReadPrompt` — the same
read a Run resolves prompts with — meant the path the refusal names is provably the path a Run
would read, rather than two implementations agreeing by inspection.

**What didn't:** the e2e suite encoded #190's "no writes" absolute as
`TheSurface_Should_OfferNoWayToWriteAny`, and it failed on CI *after* the PR was marked ready —
the local functional tier never runs it. A deliberately reversed invariant is exactly the kind a
test asserts somewhere the implementer does not look; the narrowing had to be written into the
test as well as the spec.

**One change next time:** when a change reverses a stated absolute, grep the e2e tier for the old
wording before pushing — the spec delta and the assertion that guards it live in different trees,
and only one of them is in the change bundle.

## 2026-08-02 — local-execution-ui

**Time (manual — the worktree telemetry gap of ADR-0011, unchanged):** one session across five
surfaces, all frontend on already-landed endpoints.

**What worked:** reading the mocks from the design project itself rather than from memory. The
five screens (3a–3e) and their three written specs pinned every detail the issue summarised —
target sizes, which control is a radiogroup, that the pod card is *disabled with its reason*
rather than hidden, that presence is unknown rather than absent without a Connector. Building
`LocusChip` as one shared component is what makes "local" identical on the projects list and the
Run page, which the spec asks for in words and a shared component enforces.

**What didn't:** the posture probe has no dedicated endpoint. The spec's rule is "404 for the
whole surface", so the probe asks the surface itself with a deliberately invalid validate-path
call and reads only the status — correct, but it spends a request to learn a fact the deployment
already knows. A `GET /api/capabilities` would say it once.

**One change next time:** when a spec expresses a capability as "this whole surface 404s", give
the frontend something to *ask* rather than something to *infer* — the inference works and reads
like a trick, which is a cost paid by every future reader of that hook.
