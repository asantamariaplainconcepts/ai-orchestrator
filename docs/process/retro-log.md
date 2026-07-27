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
