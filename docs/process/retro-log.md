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

## 2026-08-02 — connector-form-essentials

**Time (manual — the worktree telemetry gap of ADR-0011, same three failing checks):** one
session; a form restructure, no backend.

**What worked:** letting the API's *conditional* validation decide the shape. The question "what
may be folded away?" had an answer already written down — `LocalPath` is required-and-absolute
under LocalFolder, the credential is exclusive-or — so the disclosure's lock and the credential's
destructive swap are enforcements of rules that already existed rather than new invariants to
maintain. Making the swap discard the replaced value put the exclusive-or in the state
transition, where it cannot be lost, instead of in the submit handler where it could.

**What didn't:** the controlled `<details>` bug. `open={state}` plus an `onToggle` that sets the
state back to `true` does nothing when it is already `true` — React re-renders nothing and the
panel closes anyway. Refusing the gesture (`preventDefault` on the summary) is the fix; correcting
after the fact cannot work for any controlled element whose native behaviour mutates the DOM.
Separately, verification kept hitting fixture gaps: mock mode had no route to save a Connector at
all, and returned the same Connector for every project, so neither "a fresh project asks four
questions" nor "the hidden field is also cleared" was observable until both were added.

**One change next time:** the previous entry's rule paid off — grepping the e2e tier before
pushing caught five tests driving a select this change deleted, plus one filling a field it moved
behind a disclosure. Keep doing it, and extend it: when a change removes or relocates a *labelled
control*, grep the e2e tier for that label, not just for the invariant's wording.

## 2026-08-02 — deployment-capabilities

**Time (manual — the worktree telemetry gap of ADR-0011, same three failing checks):** one
session; one query use case, its tests, and the portal's switch away from the probe.

**What worked:** reading the composition before writing the spec. The issue — and my own grill of
it — claimed that self-host should hide "name an existing secret", because there is no vault.
`AddSecretResolution` says the opposite: a resolver is composed in every habitat (Key Vault, the
protected-file store, or `ConfigurationSecretResolver` over `Secrets__<name>`), while *storing*
needs an `ISecretStore` that writes, and without one the composition registers
`UnavailableSecretStore` whose every write throws. So the capability is `canStoreSecret`, the
hidden control is **pasting**, and the condition is not the posture at all — a self-host
deployment with `Secrets:Directory` stores perfectly well. Catching that before implementing cost
one read; catching it after would have shipped a form that hides the working option.

**What didn't:** nothing structural. The functional tests deliberately assert the two capabilities
*independently* — they coincide in every habitat we run, and a test that only ever saw them
together would not notice one being derived from the other, which is exactly the mistake the
proposal made in prose.

**One change next time:** when a grill's premise is "X is unavailable in habitat Y", read the
composition that decides X before writing the acceptance criteria. The premise here survived a
grill, an issue body and a proposal — three documents — because nobody had opened
`AddSecretResolution`, and ADR-0009's rule ("a claim about existing behaviour cites where it
lives") is exactly the discipline that would have caught it at the first of the three.

## 2026-08-02 — least-privilege-connector

**Time (manual — the worktree telemetry gap of ADR-0011, same three failing checks):** one
session; one capability set, both connectors, a new read, and the surfaces that state it.

**What worked:** making the capability set a single function that *both* consumers read. The
question "what do we ask for" and the question "what do we verify" had drifted apart for as long
as they were separate lists — verification probed two reads while DEC-030 granted five scopes —
and one function makes the drift impossible rather than merely unlikely. Pairing each capability
with its vendor scope name in the same type means a capability without a scope does not compile,
so the documentation cannot rot away from the code.

**What didn't:** two self-inflicted test failures, both instructive. I added two properties to a
shared stub and forgot `Reset()`, so state leaked between tests in the same class — the exact trap
the Projects fixture documents in a comment I had read earlier the same session. And a regex
rewrite of a stub method was greedy across method boundaries and silently ate two others; the
build caught it, but a narrower edit would not have needed catching.

**One change next time:** when a change widens a shared test stub, add to `Reset()` in the same
edit as the property — not as a follow-up when a test goes red. And prefer an anchored
string replacement over a regex when editing code: `re.search(..., re.S)` across a method body
will happily match to the end of the file.

## 2026-08-02 — local-dispatch

**Time (manual — the worktree telemetry gap of ADR-0011, unchanged):** one session; a second
substrate, the composition that chooses between them, and the habitat that drops a container.

**What worked:** the seam. `IRunDispatcher` is one method taking a Run id, so a whole second
substrate was a composition change — no module learned anything. And separating the two
registrations (`AddRunDispatch` for the producer, `AddRunDispatchConsumer` for the consumer) is
what let the dangerous half be refused where it would be made: composing a consumer in a habitat
that has a queue throws, naming the credential boundary it would erase. That refusal is a test,
not a comment.

**What didn't:** two stale declarations, both caught by CI rather than by me. The compose file is
*generated* from the AppHost and committed — changing the AppHost without regenerating it is
exactly what its drift gate exists to catch, and it did. Then the e2e suite waited two minutes for
a `dispatch` resource this change removes; 28 of 29 tests passed, so the habitat worked and only
the assertion was obsolete. Also self-inflicted: a `re.search(..., re.S)` rewrite of one method ate
two others, and a slice-based replacement swallowed an `if` block's opening brace. Both were caught
by the compiler, but a narrower edit would not have needed catching.

**One change next time:** #220's retro said to grep the e2e tier for a *label* a change removes.
Widen it: grep for any **name a change makes disappear** — an AppHost resource, a generated
artifact, a configuration key. Three of the four CI failures this session were the same shape,
"the change is right and something declared elsewhere still says otherwise", and the names are
what make them greppable.

## 2026-08-03 — wire-existing-pipeline

**Time (manual — the worktree telemetry gap of ADR-0011, unchanged; `verify-telemetry.mjs`
still fails on `OTEL_EXPORTER_OTLP_ENDPOINT` unset, `usage.jsonl` empty, and no session
mapping):** one session; discovery, adoption, gap-filling, and the surface for all three.

**What worked:** the catalogue stayed the single source of methodology. A pipeline step is a
manifest entry that carries wiring, so recognising `grill.md` is a data read — and the rule that
kept an opt-in tier from being installed unprompted is `Requires is null`, catalogue content, not
a branch in the handler. A fork that wants a different pipeline edits the manifest and this change
follows it. Extracting `StarterInstaller` rather than copying the publish path was the other one:
the second caller wanted the same ceremony for four files, and one seam means one set of
stage-named refusals instead of two that drift.

**What didn't:** I narrowed a seam in #215 and paid for it here. `ListDirectoryFiles` returned
file names because the picker only wanted file names — but the vendor response already
distinguished files from folders, and one change later discovery needed the folder names to find
`.claude/commands/ds`. Widening it cost six compile errors across two vendor implementations, two
test stubs and two consumers, and I found the need by writing code against a member that did not
exist rather than by reading the seam first. Separately, three Projects tests failed on a missing
`wwwroot` — the SPA had never been built in this worktree — which read as a regression for a few
minutes before it read as an environment gap.

**One change next time:** when a seam projects a vendor response, do not drop a distinction the
vendor already makes unless there is a reason to, and if you drop one, say so in the seam's own
doc comment. "Only the current caller needs this" is how a seam acquires a second shape a change
later. And the worktree friction is now three-dimensional, not just telemetry: `.claude/launch.json`
had no `autoPort`, so a second worktree session could not start a preview at all. Fixed here;
ADR-0011's premise — a worktree session inherits nothing the main checkout arranged — keeps
finding new surfaces, and the next one should be looked for rather than tripped over.

## 2026-08-03 — guided-automation-form (#231)

- **Didn't — I ran a frontend mutation check against a stale bundle and read the green as proof.**
  The E2E suite serves `wwwroot`, not the source, so mutating `.tsx` and re-running the tests
  exercises the *previous* build. The first mutation "passed" and I nearly recorded that as the
  check being done. Rebuilding with `pnpm build` turned it red immediately. New rule, and it
  generalises past this repo: **a mutation check is only valid against the artifact the test
  actually loads** — for a compiled frontend that means rebuilding, not editing.
- **Didn't — the payload test was a false green of exactly the #189 kind, three months on.** It
  typed no label before choosing "stop", so `withDraft()` returned `[]` under both the new and the
  old behaviour: an assertion the code it replaced also satisfied. #189's retro said "assert the edge
  the number moved to, not a value that sat inside the old one as well", and I wrote the same shape
  again in a different guise. What caught it was the mutation check, not re-reading — which is the
  argument for running one on *every* behavioural claim rather than the ones that feel risky.
- **Worked: the browser found what no test asked for.** Choosing "hand to the next step" before
  naming a label left the sentence reading "the chain stops there" — the single gap D2's
  name-what-is-missing rule failed to name. Nothing in the criteria covered it; opening the page and
  clicking did.
- **Worked: checking the sketch against current `main` before proposing.** The design bundle's
  turn 4 was written against an older tree. One of its three issues had partly landed in #229, and
  the `action` field it wanted regrouped turned out to be a one-option select ADR-0006 forbids
  removing. Both would have been discovered mid-implementation.
- **Also: the request-shape criterion had nowhere to live.** There is no frontend test runner in this
  repository — no vitest, no jest, no test files. Rather than tick it or invent a harness inside an
  unrelated change, it moved to an E2E that reads the stored Automation back from the API, which
  witnesses more than a component assertion would have.
- **Also — the archive tool caught a content loss I would have shipped.** My MODIFIED block was
  written from the requirement as it stood when the design bundle was authored, and refusing to
  archive told me it would drop ten scenarios added since by #215 and others. Rewriting a whole
  requirement to add prose to it is a delete-by-default operation; the delta should be refreshed
  from the live spec at write time, not at archive time.
- **Next time:** when a change is frontend-only and its evidence is E2E, put `pnpm build` in the
  mutation loop explicitly. The gap between "I edited the source" and "the test can see it" is
  invisible and silently converts a check into a formality.
- **Time invested:** not measured (source: **manual** — ninety-seventh consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. Both findings are ADR-0004's "assert the artifact" — now with the sharpest
  statement of it yet: the artifact is what the test *loads*, not what you edited.

## 2026-08-03 — vertical-workflow-canvas (#232)

- **Didn't — `rtk pnpm build` told me a failed build had succeeded, and I believed it.** The
  mutation broke TypeScript, the build errored, `wwwroot` kept the previous bundle, and the wrapper
  still exited 0 — so a chained `&& echo "built"` printed. The E2E then exercised the old artifact
  and passed, and the green read as "this test does not cover the behaviour". Three separate
  explanations were plausible and I chased two of them before checking whether the build had run at
  all. This is the same masking already recorded for `rtk git commit`, which means it is a property
  of the wrapper and not a quirk: **when a command's success is load-bearing, run it unwrapped and
  assert the artifact.** Saved to memory, since it outlives this repository.
- **Didn't — I repeated #231's finding one change later, having written it down.** The E2E serves
  `wwwroot`, so a `.tsx` edit is invisible until `pnpm build` runs. Recording a lesson in a retro did
  not stop me making it again within the hour; only putting the build into the loop will.
- **Didn't — a test asserted a scenario the product deliberately does not render.** A lone Automation
  never appears on the canvas ("No Automation hands work to another yet"), so seeding one and waiting
  for a node waited thirty seconds for something that was never coming. I had skim-read
  `workflowGraph.ts` and concluded a single node forms an orphan chain; the surface says otherwise.
  Dumping the rendered DOM answered in one run what three rounds of guessing had not.
- **Worked: measuring instead of eyeballing.** `shrink-0` on the node wrapper was correct for a
  scrolling row and wrong for a column — it stopped the card shrinking and reintroduced the exact
  horizontal scroll this change removes. Nothing in the diff looked wrong; comparing `scrollWidth`
  to `clientWidth` in a browser found it immediately.
- **Worked: sharing by meaning, then noticing what does not transfer.** Extracting `GateChip` was
  right; shipping the board's "dropping here starts a plan…" tooltip onto a canvas nothing is
  dropped onto was not. The hint became a prop. The general form: when two surfaces share a
  component, the parts that differ by *surface* must be parameterised, not inherited.
- **Also: the overflow assertion is scoped deliberately.** The page really does overflow at 375px —
  the project tab strip is 528px wide — but that predates this change, so the test asserts the canvas
  and the tab strip is filed separately. A test that fails for somebody else's defect is a test about
  the wrong thing.
- **Next time:** for any frontend change verified by E2E, the loop is edit → `rtk proxy pnpm build`
  → grep the bundle → run. Three of this change's four false signals came from skipping a step in
  that sequence.
- **Time invested:** not measured (source: **manual** — ninety-eighth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. ADR-0004 again, and the sharpest form yet: the artifact is what the test loads,
  and you have not verified a build until you have looked at its output.

## 2026-08-03 — setup-plan-before-build (#233)

- **Worked: checking the sketch against `main` before proposing, again.** The design bundle's
  issue-11 predated #229, which had already landed candidate discovery and most of the data a plan
  needs. Filing it as written would have asked somebody to rebuild #229. Grilling it against current
  code turned a large sketch into a narrow, true change — the second time in this batch that reading
  the repository beat reading the artefact describing it.
- **Worked: putting the plan where the data already was.** Discovery had every candidate's file list
  and the canonical steps are compile-time from the embedded catalogue, so the plan is a projection
  of a read that already happened — no new endpoint, no extra vendor call. A separate preview
  endpoint would have created a *second* place deciding what the build does, which is the bug this
  change fixes one level up.
- **Worked: deleting a test rather than weakening it.** The safety sentence only renders after
  discovery succeeds, which needs a Connector serving directory listings; this tier's GitHub stub
  answers issues only. My first draft asserted it anyway and failed — correctly. The choice was to
  extend the stub (its own change), weaken the assertion into something vacuous, or delete it and
  say why. Deleted, with the reason in the test's own summary and in the design.
- **Didn't — I wrote that test before checking what state the tier could reach.** Third time this
  batch that a test asserted a scenario the system does not produce: a lone Automation that the
  canvas never draws (#232), and now a card state the stub cannot reach. The pattern is writing the
  assertion from the criteria and only then asking whether the harness can get there. **Ask what the
  fixture can produce before writing what to assert.**
- **Also: removing #229's consent checkbox is a decision, not tidying.** It was right while the
  writing was invisible; the plan names the files now, and a consent restating a preview trains
  people past both. The safety property moved to the sentence beside the button rather than
  disappearing.
- **Time invested:** not measured (source: **manual** — ninety-ninth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none new. The recurring finding across all three of this batch is one sentence —
  *verify the harness can reach the state before asserting on it* — which is ADR-0004's family and
  now has three occurrences in a day. If it recurs once more it is worth its own record.

## 2026-08-03 — connector-below-the-step (#238)

- **Didn't — #232 shipped a visibly broken layout past four green assertions.** The chain container
  became a column; the wrapper holding a step and its connector stayed a row. Every rail and
  human-review marker rendered in a lane beside the steps. The tests asserted the *chain* was a
  column and did not scroll sideways — both true the entire time the bug was present. They were
  proxies for the outcome, and a proxy is exactly what a regression walks past.
- **What found it: a screenshot taken for a different task.** Capturing the Automations tab for the
  product manual (#237) put the thing on screen at full size, and it was wrong at a glance. Not a
  test, not a diff review, not the browser checks I *did* run during #232 — those queried
  `flexDirection` and `scrollWidth`, which is the same proxy in a different tool.
- **The pattern, now four for four this batch.** A payload asserted where old and new agree; a lone
  Automation the canvas never draws; a card state the E2E stub cannot reach; and now geometry
  asserted as a class name. Every one is *asserting something adjacent to the claim instead of the
  claim*. The rule that would have caught all four: **write the assertion as the sentence a reader
  would use to describe the outcome, then find the measurement that says exactly that.** "The chain
  is a column" is not "each connector sits below its step".
- **Worked: fixing it before documenting it.** The manual was one commit from shipping a screenshot
  of the defect, which would have made it canonical. Stopping to fix cost twenty minutes; a picture
  of a broken layout in `docs/` would have cost the next reader their trust in the manual.
- **Also: the new test measures pixels, deliberately.** `connector.top - step.bottom >= 0` is uglier
  than a class assertion and it is the only one that could not have passed while the bug existed.
- **Time invested:** not measured (source: **manual** — hundredth consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** this is the fourth occurrence of the assert-the-proxy shape in a single day, which is past
  the graduation rule. **ADR-0011 is owed**: an assertion must be the outcome stated as a
  measurement, not a property that correlates with it. Writing it is the next change, not a note
  appended here.

## 2026-08-03 — product-manual (#237)

- **Worked, and this is the finding: looking at whole screens found a bug four assertions missed.**
  The first capture of the Automations tab showed every connector in a lane beside the steps —
  #232's regression, green in CI, invisible to the browser checks I had run during that change
  because those queried `flexDirection` and `scrollWidth` rather than looking. Nothing in this
  repository had ever rendered a full surface and *looked at it* until a manual needed pictures.
- **Worked: fixing before documenting.** The manual was one commit from canonising a broken layout.
  Twenty minutes to stop, file #238, fix it, and recapture; a wrong picture in `docs/` would have
  been believed for months.
- **Worked: a capture script rather than seven hand-made images.** Screenshots are the part of
  documentation that rots silently — the text is reviewed when it changes, the pictures are not.
  `scripts/capture-manual-screenshots.sh` makes refreshing them one command, and it records the two
  things that are not obvious: the dev server's HMR websocket means network-idle never fires, and
  the tabs are deep-linkable so no click sequence is needed.
- **Didn't — I burned five minutes on a headless-Chrome invocation that could never work.** Same
  websocket cause, but I tried three variants before diagnosing it instead of asking why the *first*
  one hung. Playwright's own CLI was in the E2E build output the whole time.
- **Also: the manual uses the mock preview on purpose.** Real-tenant screenshots would carry project
  names, repositories and object ids into a public repository, and would need reviewing for that
  every time they were refreshed. The mock has none, so refreshing is safe by construction.
- **Time invested:** not measured (source: **manual** — hundred-and-first consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none of its own, but this strengthens the case for the ADR-0011 owed by #238's retro. The
  four proxy-assertion failures were all caught by *looking at the artifact* — a screenshot, a DOM
  measurement, a bundle grep. That is the same rule ADR-0004 states for commands, and it now wants
  stating for assertions.

## 2026-08-03 — manual-local-flavour (#241)

- **Didn't — I shipped a manual whose pictures said something its words did not.** Every screenshot
  carried the local-owner banner and the projects list wore a **Local** badge, because the mock's
  first project points at a folder rather than a clone. I captured all seven, read them closely
  enough to catch a layout regression in one, and still did not notice that the flavour on screen was
  never explained. Looking *for a defect* is not the same as reading as a newcomer would.
- **Worked: the owner's one-line correction was the whole finding.** "Don't forget the local version"
  named a gap I had looked straight at. Worth remembering that a reader who has not been inside the
  change sees the omission first.
- **Worked: declining to build tooling for one screenshot.** Two local surfaces cannot be captured —
  the code-source control needs a click the screenshot CLI cannot perform, and a Run's detail page has
  no stable URL because mock run ids are per-load. The options were a console project to drive clicks,
  a screenshot taken by hand under different conditions, or prose plus a recorded reason. The third is
  the only one that stays true: hand-captured images drift from the scripted seven in size, theme and
  timing, and nothing would flag it.
- **Also: the section states the local trade rather than selling it.** One process means the portal
  resolves credentials and clones repositories itself — right on a machine one person owns, wrong
  anywhere shared. A manual that only lists what a habitat gives you is marketing.
- **Next time:** after writing documentation, re-read it against its own screenshots and ask what a
  newcomer would see that the text never mentions. The gap is visible in seconds from that angle and
  invisible from inside the change.
- **Time invested:** not measured (source: **manual** — hundred-and-second consecutive). Unchanged:
  `.telemetry/usage.jsonl` is absent, so `collect-usage` has nothing to join against.
- **ADR:** none. ADR-0011 remains owed from #238 and is untouched by this.

## 2026-08-04 — selfhost-declares-its-limits

**Time (manual — the worktree telemetry gap of ADR-0011, unchanged):** a short change inside a
longer exploration session; the code was small because the exploration had already done the
thinking.

**What worked:** the StoreRemedy pattern paid for itself a second time. The capabilities read
already had the shape "a missing ability carries its remedy", so the Local locus was one more
fact, not a new mechanism — and D4's rule that the reason is *one sentence read in one place*
meant the capability read, the save refusal and the Run refusal literally cannot disagree. The
exploration-first order also showed: reading Orbion and loop-task before grilling turned "maybe
Electron?" into a grounded no with evidence, and the issue bodies carry it.

**What didn't:** an early return placed before the hooks in `CodeSourceSection` — the
rules-of-hooks failure the design validator caught. The withheld branch was conceived as "render
something else entirely", and writing it at the top of the component read naturally but broke
hook order. Also, the first storage assertion asked a read model (`GET /connector` → 404?) about
absence when the read model has a default shape; the store itself was the honest witness.

**One change next time:** when a component gains a "render nothing of the usual" branch, put the
branch *after* every hook call by default and let the comment say why it sits low — the natural
reading position (top) is exactly where it breaks. And assert absence against the store, not
against a read model whose empty answer has a shape.

## 2026-08-04 — docker-run-pods

**Time (manual — the worktree telemetry gap of ADR-0011, unchanged):** one session, alongside the
exploration that produced it; the third execution arrangement on a seam two changes old.

**What worked:** observing before fixing, three times over. The design said "sessions ride a
read-only mount of ~/.config/opencode" and the first real probe corrected it twice — opencode's
credentials live in `~/.local/share/opencode/auth.json`, and macOS Docker Desktop can refuse the
bind outright — both now recorded in the design instead of shipped as assumptions. And the probe
against a missing database caught something no unit test would have: the worker's unhandled
composition exception left the process ALIVE, spinning at 99% CPU — precisely the hang design D3
forbids. The fix (assert the connection string before anything composes, exit 69 named) exists
because a probe ran a real container and watched it.

**What didn't:** ten e2e failures that had nothing to do with the change — `wwwroot` was built on
the previous branch, before another session's UI landed on main, and the e2e serves whatever
wwwroot holds. Second time this session that an environment artifact read as a regression (the
missing wwwroot in selfhost-declares-its-limits was the first). Also: the full pod Run against
the free model (task 3.3) is honestly NOT DONE — the probes covered image → composition →
database → executor → exit semantics, and the agent pass in a pod remains unexercised, the same
shape of debt local-dispatch recorded.

**One change next time:** a stale `wwwroot` after a branch switch is now a known false-regression
shape — rebuild the SPA after every rebase/branch that moves frontend files, before reading e2e
results. And when a probe watches a container, watch the PROCESS after the failure too:
"printed the exception" and "exited" are different facts, and the second one was the bug.

## 2026-08-04 — apphost-habitat-parameter

**Time (manual — the worktree telemetry gap of ADR-0011, unchanged):** the smallest change of the
session; a refactor whose behaviour delta is one parameter and one refusal.

**What worked:** byte-identity as the acceptance criterion. "The regenerated compose must not
change" turned a refactor review into a mechanical check, and it held on the first regeneration.
Reading the parameter as plain configuration instead of `AddParameter` was the one real decision:
a parameter *resource* would have materialised in the publish output, and the artifact must not
carry a habitat choice — the alternative was caught at design time by asking what publish emits.

**What didn't:** the first test set `builder.Configuration[...]` after
`DistributedApplicationTestingBuilder.CreateAsync`, which runs the AppHost's Program before the
test's line executes — the parameter read had already happened. Two of three tests failed until
the value travelled as a command-line arg instead. The testing builder looks like a builder; for
anything the Program reads, it is already an application.

**One change next time:** when a test drives AppHost composition, pass inputs as args to
CreateAsync, never as post-hoc configuration writes — the Program has already run. Worth
remembering alongside the wwwroot lesson from the previous change: the e2e tier's fixtures have
their own lifecycle rules, and both failures this week were lifecycle misreads, not product bugs.

## 2026-08-04 — design-review-turn-5 (self-host posture chip + pods panel)

**Time (telemetry):** 48 min agent (2 867 s cli, $78.97, 178k output tokens); human time
unmeasured — two steering messages, the change ran end-to-end from the design-project import.
Caveat: `verify-telemetry.mjs` reports FAIL when run from a worktree (no `.telemetry/` there and
`OTEL_EXPORTER_OTLP_ENDPOINT` unset in spawned shells) while the main checkout captured this
session fine — the check reads cwd-relative paths and misreads worktrees.

**What worked:** the design project as the spec. Issues 12–15 in the claude.ai/design project
carried exact component sketches, i18n key lists and acceptance criteria, so implementation was
mostly disciplined transcription against DESIGN.md — three explorer agents mapped the Runs
module, the frontend patterns and the secrets seam in parallel before any code. Verifying in the
mock browser caught a real defect the types never would: the looks-like-token length heuristic
flagged the product's own 49-character derived secret names (names read like names — hyphenated;
tokens read like entropy). The seam pattern paid again: `IAgentPodsMonitor` in BuildingBlocks
with a `TryAdd` default in the module meant the panel endpoint needed no knowledge of docker,
and the functional test faked one registration.

**What didn't:** three doc surfaces contradicted the change after the code was done — the
frontend-architecture spec's banner-on-every-screen requirement and two manual paragraphs.
Implementing directly from the design artifact (no grill→propose) meant nothing forced the spec
delta until a grep for "banner" found it; the openspec workflow would have surfaced it at
proposal time. The manual's screenshots still show the old banner — text updated, images not.
And the chip's accessible name ("This machine — owner · no sign-in") broke four e2e tests at CI:
Playwright's default GetByLabel is a substring match, so a shell-level label containing "owner"
collided with the connector form's Owner input on every screen — fixed by exact-matching the
form locators, a third e2e-tier surprise this week after the two lifecycle misreads.

**One change next time:** when implementing from a design artifact instead of an OpenSpec
change, grep `openspec/specs/` for the surface being replaced FIRST — the requirement that
contradicts the design is the real proposal document, and finding it early is the difference
between a spec delta and a spec contradiction. And when adding an always-present labelled
element to the shell, grep the e2e suite for loose GetByLabel/GetByRole substrings its name can
newly match — the collision is deterministic, so it can be found before CI does.

## 2026-08-04 — compose-per-resource (#252)

**Time (telemetry, partial):** 51 min agent this session (3 051 s cli, $17.61, 45k output
tokens) — the proof/close-out session, captured by the main checkout despite running in a
worktree. The implementation session (`para-grill` worktree, commit `1a2ba9e`) is unattributed:
`sessions.jsonl` has no `compose-per-resource` mapping, and `verify-telemetry.mjs` from this
worktree fails 3 checks (`OTEL_EXPORTER_OTLP_ENDPOINT` unset in spawned shells, cwd-relative
`.telemetry/` absent, SessionStart hook never fired) — second consecutive change hitting the
worktree misread documented in the previous entry.

**What worked:** the per-resource API said everything the global patch block used to say — the
regenerated compose came out byte-identical, stronger than the equivalent-not-identical the spec
allowed, so the drift baseline never moved. And D3's "prove by booting, not diffing" paid
immediately: the boot surfaced the missing repo-root `.dockerignore` (every image build was
shipping 1.3GB of node_modules/bin/obj to the daemon).

**What didn't:** roughly an hour lost to a machine-local hang that looked like a slow build —
`docker-credential-desktop` wedged, and every pull and buildx `load metadata` consults it before
touching the network, so two ~28-minute builds sat silent with zero output. The silent pipe was
the accomplice: `up -d --build | tail -40` buffers everything, so there was no signal to
distinguish "compiling" from "hung". Separately, the first E2E run went 36/43 red purely from
the known missing-bundle gotcha plus CPU contention with the wedged build — 18 minutes of
timeouts for an already-documented cause.

**One change next time:** long docker operations never run as a silent pipe — `--progress=plain`
to a log file plus a monitor from the first attempt, and a short-timeout `docker pull` of one
base image as preflight before any compose build, so a daemon-level hang fails in seconds
instead of half an hour. The credential-helper failure mode is worth remembering: host network
fine + daemon pulls hanging = check `docker-credential-desktop` before restarting anything.

## 2026-08-05 — sdk-built-images (#257)

**Time (telemetry, session-level):** the driving session totals $80.86, 157 min agent, 167k
output tokens — but it spans three changes (compose-per-resource's close-out claimed $17.61/51
min of it earlier); the ~$63/106 min delta is mostly this change plus the telemetry-worktrees
fix. Session→change mapping still reads `(none)`: the UserPromptSubmit re-mapping fix rides
PR #256, unmerged while this change was built — consistent, and self-measuring once it lands.

**What worked:** the evidence gate (design D1) dissolved the scariest dependency in fifteen
minutes — probing the installed Aspire DLLs plus one real `/t:PublishContainer` beat reading
docs: no Aspire upgrade, no JS publish API needed, because CI's pnpm-build-then-publish order
puts the SPA in wwwroot for free. And the retired-names grep as a proof gate earned its place:
it caught a fourth Dockerfile nobody had counted (ConversationSession, Azure path) and turned it
into a documented exception instead of a silent survivor.

**What didn't:** the "three Dockerfiles" premise survived grill, proposal and design unchallenged
— `find -name Dockerfile` costs seconds and ran only at the proof gate. Smaller: zsh's `:l`
modifier silently mangled `$n:latest` into `$n` + "atest" when tagging images (braces fix it),
and the first transport proof wedged on the worker's indefinite database retries (wrong-password
scenario) instead of failing fast — switched to a nonexistent run id, which exits promptly.

**One change next time:** when a change claims to retire a category ("the N Dockerfiles",
"every X"), enumerate the category mechanically at GRILL time — the count is a claim like any
other, and the cheapest gate is the earliest one. And a transport proof picks the
fastest-exiting failure mode available, never one that depends on a retry policy elsewhere.

## 2026-08-05 — deploy-sdk-images (#260, spec-less)

**Time (telemetry, session-level):** same driving session as sdk-built-images — the hotfix took
roughly 20 minutes of agent time from red deploy to green PR inside it; per-change split still
unavailable until #256's mapping fix lands.

**What worked:** the #202 rule — watch the deploy the merge triggers — caught the breakage in
minutes, on the first run after the merge, with the failing step naming the exact file. And the
remedy was already half-built: the publish-images workflow written for #257 was the template the
deploy script needed (same pnpm-then-PublishContainer order, same explicit-platform lesson).

**What didn't:** "the Azure path is out of scope" was read as "the Azure path is unaffected" —
but out-of-scope surfaces can still CONSUME what a change retires. The retired-names grep swept
src/, docs and panel copy and never infra/, which is where the consumer lived. #257's own retro
had just said "enumerate the category mechanically" — the enumeration ran, but over a subset of
the tree.

**One change next time:** a retired-names grep runs over the WHOLE repository — `git grep` from
the root, no directory list — because the next consumer will also live in the one directory
nobody thought to include. Out-of-scope means "this change doesn't modify it", never "this
change cannot break it".

## 2026-08-05 — telemetry-worktree-attribution (#255, spec-less)

**Time (telemetry, session-level):** built inside the same long-running session as
compose-per-resource's close-out and sdk-built-images; ~30 min of its agent time. The last
change that cannot split its own time: this is the fix that makes the next one self-measuring.

**What worked:** reading the artifacts before writing code. The claimed defect ("the hook never
fires in worktrees") was false — sessions.jsonl showed worktree sessions mapping fine; the real
defects were the verifier resolving paths from its own file location and the mapping being
start-only while the dominant flow switches branches after start. Ten minutes of grepping the
actual records replaced a fix aimed at the wrong component. ADR-0011 also paid off as designed:
it recorded the debt, and the debt was collected by exactly the kind of change it named.

**What didn't:** the evidence-based check-1 rework (fresh usage.jsonl bytes beat subshell env)
was invented at fix time — the two prior retros had already caveated the false FAIL twice, and
nobody promoted the caveat into the verifier until a third change tripped over it. The
graduation rule exists for ADRs; check wording deserves the same second-occurrence reflex.

**One change next time:** when a retro caveats the same diagnostic twice, the next touch of that
tool fixes the diagnostic itself, not just the work it was misreading — a check that lies twice
is a defect, not a footnote.

## 2026-08-06 — select-setup-steps (#262)

**Time (telemetry, per change):** agent 55.5 min, cost $29.52, 38.27M tokens (123,822 output;
645k cache creation; 37.5M cache read). **The first self-measuring change**: session `6ae2dd93`
maps to `select-setup-steps` in `sessions.jsonl` and 395 datapoints joined on `session.id`, so
these are this change's numbers rather than — as in the three entries above — a session's totals
spanning several changes. #255 said it was "the fix that makes the next one self-measuring"; this
is the next one, and it did.

**Human time is still not measured, and it is no longer ADR-0011's fault.**
`claude_code.active_time.total{type=user}` has zero datapoints for this session and for all 49
recorded sessions, while `node .config/otel/verify-telemetry.mjs` passes all five checks. The
worktree gap is fixed; the user half of "human vs agent" has simply never been emitted, which
could not be seen before because the pipeline was too broken to distinguish "absent" from "lost".
First clean sighting, so no ADR yet — **the trip-wire: if a second change records the same
user-metric absence against a green verifier, that is the graduation point.**

**What worked:** reading the implementation before writing the spec changed the design twice.
`FillGaps` already short-circuits on an empty gap list, so "no pull request when every gap is
deselected" needed no new code — only filtering upstream of it. One layer below,
`StarterInstaller.Install` answers `Workspace.NoChanges` for an empty file list, which would have
reported a *failure* for a choice the Admin made; the design named that trap and a functional test
now pins it.

The absent-versus-empty distinction (design D2) read as pedantry at spec time and caught a real
bug at implementation time. The first pass disabled the confirm control whenever nothing was
selected — which would have killed the empty-repository onboarding path, where there are no plan
rows, nothing is selected by definition, and the button that installs the whole starter set would
have been permanently dead. Because the spec had already insisted `null` and `[]` are different
answers, the fix was immediate: a repository with no plan sends no selection at all.

**What didn't:** the mock's setup report was written from a hardcoded trigger list instead of from
the plan the mock itself serves, so mock mode reported `Excluded ai:grill, ai:propose` — steps
that candidate's plan never offered. Nothing caught it but opening the browser and reading the
report out of the running UI. Task 4.2 asked for "the same report shape the API returns", and the
first attempt satisfied it in shape and not in content. A mock that teaches a contract the API
does not have is worse than no mock, because mock mode is where the surface gets exercised by
hand and its lies are the ones that get believed.

**One change next time:** when a change adds a field to an endpoint the mock also serves, the mock
derives that field from the same source the real handler derives it from — never from a parallel
list kept beside it. A fixture answering from its own copy drifts the moment either side moves.

## 2026-08-06 — spec-first-is-the-catalogue (#269)

**Time (telemetry, partial):** 1 h 28 min agent across two sessions (5 309 s cli, $59.20,
224k output tokens, 89.9M cache reads). **Human active time reads 0 s and that is a capture gap,
not a measurement:** `verify-telemetry.mjs` passes all five checks and the agent figures are real,
so `active_time.total{type=user}` recorded nothing for these sessions while `{type=cli}` recorded
correctly. Named rather than absorbed — the whole reason this field exists is the human/agent
split, and half of it is missing. Related to but distinct from ADR-0011's worktree gap: the
`.telemetry/` directory resolves to the main checkout, and the session→change mapping worked
(two sessions mapped), so this is the metric and not the plumbing.

**What worked:** deleting the portable tier **simplified** instead of complicating. The scope
change arrived late — four rounds of grill had already settled a consent switch beside two tiers —
and removing the second tier collapsed three acceptance criteria into one and let the manifest's
duplicate-trigger refusal keep its no-exception form instead of growing a gated-claim carve-out.
Worth noticing because the instinct on a late widening is that it costs; here the wider ask deleted
a special case. Grounding the design in verified reads before writing it paid too:
`StarterInstaller.Install` already wrote arbitrary repository-relative paths and
`IDocumentReader.Read` already read them, so the new write seam the design nearly invented never
needed to exist — both checked by reading the file, ADR-0009's discipline, not by assuming.
Reconciling rather than bypassing the spec conflict was the third: `automation-configuration`
forbade "a separate consent" in #262's own words, and the delta narrows that requirement to the
files a plan row names instead of quietly deleting the scenario.

**What didn't:** **I wrote two requirements in this change's own spec bundle that contradicted each
other**, one file apart, and did not notice until a test forced the case where both applied — "a
consented tier with no gap still brings its documents" against "the selection and the consent
together leaving no gap opens no pull request". Consenting and then unchecking every row satisfies
the first and violates the second, and the implementation followed the first: seven process
documents written into a repository whose owner had just excluded everything. I reviewed each delta
against the requirement it modified and never against the other. The fix is a third requirement —
prerequisites follow a tier actually being acted on — which is the rule that should have been
written first. Separately, an old E2E assertion was **passing on a substring**:
`ShouldContain("implement.md")` is satisfied by `"aio-implement.md"`, so the line written to prove
the two were distinguishable could never have failed. Found only because the surrounding test broke
for an unrelated reason. That is the second occurrence of this class — #231's `GetByLabel` collision
on 2026-08-05 was the first — so it graduates to
[ADR-0013](../adr/0013-an-assertion-must-be-able-to-fail.md). Also cost a wrong-directory read: a
`cd` into `src/frontend` persisted across tool calls and sent a PR-template lookup to a path that
does not exist there.

**One change next time:** when a change's spec bundle both **adds** a requirement and **modifies**
another, read the new one and the modified one side by side and name the case that satisfies both,
before any code. Reviewing each delta against the requirement it replaces is necessary and not
sufficient — the contradiction here lived between two of my own new sentences, was reachable by
inspection, and cost a wrong implementation plus a spec rewrite to find at test time.

**Decisions recorded:** [ADR-0012](../adr/0012-a-seeded-document-is-the-projects-own.md) with
DEC-064, revising DEC-048's rubric clause on the narrow ground that "the weaker of the two"
presumes two — an existing document still wins, so a seed lands only where there is none.
[ADR-0013](../adr/0013-an-assertion-must-be-able-to-fail.md) on the substring assertion.

## 2026-08-06 — automations-tab-legibility (#271)

**Time (telemetry, partial):** 1 h 57 min agent (7 026 s cli, $86.16, 239k output tokens, 115.9M
cache reads), one session mapped to the change. **Human active time is absent again** — no
`active_time.total{type=user}` datapoint exists for this session while `{type=cli}` recorded
correctly, and `verify-telemetry.mjs` passes all five checks. This is the **second consecutive
occurrence**: #269's entry named the identical gap one entry above this one. Twice is no longer a
finding to name — the human/agent split is the reason the field exists, and it has now silently
recorded nothing twice, so it needs a tracked fix rather than a third naming. Worth noting the
mapping itself worked despite a mid-change branch rename: the session resolved to
`automations-tab-legibility` even though the branch began life as a worktree name, so ADR-0011's
attribution held.

**What worked:** Reading the E2E suite before writing any code. Twenty test files were grepped for
the selectors this change would move, which is why the two assertions scoped to `main` and the
`form:has(#trigger-label) button[type=submit]` locator were corrected in the implementing commit
rather than discovered by a red lane. The same discipline on the canvas: the geometric properties
`VerticalWorkflowCanvas_Should_Constraint` measures were read first and preserved deliberately —
`[node, connector]` as the wrapper's two children, `max-w-[520px]` on the chain, the Gate chip
ahead of the approval toggle so the `[title=…]` first match still reads `Approval`. Also correct
was refusing to copy a stale requirement forward: `automation-configuration` still described the
chain as a horizontally-scrolling row at wide viewports, which #232 replaced, and because a
MODIFIED block replaces its requirement wholesale at archive time, copying it would have written a
knowingly false spec into `openspec/specs/`.

**What didn't:** **I reported the change as verified when the verification could not see two of its
three defects.** The browser checks were real — scroll offset held across open, Esc and save,
computed colours, node geometry at three widths, both themes — but every one of them measured
pixels or layout, and the defects lived in the accessibility tree. `sr-only` hides pixels and
nothing else, so the panel's `hideTitle` left radix's `<h2>` in the tree beside the content's own
`<h2>` of the same name: one heading announced twice, and two elements Playwright's role query
could not tell apart. CI's E2E lane found it as a strict-mode violation; nothing I ran could have.
**This is ADR-0004 again** — the assertion took a proxy (it looks right) for the artifact (what the
tree contains) — and the third instance of that family in this log. Two smaller ones, both mine:
`gh run rerun` during a GitHub Actions outage created a run stuck in `queued` that could then
neither be cancelled (409) nor deleted (403), and I twice misread outage symptoms as something
else — first calling an unstarted queue "latency", then reporting `main`'s red as an Azure or code
problem when it was the same cancelled-`changes` pattern the outage was producing everywhere.

**One change next time:** when a change introduces or moves a dialog, a sheet, or any `sr-only`
content, the browser verification includes at least one **accessibility-tree** assertion — count
elements by role and accessible name and check the panel's `aria-labelledby` resolves — before the
change is called verified. A visual check cannot see a duplicated heading, which is exactly the
defect a modal panel introduces, and "it looks right in both themes at three widths" is a
statement about rendering that says nothing about what a screen reader or a role query receives.

**Decisions recorded:** none new. The lesson is
[ADR-0004](../adr/0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md), already
Accepted; writing a fourteenth ADR for a further instance would fragment a decision that is
already correct and already cited. The recurring telemetry gap above is the item that needs an
owner, not a new principle.

## 2026-08-06 — spec-first-chain (#273)

**Time (telemetry, delta):** 1 h 03 min agent (3 762 s cli, $61.53, 65k output tokens), computed as
the difference from the previous entry's totals — this session carries two changes, and the
session→change join cannot split one session between them, so the delta against the totals recorded
at the last retro is the honest figure. **Human active time is absent for the third consecutive
change** — `active_time.total{type=user}` recorded nothing again; the capture defect named in the
last two entries now has three data points and still no owner.

**What worked:** #269's discipline of scoping the wiring out *with its reasons recorded* made this
change almost free: the decision arrived pre-framed (which edges, where the human waits live), the
marker and its tests already existed, and the whole implementation was three manifest values plus
the surfaces that had documented their own absence. The grill decisions (full chain, gates as
waits, refine/status standalone) were made in-session by the product authority and the proposal
cited them instead of re-litigating. CI's E2E lane ran the mock-mode marker scenario that local
verification had already exercised by hand — unchecking `ai:propose` marks `ai:implement`.

**What didn't:** session-scoped telemetry attribution broke down the moment one session carried two
changes: `sessions.jsonl` maps this session to both, so neither change's figures are separable
except by manual delta against the previous entry. Related to but distinct from ADR-0011's worktree
gap — the mapping *worked*, twice, and that is exactly the problem.

**One change next time:** one session per change once the loop runs multiple changes back-to-back —
or the mapping hook records a timestamped span rather than a bare session→change pair, so a later
join can attribute by interval. Decide before the next multi-change session, not after it.

**Decisions recorded:** none new — the methodology decision itself is #273's grill record and the
added `default-automations` requirement.

## 2026-08-07 — inbox-open-prs (#274)

**Time (telemetry, delta):** 34 min agent (2 014 s cli, $69.65, 79k output tokens), by delta
against the previous entry's session totals — same shared-session caveat as spec-first-chain's,
same absent `{type=user}` datapoint, now the **fourth** consecutive occurrence.

**What worked:** exploring before proposing. A read-only agent mapped the Inbox, the seam and the
Run→PR join before the proposal was written, and it surfaced the two facts the design turned on:
the seam's vocabulary rule ("change", never "PullRequest") and the shell badge polling the inbox
array from every page every 30 s — which is what made "its own endpoint, page-scoped, slower" a
requirement rather than a preference. The browser console then caught a real defect the eyes had
approved: an `<a>` nested in an `<a>` for the "by a Run" link, which React refuses to hydrate.
Reading the console after every screen change is now paying for itself — this is the second
change in a row where the accessibility/DOM layer held the defect the pixels hid.

**What didn't:** I shipped the handler with `VisibleProjects() == null` treated as "nothing
visible" when the contract says null means **all** projects — the owner and the self-host habitat,
i.e. the product's primary user (DEC-016). The functional test caught it in minutes, but the shape
of the mistake is worth naming: the inbox filters a *query* with null-means-all semantics that cost
nothing to honour, while this surface iterates *projects*, and I copied the guard without copying
the semantics. The fix added `IProjectCatalog.ActiveProjectIds` — a Contracts widening ADR-0006
would have demanded eventually anyway, since a capability the primary user cannot reach is not a
capability.

**One change next time:** when a use case consumes `VisibleProjects`, write the null-path test
first — "the owner sees everything" is the semantics most likely to be silently wrong, and it is
one test.

**Decisions recorded:** none new. The rate-limit-driven narrowing (the ambient count neither
includes changes nor triggers vendor reads) is contract in the delta spec, decided in #274.

## 2026-08-07 — run-on-a-pr (#275)

**Time (telemetry, delta):** 41 min agent (2 441 s cli, $117.56, 96k output tokens) by delta
against the previous entry — the shared-session caveat stands, and `{type=user}` is absent for the
**fifth** consecutive change. The batch total for tonight's three-change loop: ~2 h 20 min agent.

**What worked:** mapping the execution path before designing it. The exploration established that
the publish step is retired (DEC-062), that `AgentInstruction.Prompt` is free-form, that the
named-branch checkout already existed, and that BR-001 is a trio whose filter is generated from
one array — so the design reused all four instead of inventing a push member the ceremony had
deliberately shed. The same map found **#274's marker had shipped dead**: it joined on
`Run.OutputLink`, which nothing writes post-DEC-062, and its test passed by seeding the column at
the persistence layer — my own test, one change ago, provisioning its own precondition (ADR-0002's
shape, ADR-0004's family). Fixed here on head branches, with an impostor-branch case so the fix
can fail. The whitelist test (`List_Should_ExposeExactlyTheRecordedSubset`) did exactly its job:
it failed on the four new fields and forced the widening to be deliberate. Mock mode caught
`undefined !== null` rendering every story Run as "PR #undefined" before any human saw it.

**What didn't:** I merged #274 with a marker that could never fire, and nothing in its
verification could have told me — the browser showed the marker rendering (against a mock that
seeded it), the functional test passed (against a hand-seeded column), and CI agreed with both.
The lie was in the seed, not the assertions. It took the *next* change's design exploration to
read DEC-062 and notice the column was a fossil. That is two ADR-violating test-seeds by me in
one night (this and the OutputLink one), both caught later than they should have been.

**One change next time:** when a test seeds a column directly at the persistence layer, that is
the moment to grep for the production writer — `rg "OutputLink ="` would have found only the
retired path and the vestigial nulls in thirty seconds. A seed with no production writer is a test
of dead code, and the grep is cheaper than the retro.

**Decisions recorded:** none new — the story-less Run's rules live in #275's grill record and the
delta specs; DEC-062 honoured rather than revised.

## 2026-08-07 — run-detail-legibility (#280)

**Time (telemetry, delta):** 35 min agent (2 126 s cli, $89.86, 55k output tokens) by delta
against the previous entry; `{type=user}` absent for the sixth consecutive change — the capture
defect now spans two days of entries and still has no owner.

**What worked:** grounding the design's every "today" claim in code line numbers before grilling
(the rail's 280px at RunScreen.tsx:257, the diff mounted inside it at :440, the decisions in the
header at :120–145) made the issue, the delta and the implementation agree without a single
re-read. The mock-first verification kept paying: it caught that the run-changes mock had been
answering a flat shape instead of the API's `{ change }` envelope — every mock Run has read "no
pull request" since the surface shipped, a defect nobody noticed because the empty state looked
plausible. And re-verifying CI after the mid-flight rebase was not ceremony: main's new commit
reconfigured the AppHost the e2e lane boots, so the stale green genuinely proved nothing about
the merge combination.

**What didn't:** two browser-harness traps cost time. The preview's `innerWidth` reads 0 until an
explicit `resize_window`, so `matchMedia`-driven components rendered their narrow branch while
the screenshot looked wide — a lie between two observation channels. And the mock regenerates run
ids per reload, so a reloaded detail URL 404s silently. Neither is product code, both are now
known.

**One change next time:** when a component branches on `matchMedia`, the browser check starts
with an explicit `resize_window` and asserts `innerWidth` — trusting the default viewport is
trusting an unset value.

**Decisions recorded:** none new. The cause→remedy map is deliberately a closed list against the
executor's own sentences (design D2); widening it is content work, not a decision.

## 2026-08-07 — runtime-readiness (#279)

**Time (telemetry):** ~79 min agent, $140.94, 129k output tokens — the session-total delta for
this change's day, and the FIRST change measured by its own mapping: sessions.jsonl carries
`change=runtime-readiness` because #255's UserPromptSubmit re-mapping fired on the branch
switch, exactly as designed. (Per-change splits within one session remain approximate; the
mapping records when a session touched a change, not per-prompt attribution.)

**What worked:** evidence before code, again — ten minutes of reading the failing Runs' actual
API responses and grepping the machine (`which` in the right shells, `mdfind` for the apps)
turned "it's failing" into two precise defects: desktop logins without CLIs, and a hard-coded
credential default with no off switch. The pods panel as a template made the panel half almost
mechanical: probe/host/read/chip each had a sibling to mirror, and the mirror held (D1's
no-premature-abstraction call kept it cheap). CI's own e2e run doubled as the absent-CLI matrix
cell for free — the ubuntu runner has neither CLI, and every journey survived the not-ready
panel.

**What didn't:** the live dev-loop matrix cell never ran — first the preview harness killed the
interactive aspire CLI silently, then the machine's own aio-postgres-data volume refused the
fresh password (the exact hazard the E2E fixture documents, still unfixed at the machine level).
The proof is honestly bounded in tasks.md, but two environments in a row failing for
environmental reasons cost ~30 min of the change's time. And the owner's "no" to the real Run
was asked too late — the plan assumed a GitHub side effect the owner never signed up for.

**One change next time:** proofs that touch the owner's external accounts get their yes/no at
PLAN time, not at execution time — the decision changes the task list, not just the moment. And
a dev-loop proof step starts by checking the postgres volume matches the session's password
(`docker volume rm` is the documented fresh start) before burning time on the app layers above.

## 2026-08-07 — project-runtimes (#244)

**Time (telemetry, delta):** ~74 min agent (4 456 s cli, $187.74, 118k output tokens) by delta
against the previous entry; `{type=user}` absent for the SEVENTH consecutive change. The
pipeline verifier reports all green — the gap is in the CLI's user-time emission, not the
collector — and it still has no owner.

**What worked:** landing on the seam #279 had just built meant the whole feature was one chain
function plus one Contracts read: `run.RuntimeName ?? automation.Runtime ?? project default ??
deployment default`, with the executor's old inline `??` deleted rather than paralleled (design
D2's exact fear). The fixture's two recording fakes made all four levels of the order pinnable
in five functional tests without touching a real CLI. And writing the AC6 pin as "never
set-to-empty" instead of "unset" made the unit test immune to whatever the host shell exports —
the defect was shadowing, and the test now states exactly that.

**What didn't:** the transcript header (D2's "name the credential's source") broke two
exact-content log tests — foreseeable, since the header is prepended to every Run's transcript,
but not foreseen; the tests were repaired after the fact rather than listed in tasks.md as a
known ripple. And a parallel session claimed the `runtimes.*` i18n prefix while this change was
mid-flight, forcing a rename to `projectRuntimes.*` — the second cross-session collision of the
week (branch overlap checks look at files, and the i18n catalogue is one file everybody touches).

**One change next time:** when a change adds a line to every transcript (or any always-on
output), grep the test suites for exact-content assertions on that stream at PLAN time and list
the repairs in tasks.md — a deliberate ripple should never surface as a red suite.

**Decisions recorded:** D3's narrowing is in the design doc — approval shows the resolved
runtime and deliberately does not re-choose it (an approved plan executed by another agent's
hands would not be the approved plan).

## 2026-08-07 — split-run-pod-into-executor-and-sandbox

**Time (telemetry, by session id):** ~134 min agent (8 064 s cli, $95.54, 291k output tokens),
recovered by hand. The automated join found nothing: `map-session-change.mjs` matches the
**branch name** against active change directories, and this session ran on
`claude/sandboxes-source-of-truth-ea2327`, which contains no change name — so `change=""`, the
exact failure its own header documents. `{type=user}` absent for the EIGHTH consecutive change.

**What worked:** the boundary landed as a substitution, not a rewrite. Both runtimes already
funnelled through one chokepoint (`HeadlessProcess`, extracted so BR-005's timeout could not
drift), so putting `IAgentProcessHost` there left every flag, parser and usage rule untouched —
and the pre-existing suites passed with no assertion edited, which is what proved the extraction
was a no-op rather than a claim that it was. Then D6 paid for itself inside the same session:
readiness reports from *where the CLI will run*, so bringing the dev loop up in sandbox mode
showed both runtimes not ready while both were installed on the Mac — the driver was creating
every sandbox from sbx's generic `shell` template, which carries no agent CLI. Every Run would
have failed with a missing binary. A check built to be honest about which machine it describes
caught the bug that check exists for, on its first real outing.

**What didn't:** the end-to-end Run went unexercised for the SECOND consecutive change, at the
identical wall — the only runnable project targets the real repository and DEC-062 has the agent
publish its own work. Both times the verification was downgraded rather than the obstacle
removed, which is how a whole substrate accumulated with its central claim unproven. That
graduated to **ADR-0014**.

Worse, and the honest headline: **the previous entry's "one change next time" was exactly the
mistake this change then made.** #244 ended with "when a change adds a line to every transcript,
grep the test suites for exact-content assertions on that stream at PLAN time and list the
repairs in tasks.md". This change added a line to every transcript, did not grep, and CI caught
three broken exact-content assertions after the PR was open. The advice was right, sat one entry
above, and did nothing — so it graduated too: **ADR-0015** (one owner for always-on output; assert its shape). The fix turned out to be better than
the repair anyway: #244 already had an always-on header naming the credential, so the second
line was redundant; the two facts are now one sentence in one place. A local `sh --version`
probe also passed on macOS and failed on Linux CI — the same dash trap this change had already
found on the sandbox side and did not generalise. And three separate times a green-looking
result was false: `rtk` reported Prettier clean over a non-zero exit, and reported "ok" on a
commit whose success had to be confirmed with `git log`.

**One change next time:** run the functional suites — not just the unit ones — before opening a
PR when a change touches shared output. Every failure CI found here was reachable locally in
25 seconds (`Runs.FunctionalTests`, 148 tests); the loop that missed them ran only the fast
suites and trusted a green build. Speed chose which tests ran, and the slow ones were the ones
that mattered.

**Decisions recorded:** D6 gained a second cadence during implementation — the host's own
preconditions probe every 30 s because they move, while the CLI-in-the-template verdict refreshes
every 15 min, because creating a sandbox costs ~4.5 s and that answer belongs to an image that
changes on deploy. And the credential-source sentence moved from the runtimes to the executor:
announcing it from the runtime polluted the agent's own output stream and broke the streaming
contract test — the right signal, caught by an existing test.

## 2026-08-07 — run-previews-over-published-ports

**Time (telemetry, delta):** ~82 min agent (4 932 s cli, ~$117, ~156k output tokens) by delta
against the previous entry. The automated join **worked this time** — the branch was
`change/run-previews-over-published-ports`, and `map-session-change.mjs` matches the branch name
against active change directories, which is exactly what the previous entry's generated
`claude/...` branch could not do. Cheap fix for that gap, if anyone wants it: name the branch
after the change. `{type=user}` absent for the NINTH consecutive change.

**What worked:** the previous entry's "one change next time" was applied, and it paid immediately.
Running the functional suites locally before opening the PR — 152 Runs, 110 Projects, 25 seconds
— produced a PR that went green on all nine checks first try, against three CI failures on the
change before it. The lesson worked because it was followed; that is worth recording as loudly as
the failures.

Exercising rather than reading caught two things nothing else would have. In the browser: a
preview kept rendering on a finished Run, because react-query's `enabled` stops a query from
FETCHING but does not retract what it already fetched, and on the first render the log had not
yet said the Run was done. Against the real sbx: the first version of the round-trip test failed,
and the failure *was the design working* — it read the published port after awaiting the run, by
which time the finally had already removed it. Both are the kind of thing that reads as correct
on the page and is not.

**What didn't:** the mock lied twice more, and one lie actively manufactured evidence that a bug
was absent — the log fixture hardcoded `complete: false` for every Run, so a Succeeded Run looked
live and the stale-preview bug looked fixed. Third occurrence of the same class (the
`nextSequence` note beside the first one already said the rule), so it graduated: **ADR-0016** —
a fixture derives what the server derives, and fixture ids are stable so every mock state is
linkable. Separately, the previous change's close-out used `git add -A` and swept this change's
proposal bundle into `main` inside an unrelated commit: harmless in effect, wrong in principle,
and entirely avoidable.

**One change next time:** stage close-out commits by path, never `-A`. A commit that carries
something it does not claim is a small lie in the one record we treat as authoritative.

**Decisions recorded:** the port belongs to the Automation, not the Project (the design's open
question), because only the prompt knows whether its change is runnable. The local lane never
previews and says so rather than ignoring a configured port. And a Run that ends while somebody
watches replaces the frame with a sentence instead of vanishing — the only exception to "a
finished Run offers nothing", and it exists because a window disappearing unexplained reads as a
glitch rather than as a rule.

## 2026-08-08 — sandbox-carries-the-owners-session (#288)

A sandboxed dev-loop Run now authenticates as the developer's own seat. The dev loop's habitat
copies the machine owner's agent-CLI credential files into each sandbox, the transcript names that
as the credential source, and a runtime whose session cannot be copied says so on the readiness
panel instead of failing mute inside a microVM.

**Worked: observing before claiming set the scope, and the scope was the finding.** Copying
candidate files into a sandbox by hand — before writing anything — established that opencode's
entire session is one 950-byte `auth.json`, that its `~/.config` tree is a gigabyte of caches worth
nothing, and that Claude Code on macOS keeps its credential in the system Keychain where no copy
reaches it. That last one looked like a blocker and was actually the shape of the change: Claude is
out of scope, and the panel explains why, which is the half that survives even if carriage were
dropped.

**Worked: the gated real-sbx file was the right home for the proof.** Inherited from the previous
change, it exercises the shipped host against the real CLI, and it caught what no double could.

**What didn't: the by-hand observation gave false confidence, and that is a new class.** Copying by
hand *as the machine owner* succeeded. The server copying *on the owner's behalf* did not: `sbx cp`
preserves the host's uid and mode, so the 0600 credential owned by uid 501 landed inside the
sandbox still 0600 and still owned by 501 — unreadable to the sandbox user, which cannot chown it
either. `opencode auth list` then reported "0 credentials" from a file demonstrably present:
carriage appearing to work and to fail at once. The method that exists to prevent unverified claims
produced one. Stating the rule here so the second occurrence graduates it: **an observation holds
for the principal that made it; if something else will act in production, nothing has been
observed.** Fixed by staging through a 0644 copy in a 0700 directory and re-creating the file *as*
the sandbox user.

**What didn't: an assertion that could not fail, again, by the flank.** The carriage test asserted
the provider slug (`github-copilot`) while the CLI prints the display name (`GitHub Copilot`), so
the *negative* test — the one whose whole job is proving the positive can fail — passed vacuously.
Caught only because the positive test failed first. ADR-0013 holds; what is new is that the dead
assertion was in the control, not in the claim.

**What didn't, structurally: the end-to-end Run is unexercised for the third consecutive change.**
ADR-0014 was written at the second and asked for a rehearsal *target*. The target exists and was
not enough: what stopped this one is the rehearsal *credential* — the Connector's PAT, which only a
human may paste. ADR-0014 removed the obstacle it could see and left the one behind it. Graduated:
**[ADR-0017](../adr/0017-a-rehearsal-needs-its-credential-not-just-its-target.md)** — a change
whose proof needs a real Run names the credentials that rehearsal consumes, says whether each
already resolves, and flags the ones only a human can create.

**One change next time:** enumerate the human-only steps of a verification during planning. Not
just for rehearsals — "who can actually perform this" is a question an agent-run workflow should be
asking of every precondition, and it is cheap to ask early and impossible to answer late.

**AC7 remains open**, honestly: point a project's Connector at
`asantamariaplainconcepts/ai-orchestrator-rehearsal` with a PAT, give it an opencode Automation,
and dispatch one Run. What was exercised instead is recorded in the change's `tasks.md` — the
carried session authenticating as the owner against the real CLI, the same command finding nothing
without carriage, and `sbx ls` identical before and after.

**Time invested:** 6.41 h agent, $388.02, 711 500 output tokens (source: **telemetry**, five mapped
sessions). Human time **not captured** — the export carries only `type=cli` datapoints for these
sessions and no `type=user` ones, while `verify-telemetry.mjs` passes all five of its checks. The
verifier confirms that telemetry *arrives*; it does not confirm that both halves of it do.

## 2026-08-08 — automation-and-run-choose-the-model (#291)

An Automation names the model its Runs think with, a launch can override it for that Run only, and
where the offered models come from depends on what the runtime can actually be asked: opencode is
asked, Claude Code reads an operator's configuration because it has no listing command.

**Worked: the measurement decided the architecture rather than decorating it.** `opencode models`
answers 41 on the host and **495 inside a sandbox** holding the carried session (#288), with the
`github-copilot/*` entries present only there. That one number turned "ask the machine that runs
agents" from a principle into an obligation — a list gathered in the wrong place is not slightly
stale, it is wrong by an order of magnitude. And the Claude side was decided the same way: `sonnet`
answers, `opus` resolves to a model this seat lacks, `fable` is not an alias at all. A hardcoded
list of three plausible aliases would have shipped two broken options, which is why the list
belongs to the operator.

**Worked: the tests found two real defects, both by being about the right thing.** The model chain
is asserted on the instruction the runtime was *handed*, not on what was stored — and that caught
the create-automation endpoint silently dropping `request.Model` on its way to the command. The
same read showed it drops `request.PreviewPort` too, which is an older defect of identical shape;
it was left alone and spawned as its own task rather than folded in. Separately, the list
endpoint's field whitelist failed on the new field, which is exactly what a whitelist is for, and
it was widened deliberately rather than relaxed.

**What didn't: a design decision was written from one measurement and applied to two runtimes.**
D5 said both CLIs name the rejected model. Claude does. opencode answers `UnknownError`,
"Unexpected server error" and an opaque ref, naming nothing — so passing its text through would
report a typo'd model as somebody else's outage. Second occurrence of the shape #288 recorded
(observed as one principal, claimed for another), so it graduated:
**[ADR-0018](../adr/0018-a-measurement-licenses-only-what-it-measured.md)** — a claim is no wider
than the measurement behind it, and where it is wider the gap is named. The correction made the
design stronger: the product now composes the failure sentence itself instead of hoping a CLI will.

**What didn't: the first change planned under ADR-0017 mis-stated its own credential
precondition.** Task 0.1 declared that no human-only credential step was needed. It was wrong —
launching a real Run needs the Connector's PAT like every other Run. The ADR written one change
earlier to stop exactly this was followed in form and missed in substance. Recorded in `tasks.md`
rather than quietly corrected, because a process step that can be filled in wrongly while looking
complete is worth seeing.

**What didn't: the usage figures below are an upper bound, not a measurement.** One session spanned
two branches, and the session→change mapping attributes it to both, so this entry and #288's share
their totals. Worth a look at whether attribution should be per-commit rather than per-session.

**One change next time:** when a design sentence contains *both*, *every* or *any*, run the second
one before writing it. It is nearly always one command, and it is the difference between an
observation and an assumption wearing an observation's clothes.

**Time invested:** ≤6.64 h agent, ≤$441.94, 840 530 output tokens (source: **telemetry**, two
mapped sessions — see the attribution caveat above; these totals overlap #288's). Human time **not
captured** — the export carries only `type=cli` datapoints, the same gap #288 recorded.

## 2026-08-08 — drag-to-chain-the-workflow (#293)

Design review turn 8, built: a standalone Automation drags into the chain, every gap states the
wiring its drop would perform before it happens, a refused drop names its rule at the gap, and a
read-only preview underneath shows the Backlog columns the workflow produces.

**Worked: the design review was implementable as written.** Its sentences became the product's
sentences almost unchanged — "ai:grill will hand to ai:estimate · ai:estimate will hand to
ready-for-proposal" is the review's own phrasing. Where it did not survive contact, the review was
right and the first implementation was wrong: the end-of-chain gap read "ready-for-proposal will
hand to ai:implement will hand to it", which is two hand-offs where there is one, and the review
had already written the correct single clause.

**Worked: putting the rules where the gesture is not.** Playwright cannot perform an HTML5 drag
(#110) and this repository has no frontend unit runner, so the gesture cannot be tested at all.
What could be salvaged was its *decisions* — what a drop rewrites, what refuses it — which now live
as pure functions of the Automations, with no React and no DOM. They are testable the moment a
runner exists, and the change says plainly that they are **not tested**, in an open task rather than
in a hedge.

**What didn't: the process ran backwards.** The implementation was written first, from the design
review, and the issue, the spec and this bundle came afterwards to land it through the normal path.
That is recorded in the change's own `tasks.md` section 0 rather than smoothed over. It was fine
here — the design review is itself a spec, written and reviewed before any code — but it means the
spec gate reviewed a decision already made, which is not what the gate is for.

**What didn't, structurally: a field went missing for the third time.** The Automation endpoint
replaces the resource wholesale, so a caller that omits a field clears it with a 200. `previewPort`
is dropped by the create endpoint and never sent by the form; `model` was dropped by the create
endpoint (caught in #291 by a test that asserted on what the runtime was *handed*) and then again
by the workflow canvas, a third caller nobody had thought about — so any drag or approval toggle
silently reverted a chosen model to the deployment's. The API's own comments have warned about this
since the field before last and the warning was not enough, because the failure is invisible at the
call site by construction. Graduated:
**[ADR-0019](../adr/0019-a-whole-object-replace-has-one-builder.md)** — one request builder per
client for a wholesale replace, and a client that cannot carry a field must not be able to write
the resource.

**What didn't: the mock had never been able to perform the gesture it draws.** There was no update
route for Automations at all, so every canvas gesture — including the human block shipped in #137 —
404'd in mock mode and the picture never moved. Adding one, the first version mutated the array in
place, and the result was the exact disagreement `workflowMembers` was written to prevent: the
chain showed three steps while the catalogue still said "standalone" and the header still said
"2 steps". React Query keeps previous data when a refetch returns the same reference, so an
in-place fixture shows the change to whichever component happens to re-render. ADR-0016's rule
arriving by a route it had not come by before.

**One change next time:** when a change adds a field to a wholesale-replace resource, grep for the
resource's other writers before finishing. Both `model` losses would have been one grep.

**Time invested:** not measured (source: **manual**). Telemetry is being captured — the verifier
passes all five checks — but **zero sessions map to this change**: the branch was created mid-session
and the mapping hook runs at session start. #291's entry recorded the opposite face of the same
defect, where one session spanning two branches was counted against both. Attribution is
per-session and the work is per-branch, and those are not the same thing.

---

## 2026-08-09 — `runs-execute-in-azure-sandboxes` (#296)

A deployed Run executes in a hardware-isolated sandbox created over an API. What began as
"replace the cloud substrate" also retired the pod, the queue and KEDA, the dispatch worker, the
vocabulary that outlived them and the documentation still promising all of it — 49 tasks, eight
commits, 593 tests green.

**What worked: the shipped host was driven against real Azure, and it found seven defects a fully
green suite could not see.** `fs cp` takes no `--id`. **No verb copies a directory tree at all**,
which invalidated the workspace design and forced tar → copy → untar. The poll loop dropped the
last lines of every Run. The egress decision log is JSON and the reader filtered lines for a word
the real output never contains, so a Run that reached a blocked host reported *nothing* — the
exact failure the feature exists to prevent. `create` was never passed `--credential`, so no agent
could ever have authenticated. `--disk` names only public disks, leaving this product's other
runtime with nowhere to run. And `sandbox delete` prompts, which the product had right by luck
rather than by test. Two of those rewrote a design decision. None was a coding slip; all were
beliefs. It was the most valuable hour of the change.

**What didn't, structurally: the stand-in invented its subject's answers.** The egress decision
log was modelled as a table of lines containing "Deny"; the real answer is JSON with a `denied`
array, and nothing in it says "Deny" at all. The test was green, confirming the invention. That is
ADR-0016's rule — a fixture derives what its subject derives — arriving at a *process boundary*
rather than at a server, and it is the second time this programme has paid for it on a launcher:
`sbx cp`'s uid-and-mode behaviour was the first. Graduated:
**[ADR-0020](../adr/0020-a-launcher-is-unverified-until-it-meets-its-real-cli.md)** — a launcher
is unverified until a gated test has driven it against the real CLI, and when the CLI contradicts
the stand-in, the stand-in is corrected to the real answer, kept verbatim.

**What didn't: a `finally` cannot survive the process that would run it.** The readiness probe
creates a microVM every thirty-second sweep and disposes it correctly — but stopping the dev loop
mid-sweep orphans one, and a week of restarts had left **31 sandboxes and 125 GB of disk gone**.
Found by the owner on his own machine, not by any test. The host now claims its namespace and
reaps what a previous process abandoned. Worth noting beside it: an early diagnosis blamed
`CarrySession`, which logs and continues rather than throwing. The fix that mattered was the
reaper, and the comment says so rather than taking credit.

**One change next time:** write the gated real-substrate test **before** the stand-in, not after.
Every one of the seven defects would have surfaced on day one instead of on the last.

**Left open deliberately, not silently:** the credential model this change designed cannot be
satisfied today. The platform's two typed providers are an Anthropic key the organisation does not
hand out and a Copilot token that must be personal — and a personal token bills the model to that
person's seat, which is exactly what #244 forbids and what the per-Project SandboxGroup exists to
guarantee. A deployment's own disk with opencode and its free model sidesteps it entirely, and
that belongs to a follow-up rather than to a green tick here.

**Time invested:** 13.0 h agent, $782.29, 1.10 B tokens across 1 mapped session (source:
**telemetry**). Human time reads **0.00 h**, which is the metric not being emitted rather than the
work being unattended — recorded as measured rather than corrected by guesswork.

## 2026-08-10 — `close-opn-007-live-agent-session` (#301)

A decision, not a capability: whether a human may take the keyboard in a Run's own agent session.
The answer is habitat-split — permitted in self-host, refused in a deployment
([ADR-0021](../adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md),
DEC-065) — and OPN-007 was recorded and closed in the same change, as every OPN before it.

**What worked: the grill read the supersession chain, not just the locked verdict.** The idea
arrived contradicting ADR-0008, which had already refused a live session. The cheap answer was
"blocked by a locked decision". Reading *why* it was locked turned that into something else
entirely: two of its three premises had moved without anyone revisiting the conclusion — DEC-013
went with its substrate (#296), and "nothing idles" had already been revised twice, by DEC-061 and
then DEC-063, which now pays for one continuously idle session at 1 vCPU and 2 GiB. What remained
was BR-006, and BR-006 is satisfiable by an inactivity bound. A gate that only checked whether a
decision was locked would have stopped the work; one that read the decision's own reasoning found
it had expired. Worth doing beside it: the spike ran **before** the decision, so the ADR could cite
feasibility as measured — a pty, signals, full-screen programs — and argue only about cost.

**What didn't: this change's time is unmeasured, for roughly the seventy-first time.**
`node .config/otel/verify-telemetry.mjs` fails three checks — *exporter enabled AND pointed here*
(`OTEL_EXPORTER_OTLP_ENDPOINT` unset), *collector accepting connections* (nothing listening), and
*our collector is the one running* (container not found). `usage.jsonl`'s last write is
**2026-08-10T20:54:02Z**, while this change's branch was created at **22:12Z**, so the export
predates nearly all of the work. [ADR-0011](../adr/0011-a-worktree-session-carries-its-own-telemetry.md)
anticipated exactly this and required the retro to name the failing check instead of shrugging —
and that fallback has now been exercised in some seventy entries. **The escape hatch has become the
normal path.** Reporting the gap faithfully has never once repaired capture, and nothing recovers
telemetry that was never written. Spun into [#307](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/307)
rather than graduated here: the fix is a collector lifecycle, not another decision. Smaller, in the same family: DEC-060 was cited where
DEC-063 was meant — a decision id written from memory instead of read from the file — and it
propagated through the proposal, the design and the tasks before review caught it.

**One change next time:** make a missing collector fail at session start, loudly, instead of
producing a retro that reports it afterwards. The reporting requirement was the right call in
ADR-0011 and it has been honoured every time; what it cannot do is make anyone fix the exporter.

**Left open deliberately, not silently:** BR-005 has no rule for an attached agent — a timeout that
bounds unattended work cannot also bound work a human is typing into — and ADR-0021 names that as
gating the into-the-agent form it permits. Authorization and audit for a shell inside a Run's
sandbox are likewise unbuilt, and that sandbox carries the machine owner's own session (#288). Both
belong to #304, with the blockers written into it rather than discovered by whoever picks it up.

**Time invested:** unmeasured — source **manual, because capture is broken** (three failing checks
named above). The only figures the export holds for this session are **$0.69 and 380,511 tokens**,
and they cover the stretch before the collector stopped receiving, which is before this change
began. They are recorded as partial rather than presented as the change's cost.

## 2026-08-11 — `human-opens-a-shell-in-a-run-sandbox` (#304)

A human opens a real terminal in an executing self-host Run's sandbox, beside the headless agent —
the capability [ADR-0021](../adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
licensed a day earlier. Nine commits, and almost no new machinery: `RunPreviewHost` already had the
registry shape, `RunLogHub` already had the self-authorizing surface, and the archived spike already
had the transport.

**What worked: the gated test met the real CLI, and the real CLI refused what a stand-in would have
accepted.** The first pseudo-terminal passed only the caller's environment to `posix_spawn`, which
*replaces* an environment where `Process.Start` inherits one — so the sbx CLI died with
`panic: $HOME is not defined` before any sandbox was touched. No mock would have objected;
`Process.Start`'s own semantics hide the difference. That is [ADR-0020](../adr/0020-a-launcher-is-unverified-until-it-meets-its-real-cli.md)
arriving on schedule, and it is the second time on this launcher that the stand-in was the thing that
was wrong. Worth recording beside it: an **architecture rule** caught `run.attach` being declared by
nothing, because it reads `[Requires]` off dispatched requests and a hub dispatches nothing — the same
blind spot `RunLogHub` documented. The rule now carries a short list of permissions enforced outside
the pipeline, each with its named enforcer, because const strings are inlined and no reflection could
have found the usage.

**What didn't: `git add src` swept two unrelated files into six commits.** An Aspire bump to 13.4.6
and the removal of `Aspire.Hosting.AppHost` were sitting uncommitted in the working tree when this
change started, and a wildcard stage carried them along. **CI found it, and found it by accident:**
the sweep included a reformatted `PackageVersion` line, `backend-format` went red, and the reason
turned up the rest. Nothing was checking whether a change contains only its own work — the formatter
noticed a side effect of the mistake rather than the mistake. The files were reverted on the branch
before the merge and the changes handed back to the working tree they came from, but the version that
merges is only clean because a formatting rule happened to trip.

**One change next time:** stage by path, never by directory. `git add src` has the blast radius of the
whole tree, and the cost is not the noise in the diff — it is that somebody else's unfinished work
lands in your merge under your name.

**Left open deliberately, not silently:** nobody has driven the terminal from a browser. The E2E tier
cannot stand up a Run in `Executing` with a live sandbox, so the pty is proven against real sbx
(a real tty, correct geometry, `^C` as SIGINT), the three refusals are proven at the hub, and the path
from xterm through the hub to the pty is proven nowhere. Merged knowingly on that basis.

**Time invested:** unmeasured — source **manual, because capture is still broken**. The same three
checks fail (`OTEL_EXPORTER_OTLP_ENDPOINT` unset, nothing listening, container not found) and
`usage.jsonl` has not been written to since **2026-08-10T20:54Z**, which is before this change began.
Tracked as [#307](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/307); this is the
second consecutive entry to report it.

## 2026-08-11 — `terminal-on-any-local-sandbox` (#311)

The Run-keyed terminal generalised to any sandbox this product has on this machine, bounded to the
namespace the startup sweep already claims. Eight commits. The mechanism was all #304's; what this
added was a second way in, a listing, an attach record that survives having no Run, and a machine-scoped
screen.

**What worked: the browser gap #304 left open was closed, and closing it was the only thing that could
have found either bug.** That entry merged knowingly with the path from xterm through the hub to the pty
"proven nowhere". Driving it found two faults, both latent in #304, neither reachable by reading.
`InteractivePty` called `posix_spawn`, which does not search `PATH`, so with the default bare `sbx` the
sandbox *listing* worked — it goes through `HeadlessProcess`, which resolves `PATH` — while opening a
terminal answered ENOENT. And the byte pump ran its first **blocking** read on the hub's own invocation
thread: `_ = Pump(...)` reads as fire-and-forget, but an async method runs synchronously until its first
*suspending* await, so once a WebSocket send completed synchronously the loop blocked there and the hub
method never returned. The symptom was precise and misleading — a working shell prompt on screen above a
surface still saying "Opening a shell…". This is [ADR-0001](../adr/0001-verify-claims-by-exercising-them.md)
and [ADR-0006](../adr/0006-a-capability-is-not-added-until-a-user-can-reach-it.md) collecting at once:
626 tests were green, including five against real microVMs, while the thing a Member actually touches
was broken in two places.

**Graduated to an ADR:** `posix_spawn` diverging from `Process.Start` has now cost one bug per change on
this same seam — the environment in #304, `PATH` here — so it is written down as
[ADR-0023](../adr/0023-a-hand-rolled-spawn-inherits-nothing-unless-it-says-so.md) rather than noted a
second time. The first occurrence was a retro note and the second happened anyway, which is what the
graduation rule exists to stop.

**What didn't: the design decided authorization twice and was wrong twice, because it reasoned about the
decorator instead of reading it.** First `[Requires]` paired with `IScopedToProject` — impossible, since
a sandbox no Run owns belongs to no project. Then no attribute at all, which the authorization decorator
**default-denies** by design, turning the read into a 403 the moment it was exercised. The vocabulary
already existed: `Access.FiltersToCaller`, defined in the codebase as "reaches across projects, and
narrows its own answer to the ones the caller may see", already used by `ListProjects` and `GetInbox`.
Two wrong answers and an ArchTest waiver written and then deleted, for a decision one file would have
settled. Same shape as #304's `run.attach` blind spot: the pipeline's conventions were reasoned about
from the outside rather than read.

**One change next time:** when a change adds a dispatched request whose permission is not held on a
project, read `Access` and the authorization decorator **before** writing the design's authorization
decision. A design that guesses at a seam it could have opened is a design that will be corrected in
review or in production, and this one was corrected twice.

**Noticed, not fixed:** the gated tests in `RealSbxTerminal_Should_Constraint` name their sandboxes
`aio-term-*`, outside the namespace the sweep claims — so a crashed gated test leaks a microVM nothing
will ever reclaim, which is the exact failure the sweep was written for. Also, `docs/adr/README.md`'s
index stops at 0013 while the files run to 0021, so ADR-0023's row was appended out of order rather than
conflicting with #312's own README edit. Both spun out rather than widened into this change.

**Time invested:** agent **1.65 h**, human keyboard time below the metric's resolution, **$55.65**,
86.7M cache-read and 185k output tokens — source **telemetry**. [#307](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/307)
appears fixed: all five `verify-telemetry.mjs` checks pass and `usage.jsonl` is current, making this the
first entry in three to report measured rather than manual time. Worth noting for what the numbers do
*not* say: the human time reads as ~0 because the metric counts keyboard and reading time in the CLI,
and this change was steered through a handful of decisions rather than typed.

---

## 2026-08-11 — `automation-claims-a-lifecycle-transition` (#310)

An Automation claims a transition in a stored, ordered Story lifecycle, and the board becomes the one
place that draws and authors the flow. The canvas is gone. A human step needs no representation at
all: it is a transition no Automation claims, so nothing fires until a person moves the label — which
works because a hand-off already travelled through the vendor label, making a person moving a label
the same mechanism as an Automation applying one.

**What worked: the migration was self-verifying.** 346 functional tests were red *purely* because the
migration did not exist yet — `PendingModelChangesWarning` is configured as an error, so every
fixture threw at initialisation. Their return to green was therefore objective proof that the
migration was right, not a judgement anybody had to trust. Two decisions made that proof mean
something: the risky group was deliberately separated from the safe ones and implemented under
supervision, and the tempting `w.Ignore(PendingModelChangesWarning)` was refused. Suppressing it
would have bought a green suite by disabling the guard that protects every configured hand-off.

**What didn't: the plan's ordering could not produce a green intermediate state, and nobody noticed
until the middle.** The moment `LifecycleStages` joined the model, all three functional projects
failed at fixture init; the migration could not precede the model it is written against. Groups 2–6
are one unavoidably-red window, and that was discovered by exercise during implementation rather
than stated at proposal time. A task in that window whose only evidence is a functional test cannot
be honoured, which is why 3.5 and 5.2 sat unticked until group 6 landed.

**What didn't: an acceptance criterion was unimplementable as written.** AC 10 asked that the count of
configured hand-offs before equal the count after. Read as `(automation, matched label)` pairs that is
unpreservable — an Automation with two matching output labels had two edges and AC 13 leaves nowhere
for the second, so the assertion would have raised on the first branching row in the real deployment.
It was replaced with a stronger per-row guard: every Automation that handed on claims one, none
invented one, and every label survives as claim-or-mark.

**One change next time:** when a change alters an EF model, the migration belongs in the *same* task
group as the model change — or the red window is named in `tasks.md` at proposal time. Nobody should
plan against a green middle that cannot exist.

**Time invested:** not measured (source: **manual**). No `.telemetry/sessions.jsonl` was present in
the worktree this change was implemented in, so the session→change join had nothing to read. Recorded
as unmeasured rather than estimated.

**ADR:** [ADR-0022](../adr/0022-an-order-a-person-can-rearrange-is-stored.md) — an order a person can
rearrange is stored, never derived. It supersedes DEC-053's "membership is derived from the edges and
never stored". First occurrence of the ordering lesson above, so it earns no ADR of its own yet.

## 2026-08-11 — product-corpus-v1 (#318)

**Time (telemetry, partial):** ~0.28h `cli` active time, $20.62, ~19.46M tokens (session
`548dd0c7-366d-4954-b987-b8307de393df`, sliced at the point `sessions.jsonl` marks
`change=product-corpus-v1` — the session carried prior work on another branch before a `/clear`).
No `type=user` datapoints on `active_time.total` for this session: human time is not measurable
from telemetry for this change. Stated as a gap, not folded into "manual" — `verify-telemetry.mjs`
passed all five checks, so capture itself was working; this particular metric simply carried no
`user`-typed points for this session.

**What worked:** writing the full v1 corpus — identity, glossary, capabilities, business rules,
journeys, roadmap — before grilling gave the human a draft to argue with instead of an open
question; the grill confirmed four decisions (cutover scope, DEC shape, lane, priority) in one
turn. Publishing the draft PR with the entire corpus visible turned spec review into reading the
actual text, not a summary of it.

**What didn't:** #316 landed on `main` mid-grill, resolving the UC-024 collision the opposite way
from the v1 draft (file-changes review → UC-028, not the grill). Caught only because apply task 1.3
explicitly diffed stable IDs against `origin/main` instead of trusting the draft was still current
— the same instinct ADR-0009 names for claims about existing behaviour, here applied to a corpus
draft's freshness. Separately, the cutover sweep (task 4.1) surfaced four live documents outside
the issue's explicit acceptance criteria — the product manual, the grill command and its skill, and
the Starter mirror of the grill command — because the AC named five specific files rather than a
mechanical sweep over the whole tree.

**One change next time:** when a change rewrites identity or process documentation, the acceptance
criteria should state a cutover grep as the criterion itself, not enumerate the files it expects to
touch — the same lesson ADR-0009 already named for retired-name sweeps ("enumerated by a command,
never a remembered list"), recurring here one level up, at the acceptance-criteria stage rather than
the execution stage.

**ADR:** none new — this is (at least) the third occurrence of ADR-0009's enumeration lesson (after
`sdk-built-images` #257 and `deploy-sdk-images` #260), one step earlier in the pipeline than before.
Existing ADR covers it; no new one needed.

## 2026-08-12 — terminal-output-test-cannot-pass-in-ci

Issue #327, PR #328. Spec-less lane (DEC-025): removes one test, changes no requirement, so there
is no OpenSpec bundle to archive.

**Time (telemetry, captured but unattributed):** session `5cf40c6b-52aa-4a88-9e49-9a737a7cdf5e`
records 1.71h active time and $76.13 — but `sessions.jsonl` maps it to no branch at all, and the
session spans four changes (#321, #323, #324, #327), so **no per-change split is derivable**.
Stated as a gap rather than folded into "manual": `verify-telemetry.mjs` passed all five checks, so
capture is working. The attribution is what failed, and for a structural reason — see the note
below.

**What worked:** the CI run history was the diagnostic that mattered. Two wrong local diagnoses and
four red runs collapsed into a decision the moment `gh run list` showed that #326's *own* pull
request had been red: that single fact turned "which of my changes broke this?" into "this was
never green", and the remedy followed immediately. Reading `--log-failed` rather than reasoning
about the failure did the same job a second time, when the test was deleted and a *different* test
failed.

**What didn't:** a root cause was announced twice on the strength of three green local runs, and
was wrong twice — first thread-pool starvation in `RunTerminalHub` (a real defect, but not this
one), then "removing the test makes it green" (a second, unrelated flake was behind it). Both
hypotheses were written into a commit message and a pull-request body *before* CI had confirmed
either, so both had to be corrected in public afterwards. Local green says almost nothing about a
two-core runner; three local runs said no more than one would have.

**One change next time:** when CI is red and local is green, read the run history and
`--log-failed` **before** forming a hypothesis — and never commit a causal claim to a message or a
PR body until CI has agreed with it. A hypothesis is cheap; a hypothesis in the permanent record is
not.

**Notes, carried rather than resolved here:**

1. **A red pull request reached `main`.** #326 merged with its own CI failing, which is what put
   `main` red and blocked #321 and #323 behind it. `/aio:sync` gates hard on a green rollup before
   the close-out commit, so either that merge did not go through this path or the gate was
   overridden. Structural, and worth knowing which — not diagnosed here because a hotfix is the
   wrong place to answer it.
2. **The spec-less lane cannot attribute its own telemetry.** The SessionStart hook maps a session
   to a change via its OpenSpec change directory; this lane has none by definition, so every
   spec-less change records unattributed time — as this one did.

**ADR:** none new. Point (1) is a candidate if it recurs — the graduation rule is the second
occurrence, and this is the first observed. Point (2) is a defect in the mapping hook rather than a
decision to record.

## 2026-08-12 — hold-replaces-the-plan-gate

Issue #321, PR #322. Supersedes DEC-039 and DEC-040; rewrites BR-007.

**Time (telemetry, captured but unattributed):** session
`5cf40c6b-52aa-4a88-9e49-9a737a7cdf5e` records 1.80h `cli` active time, $83.02 and ~141M tokens —
but `sessions.jsonl` maps it to no branch, and the session spans four changes (#321, #323, #324,
#327), so **no per-change split is derivable**. No `type=user` datapoints on `active_time.total`,
so human time is not measurable for this change either — the same gap the `product-corpus-v1` entry
recorded. `verify-telemetry.mjs` passes all five checks: capture works, attribution is what fails.
Fourteen session records *are* mapped to this branch and carry no usage at all, which is the shape
of the defect: the ids that are mapped are not the id that did the work.

**What worked:** the grill caught that the idea contradicted two **locked** decisions before any
code existed — DEC-039, and DEC-040 which the log records as chosen "deliberately, knowing it
doubles job orchestration" — and then found the argument for reversing them already written in the
corpus: DEC-062's accepted cost that the approval gate "is a workflow control now, not a
containment control". That turned a preference into a decision with a citation, which is the whole
reason it survived review. Writing the spec deltas against the real spec files then caught a fourth
affected capability the three-way split had missed: `default-automations`, the shipped starter
chain, which would otherwise have created fresh projects configured with a field that no longer
existed.

**What didn't:** a spec scenario was written that the design could not support — "a request
carrying an approval flag is refused as an unknown field", which needs a global JSON
`UnmappedMemberHandling` policy affecting every endpoint in the product. It was caught at
implementation and amended on the branch, but it should have been caught while writing the delta.
Separately, UC-026's real narrowing surfaced from four failing tests rather than from design: the
proposal named the emptied approval category as a follow-up, but nobody noticed `AwaitingInput` was
**also** dormant (DEC-062), so "the Inbox loses one of three categories" was in fact "the Inbox has
one producible category left".

**One change next time:** when writing a spec delta, check each new scenario against the design's
own decisions before committing it — a scenario asserting enforcement the design never describes is
invented rather than specified. And when retiring a capability, enumerate what *else* feeds the
surfaces it touches, not only what it feeds.

**ADR:** none new. The spec-scenario lesson is a candidate if it recurs; this is the first observed
occurrence and the graduation rule is the second.

## 2026-08-12 — aio-commands-honour-the-hold (#323)

**Time:** human ~0.25h (source: **manual**, per [ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) — human time is recorded, never derived); agent 0.73h `cli`
active time, $18.78, ~25.59M tokens (telemetry, session
`c8ca3ebc-bad8-4001-ab31-facac7734775`, mapped by the SessionStart hook at the moment the branch was
created). `verify-telemetry.mjs` passed all five checks. `active_time.total{type=user}` carried **no**
datapoints for this session — the second consecutive occurrence, which is what earned ADR-0025 rather
than a third identical paragraph.

**What worked: the issue's claim about its own surfaces was checked, not restated.** The issue argued
RULE-004 was satisfied because "surfaces do not overlap — `.claude/` and `docs/` here, `src/` there".
A `diff` found six **byte-identical** copies of the `/aio:*` command files under
`src/modules/Projects/.../Starter/workflow/`, shipped as the spec-first starter tier, with **no test
gating that identity** — `StarterCatalogue_Should_Constraint` checks frontmatter, bodies, path
collisions and wiring, but never compares the two trees. The conclusion survived (#321 shares no
file); the premise did not. The proposal recorded the corrected reasoning instead of repeating the
claim, and the mirror became its own task group — so the catalogue did not ship a loop this
repository no longer runs.

**What didn't: determinism was asserted from three data points, inside a refusal.** `/aio:sync`
correctly refused on a red rollup, and the refusal said the failing terminal test was "deterministic,
not flaky" on the strength of three consecutive failures. Within the hour #327 dropped that test, and
the next `main` run failed a *different* test that passes 110/110 locally. In a suite with more than
one flaky test, three failures in a row is bad luck, not evidence. The gate needed one fact — the
rollup is red — and the added claim was both unnecessary and false. ADR-0001 says exercise a claim
before recording it; this was a claim about *test behaviour over time*, which three samples cannot
establish.

**What didn't: the change could not complete its own final step.** AC 13 has `/aio:implement` apply
the hold when it marks the PR ready — but the label did not exist, and AC 2, added by this same
change, forbids a command creating one. The bootstrap ordering was invisible at spec review: the
proposal named provisioning as a task, and nobody noticed that a *later step of the same run* would
need it. Implementation stopped mid-flight for an out-of-band decision. The rule was right; the
sequencing was never stated.

**One change next time:** when a change introduces a label, config key, or any precondition its own
commands then require, `tasks.md` sequences provisioning first **and** the proposal names the command
step that first consumes it. A precondition whose consumer is inside the same change is not a
bootstrap footnote — it is an ordering constraint, and it belongs in the spec review where it is
still cheap.

**ADR:** [ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) —
human time is recorded by a person, never derived from telemetry. Second occurrence of the
`type=user` gap (after `product-corpus-v1` #318), so it graduates. The determinism lesson above is a
first occurrence of its own kind and earns no ADR yet; ADR-0001 already covers the instinct it
violated.

## 2026-08-12 — local-run-in-its-own-checkout (#331)

**Time:** human ~0.25h (source: **manual**, per
[ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) — human
time is recorded, never derived); agent 0.12h `cli` active time, $3.18, ~3.21M tokens across 2
mapped sessions — **a floor, not the cost**. `verify-telemetry.mjs` failed one check:
`OTEL_EXPORTER_OTLP_ENDPOINT` was UNSET, so most of this change's exports went to the OTLP default
port rather than this project's collector. The figure is reported with that named defect attached
rather than as a measurement, because a partial capture presented as a total is worse than no
number at all.

**What worked: the design measured git instead of assuming it, so implementation was transcription
rather than discovery.** D1 carried a probe table run against real `git` — `worktree add` from a
repository with uncommitted changes succeeds, the folder afterwards is unchanged, `worktree remove`
leaves the branch carrying its commit, and adding a worktree for a branch already checked out
elsewhere fails with exit 128. Every one of those held exactly as recorded when the code was
written; nothing in the git layer surprised the implementation. The fourth row paid twice: D2 turned
"git refuses a branch already checked out" into a *mechanical* second guard for BR-001, so the
one-Run-per-Story rule gained an enforcement point without gaining a rule. A design that had
asserted these properties instead of exercising them would have read identically and been worth
nothing.

**What didn't: the by-hand verification was blocked, repeatedly, by things the change has nothing to
do with.** Tasks 6.4 and 6.5 cost several cycles before they could even begin — `aspire run` refused
its port because another worktree's dev loop still held it; the Claude runtime failed with a
credential default that had to be cleared before the machine's own session could be used, and then
with "Not logged in" until the run was moved to OpenCode; and the prompt could not be reached at all
because prompts resolve only from the *vendor repository's default branch*, so a prompt written into
the scratch folder under test was invisible and the exercise needed a real path on `main` plus a
hand-written file in the live checkout to drive the commit path. None of this was a defect in the
change, and all of it was discovered one failure at a time, mid-verification. The lesson is not
"prepare the environment first" — it is that a task list which ends in "exercise it by hand" is
under-specified until it also names what the exercise *needs* to be able to run.

**One change next time:** fix the repository-root detection that cannot work in a git worktree.
`Terraform_Should_NeverConfigureTheLocalOwner` walks up looking for a `.git` **directory**, but in a
worktree `.git` is a *file* — so the test fails on every local full-suite run in exactly the
worktrees this repository now encourages, and passes in CI, which clones. A test that is red locally
and green remotely trains its reader to ignore a red suite, which is the opposite of what it is for.
First occurrence of its kind, so it earns no ADR — a one-line fix and an issue, not a decision.

## 2026-08-12 — local-run-checkout-is-ready-to-build (#332)

**Time:** human ~0.3h (source: **manual**, per
[ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) — human
time is recorded, never derived); agent time **unmeasured**. Not "manual because the change predates
telemetry" — manual **because capture is broken**. `verify-telemetry.mjs` fails one check:
`OTEL_EXPORTER_OTLP_ENDPOINT` is UNSET, so exports went to the OTLP default port rather than this
project's collector. The session is mapped in `.telemetry/sessions.jsonl`
(`84657235-0662-40ac-a668-0cc1db8623ec` → this change) and `usage.jsonl` holds **zero** matching
datapoints. No cost or token figure is reported, because there is none to report and an invented one
would be worse than the gap. Now tracked as #337.

**What worked: the shell was measured before it was designed around, and both measurements changed
the design.** `sh -lc` sourced `~/.profile` and wrote an unrelated error into the output *before
running anything* — so the login shell was rejected, and the setup command instead inherits the
Server process's own environment, which is exactly the one `LocalAgentProcessHost` already gives the
Agent. That agreement is the point: a dependency that installs for setup and is missing for the
Agent is the failure the design forecloses. The second measurement, that `a; b` reports only `b`'s
status, went into the spec as the shell's own rule rather than being papered over with argument
parsing the product would then have owned. Both then became assertions in the seam's suite, so they
are re-checked on every run instead of resting on a probe run once — which is the difference between
a design that measured and a design that remembers having measured.

**What didn't: two defects this repository had already named, in writing, were paid for again by the
very next change.** #331's retro recorded the `OTEL_EXPORTER_OTLP_ENDPOINT` gap in its Time line,
and its "one change next time" was the worktree `.git` root-detection ArchTest — *"a one-line fix and
an issue, not a decision."* Neither became an issue. This change lost **all** of its telemetry to the
first, and spent its full-suite investigation on the second, establishing that a red
`Terraform_Should_NeverConfigureTheLocalOwner` was pre-existing rather than newly broken. Both were
correctly diagnosed, both were written down in the place the process provides for writing things
down, and that changed nothing about what happened next. `AGENTS.md` already carries the instruction
the first would have needed — *"Check it works before starting a change, not at the retro"* — which
is the evidence that more prose was not the missing part.

**One change next time:** a retro finding that names a fix becomes a **tracked issue** before the
change syncs. A finding recorded only in the log is a note; the next change does not read the log,
it hits the defect. Two independent instances inside one change is what separates this from bad
luck, and it is the second occurrence, so it graduates.

**ADR:** [ADR-0026](../adr/0026-a-retro-finding-that-names-a-fix-becomes-a-tracked-issue.md) — a
retro finding that names a fix becomes a tracked issue before the change syncs. Its first
application is this entry: #337 (the telemetry endpoint) and #338 (the worktree root detection) were
filed during this sync, and the ADR is written so that a finding with no concrete remedy still
creates nothing.

## 2026-08-12 — shell-projects-tree (#335)

**Time:** human ~0.5h (source: **manual**, per
[ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) — human
time is recorded, never derived); agent time **unmeasured**. Manual **because capture is broken**,
not because the change predates telemetry. `verify-telemetry.mjs` fails the same single check #332's
entry named: `OTEL_EXPORTER_OTLP_ENDPOINT` is UNSET, so exports go to the OTLP default port rather
than this project's collector. All three sessions are mapped in `.telemetry/sessions.jsonl`
(`d10e1dc1…`, `d6b2a16a…`, `2ff0e789…` → this change) and `usage.jsonl` holds **zero** matching
datapoints; its last write was 14:36, before this work began at 17:32. No cost or token figure is
reported because there is none. This is the **second consecutive** change to lose its measurement to
an already-tracked defect (#337, open) — the ADR-0026 filing worked, the fix has not happened yet.

**What worked: reading the code before writing the spec, rather than after.** Four design points came
from verified facts, and every one of them would have been wrong from memory. `Story.Labels` is a
`text[]` column, so the hold's case fold cannot move into SQL — a translated `Contains` would report
`HITL` as unheld, which is precisely the failure DEC-056 exists to prevent, and the test for it is
functional rather than unit because that is the only tier where the claim is real. The Mirror is
Postgres, so held Stories cost no vendor call, which is what makes a 30-second cadence affordable and
what separates this surface from the open-changes one whose own docstring forbids exactly that.
`--sidebar-w-collapsed` is 64px, too narrow to indent, hence the rail's popover. And the kit has no
collapsible primitive, so none was added.

Also: **distrusting a green result.** Three Playwright tests reporting under a second is not a
credible time for navigating, collapsing 280px to 64px and opening a popover. Rather than report it,
the href assertion was mutated to a deliberately wrong value and confirmed to fail with the real
rendered string — `/projects/<id>/stories/77` — then reverted and re-run green. The tests were sound;
the difference is that the confidence was earned instead of assumed. The milliseconds were test
bodies and the AppHost boot was the collection fixture's 35 seconds, which the first reading could
not distinguish from a no-op suite.

**What didn't: the accepted spec was wrong twice, and implementing it is what found both.** A `Run`
targets exactly one of a Story or an open change — "never both, never neither", as `Run.cs` says in
so many words — so `VendorStoryId` is null for a change-targeted Run and the approved Project → Story
→ Run shape had nowhere to put one. Omitting those Runs would have left a panel about live work
silent about a Run that is executing; reporting them bare would have defeated the reason Runs nest
under a subject at all. Separately, "non-terminal" is not "live": DEC-067's retired `Planning` and
`AwaitingApproval` are neither, and enumerating the live states was the only honest way to say so.
Both errors were prose that read correctly against other prose. Spec review cannot catch that class
without opening the file the claim is about, which is an argument for citing real paths in a spec —
not for reviewing harder.

**One change next time:** read load-bearing config with the file tool, never a shell `cat` a hook may
rewrite. `workflow.json` came back **without** `holdLabel`, and a `git diff --name-only` came back as
formatted prose that made a branch-overlap check read as zero files. A filtered read that drops keys
still parses and still looks complete, which is strictly worse than one that fails. Filed as **#341**
per ADR-0026 rather than left in this log — the log already recorded this masking twice, for
`rtk pnpm build` and for `rtk git commit`, and recording it a third time is what ADR-0026 exists to
stop. No new ADR: 0026 already governs the rule, and the previous entry's own finding was that more
prose was not the missing part.

## 2026-08-12 — backlog-auto-refresh (#340)

**Time:** human ~0.5h (source: **manual**, per
[ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) — human
time is recorded, never derived); agent time **unmeasured**. Manual **because capture is broken**,
not because the change predates telemetry. `verify-telemetry.mjs` fails the same single check the
last two entries named: `OTEL_EXPORTER_OTLP_ENDPOINT` is UNSET, so exports go to the OTLP default
port rather than this project's collector. Both sessions are mapped in `.telemetry/sessions.jsonl`
(`028de7d4…`, `5c61b375…` → this change) and `usage.jsonl` holds **zero** matching datapoints; its
last write was 14:36, before this work began at 19:57. No cost or token figure is reported because
there is none. This is the **third consecutive** change to lose its measurement to an already-tracked
defect (#337, open, `status:backlog`) — the filing worked, the fix has not happened yet.

**What worked: verifying library behaviour against the installed source instead of from memory.**
Three claims were checked in `@tanstack/query-core@5.101.4` before they entered the design, and two
of them changed the work. `refetchOnWindowFocus: true` is gated by `staleTime`
(`queryObserver.js:450-456`), so the obvious implementation would have been silently suppressed for
exactly the 30-second window acceptance criterion 2 exists to fix — it would have shipped the bug
under the name of its own fix, and passed review, because the option *looks* like it does the thing.
Only the literal `"always"` bypasses the gate. Second, the interval timer is already gated on
`focusManager.isFocused()` (`:215`), which is `document.visibilityState !== "hidden"`
(`focusManager.js:55-60`) — so "a hidden tab is idle" needed no code, and the task became asserting
it so a later change cannot flip it silently. The same discipline applied to this repo's own code:
the design flagged a board-drag hazard, and `useMoveStory.onMutate:52` turned out to already call
`cancelQueries` with a comment naming that exact hazard. The guard predated the change; a second one
would have been noise.

**What didn't: two tasks were written against a test harness nobody had opened.** `tasks.md` said to
"drag a card and hold it across an interval boundary" — but Playwright cannot perform an HTML5 drag,
and this repository's own `KanbanBoard_Should_Constraint.cs:74-75` says so in a comment written for
precisely this reason. It also said to "let the server poll reconcile", while the E2E fixture sets
`Backlog__PollingEnabled = "false"`. Both instructions survived spec review because both read
correctly as prose. The proposal and the design were sound — what was wrong was the layer that
encoded assumptions about the *harness*, and one grep of the test project would have falsified both.
Reworking them mid-implementation produced a better test than the original wording would have: with
the server poll off, the Mirror is reconciled out-of-band through `page.APIRequest`, which never
invalidates the browser's cache, so the page catches up on nothing except the behaviour under test.
Separately, PR #336's merge state was read through `gh pr list --search`, which goes through GitHub's
lagging search index; it reported an already-merged dependency as `OPEN`, and that wrong answer was
offered to the human as a reason to stop the command. `gh pr view` reads the API directly and was
correct.

**One change next time: before writing a task that says "verify X in the harness", open the
harness.** Concretely — at proposal time, grep the E2E project for the capability the task assumes
(drag, polling, visibility, request counting) rather than describing the check in prose and
discovering at implementation time that the harness cannot perform it. This is the previous entry's
own rule — read the thing the claim is about — extended from load-bearing *config* to load-bearing
*harness assumptions*. No issue filed and no new ADR: this is the first occurrence, and ADR-0026's
graduation rule is the second. The `gh pr list --search` staleness is likewise recorded here rather
than filed, for the same reason.

## 2026-08-13 — ship-a-change-unattended (#343)

**Time:** human ~0.5h (source: **manual**, per
[ADR-0025](../adr/0025-human-time-is-recorded-by-a-person-never-derived-from-telemetry.md) — human
time is recorded, never derived); agent time **unmeasured**. Manual **because capture is broken**, not
because the change predates telemetry. `verify-telemetry.mjs` fails the same single check the last
three entries named: `OTEL_EXPORTER_OTLP_ENDPOINT` is UNSET, so exports go to the OTLP default port
rather than this project's collector. The session is mapped in `.telemetry/sessions.jsonl`
(`a51a66a2…` → this change) and `usage.jsonl` holds **zero** matching datapoints; its last write was
2026-08-12T14:36, before this work began at 21:48. No cost or token figure is reported because there
is none. This is the **fourth consecutive** change to lose its measurement to already-tracked
[#337](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/337) (open) — the ADR-0026
filing worked, the fix has not happened yet.

**What worked: the grill closed a decision instead of routing around a rule.** The request — propose,
implement and sync with no hold — collided head-on with two locked requirements of
`openspec/specs/workflow-commands/spec.md`: *the hold is a refusal, and no command ever clears it*,
and *clearing the hold is the approval*. The collision was surfaced in the grill's first response
rather than met at implementation, and seven answers across two question clusters turned it into a
recorded decision (ADR-0027, DEC-068) instead of an exception. The mechanism that resulted preserves
the invariant **literally**: `/aio:ship` applies no hold on its happy path, so nothing ever clears
one. RULE-006's instinct — no proposing on a guess — applied to a decision the owner could simply
make, once it was named.

**What worked: the design changed while it was still free to change.** The proposal first said
`/aio:ship` would orchestrate the skills directly. Writing `design.md` killed that: it would force
sync's load-bearing orderings — CI-green before the `[skip ci]` commit, lint before merge — into a
second file, which is ADR-0003's exact failure and #202's four extra days of red. Reuse-by-invocation
replaced it, and the result is checkable: `git diff --numstat` shows each of the three staged command
files replaced **exactly one line**, the "Never remove the hold" guardrail, strengthened rather than
weakened.

**What didn't: [#341](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/341) was
filed one change ago, and this change walked straight into it.** #335's entry recorded that the
filtering command proxy makes `git diff --name-only` come back as prose so a branch-overlap check
reads as zero files, and filed the issue. Two verifications here were then run through exactly that
hazard: `rg` reported **no** `hitl` outside `.claude/workflow.json` (≈69 lines contain it, including
DEC-067, `05-business-rules.md` and `StoryHold.cs`), and `git diff | grep '^-'` reported **no** removed
lines (there was one per file). Both failed in the direction that reads *clean*, which is the whole
danger — a check that under-reports looks identical to a check that passed. Redone with a plain
reader, and `tasks.md` now forbids `rg` for that scan. Fourth occurrence of this masking, with the
rule already in this log **and** an open issue: prose plus a filed issue did not prevent it, and the
untaken remedy is mechanical.

**What didn't: the starter mirror was found at verification, not at proposal.**
`src/modules/Projects/.../Starter/workflow/` ships byte-identical copies of the six `/aio:*` command
files — all six verified identical to `origin/main` immediately before this change edited three of
them. Nothing tests that identity, a gap #323's retro named and this change rediscovered at task 5.2.
Not mirroring is the right call (product scope is out of #343, and shipping an unreviewed-merge route
into other people's repositories is a bigger decision than ADR-0027 made — ADR-0021's habitat logic),
but "deliberate" was established *after* the edits rather than in the spec. Second occurrence of the
same structural gap, so it leaves this log as
[#346](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/346) per ADR-0026.

**One change next time:** a change that edits a `/aio:*` command file carries a **mirror task in
`tasks.md` at proposal time** — the starter copies have no test gating their identity, so the check
belongs in the plan, where spec review can see it, not in the verification pass where it is a lucky
catch.

**Route:** this change landed through the **staged** path — both holds applied, both cleared by a
person — which is deliberate: a human read the ADR that authorises the unattended route before that
route existed. `/aio:ship` is **not** exercised end to end by this change; 6 of the issue's 15
acceptance criteria first come true on a real unattended run.

**ADR:** none new. ADR-0027 (a change may reach `main` unreviewed, on one explicit invocation) is
authored *by* this change rather than graduated from it. Both "didn't" findings name concrete remedies
already tracked as issues (#341, #346), and #335 established the precedent that more prose is not the
missing part where ADR-0026 already governs.

## 2026-08-13 — host-credential-decision (#223)

**Route: unattended.** This change was proposed, implemented and merged by `/aio:ship 223` in a single
run (DEC-068 / [ADR-0027](../adr/0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md)).
**No human read its spec or its diff before it reached `main`.** It is the **first** change to travel
that route end to end — #343 authored it but landed staged — so 6 of #343's acceptance criteria first
come true here. **Its three reflection points below are UNCONFIRMED:** nobody reviewed them, and an
entry that implied otherwise would corrupt the one record this route is measured by (ADR-0018).

**Time:** human **unmeasured**; agent time **unmeasured**. Manual **because capture is broken**, and
broken twice over. (1) `verify-telemetry.mjs` fails the same check the last four entries named:
`OTEL_EXPORTER_OTLP_ENDPOINT` is UNSET, so exports go to the OTLP default port rather than this
project's collector — [#337](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/337),
open. (2) **New, and specific to this route:** the session is mapped with `change: ""` and never
corrected, so `grep host-credential-decision .telemetry/sessions.jsonl` returns **zero** records for a
change that took a whole session. Filed as
[#349](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/349).

**What worked: the decision got smaller the moment the seam was read instead of assumed.** OPN-006
looked like a fourteen-signature change — every `IBacklogConnector` method takes `string token`, and
the issue framed the blast radius that way. Reading `connector-configuration` showed credential
*resolution* already sits upstream of the seam: token values resolve through **one** abstraction, per
read, with a rotated secret picked up on the next resolution and a missing name failing loudly rather
than falling back to an empty credential. A host-derived credential is another resolver behind the
seam that exists. That single reading turned the decision from "change the Connector" into "add a
resolver", and it is why the ADR could permit the thing #347 asked for instead of refusing it on cost.

**What didn't: the one factual question the issue set could not be answered the way it was asked.**
Option (d) hinged on *whether a git credential helper's output may authenticate vendor API calls*. The
direct test — read this machine's real `github.com` credential and probe the API — was refused by the
session's own guardrails, correctly. The claim was **not** downgraded to prose: a stand-in helper
emitting every key the protocol permits proved the decisive half structurally — the protocol carries
`username`, `password`, `oauth_refresh_token`, `password_expiry_utc` and **no scope, no capability, no
naming of the application a credential was minted for**. That reframed the answer from *can it work*
(machine-specific, unknowable in general) to *can the product know it will* (no, by construction), which
is the half the decision actually turned on. The lesson is that a blocked probe is a prompt to find the
structural form of the question, not a licence to assert the answer — but it was luck that this
question had one.

**What didn't: an unattended run decided something the issue assigned to a person.** #223 names ACT-001
— *"the owner decides, DEC-003"* — and its deliverable is an ADR plus a `DEC-*`. `/aio:ship` merged it
with nobody reading it. Sync's own unattended clause says DEC-068 *"authorises shipping code nobody
read; it does not authorise deciding architecture nobody read"* — written about a retro's structural
finding, but the principle reaches this change's entire scope, and no gate noticed, because no gate
keys on what an issue *delivers*. The run proceeded because the invocation authorises it and the ACs
made the analysis tractable; that is defensible and it is not the same as being reviewed. **This is the
concrete shape of DEC-068's stated cost (2)** — *"a run that under-detects its own ambiguity ships a
guess"* — recorded here as the first real instance rather than as a hypothetical.

**One change next time:** `/aio:ship`'s halting contract should treat **an issue whose deliverable is a
decision** (RULE-006 decision-closure, an ADR or a `DEC-*` in its acceptance criteria) as a question the
issue does not answer, and halt — the same way it halts on a red rollup. The route is right for work
whose shape a person already accepted at the grill; a decision-closure issue is by definition work whose
shape is the open question. Not filed as an issue yet: it is the **first** occurrence, and ADR-0026's
graduation rule is the second — but it is the one thing here most likely to recur, because nothing
currently distinguishes the two kinds of issue at the gate.

**ADR:** ADR-0028 is authored *by* this change (closing OPN-006), not graduated from it. **No ADR was
written for the reflections above**, per `/aio:sync`'s unattended clause — DEC-068 does not authorise
deciding architecture nobody read, so the structural finding became
[#349](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/349) instead (ADR-0026). The
ADR that route may owe is written by a person on a later change.

## 2026-08-13 — ssh-net-advisory (PR #351, no issue)

- **Worked:** The halt paid for itself. `/aio:ship 347` stopped on a red build instead of pushing
  through it, and the cause was checked on a detached `origin/main` worktree rather than assumed —
  one command turned "probably not mine" into a fact, which is what let this land as its own change
  instead of being smuggled into a feature PR. Pinning beat suppressing: NuGet's audit was right
  ([GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284), high, everything
  `<= 2025.1.0`) and 2026.0.0 was already published. The bump was then **exercised** rather than
  trusted — the Backlog functional suite, 97 tests against real Testcontainers Postgres, passes on
  it. A green build could not have shown that a major-version jump in a transitive dependency leaves
  Testcontainers working at runtime, and that is the claim the pin actually rests on (ADR-0001).
- **Didn't:** An advisory published against a **pinned transitive** dependency turned every branch in
  the repository red simultaneously, with no owner and no signal. `TreatWarningsAsErrors` promoted it
  to a build error in every test project, so `dotnet test` could not run at all and no branch could
  reach CI-green — `main` included, whose own last run was green at 08:15Z the same day. At the
  console this is indistinguishable from "you broke the build", and nothing in the repository says
  "main is red for a reason that is not yours". Separately, this change carries **no issue and no
  telemetry**: PR-only at the maintainer's explicit direction (so DEC-025's lane is knowingly
  incomplete — no `lane:spec-less` label to carry, no `status:done` to set), and
  `.telemetry/sessions.jsonl` does not exist in this worktree at all, so the time below is manual.
  That is the second live instance of the attribution gap
  [#349](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/349) names, reached by a
  different route than the one it describes.
- **Next time:** Treat a dependency-audit failure as an **infrastructure event, not a change
  defect** — put the "does `origin/main` fail identically?" check inside the `/aio:*` halt path, so
  the diagnosis is automatic instead of a judgement call made well. It cost one command here only
  because the halt happened to prompt the question.
- **Time invested:** human ~0.1h, agent ~0.6h, cost unknown (source: **manual** — no
  `.telemetry/sessions.jsonl` in this worktree, so `collect-usage` had nothing to join on)
- **ADR:** none. The "next time" point is structural and is the kind that recurs, but this is its
  **first** occurrence and ADR-0026's graduation rule is the second — recorded here so the second
  one is recognisable rather than pre-empted.

## 2026-08-13 — filter-proxy-passthrough (#341, PR #352)

**Route: `/aio:ship 341`, unattended (DEC-068, ADR-0027). No human read this spec or this diff before
merge. The three reflection points below are UNCONFIRMED — nobody confirmed them.**

- **Worked:** Negative-testing caught what reading could not. The verifier shipped here was **vacuous
  twice** before it worked, and both versions looked correct: the first ran its commands with
  `execSync` inside Node, which bypasses the Claude Code `PreToolUse` hook entirely, so every check
  compared a command with itself and returned three confident greens; the second resolved the rewrite
  properly but used `execFileSync`, which throws on any non-zero status, and `rtk rewrite` exits **3**
  for a rewrite-with-prompt — so the catch reported "no rewrite" and the guard could be deleted with
  everything still passing. Only `spawnSync` plus an explicit status check made the thing capable of
  failing, proven by removing the guard and watching it report `git reported 2 path(s), 'rtk git diff
  --name-only …' reported 3 and added prose decoration`, exit 1. A check that cannot fail is worse
  than no check, and the only way that was discovered was by deliberately breaking the thing it
  guards (ADR-0004, applied to a test rather than to infrastructure).
- **Didn't:** The issue's own premise was wrong, and it took a `grep` to notice. #341 states that
  *"`AGENTS.md` guidance and three retro entries have not prevented recurrence"* — but `rtk` appeared
  **nowhere** in `AGENTS.md`. The guidance only ever existed in retro entries, which are read after
  the fact, so the conclusion drawn from its failure ("more prose is not the missing part") was drawn
  from an instruction that was never in the place an agent reads before acting. The remedy still
  holds and is stronger for it, but a filed finding was carried forward for three changes without
  anyone checking its central claim. Separately, the fix that actually protects this repository lives
  **outside** it — a passthrough list in the proxy's own config on one machine — and a fresh clone
  inherits none of it; the committed verifier is the only thing that makes that absence visible
  rather than silent.
- **Next time:** When a retro finding claims an existing instruction failed, **verify the instruction
  exists** before designing around its failure. Cheap (`grep`), and here it changed the shape of the
  work: the deliverable was closing a gap, not replacing a defence. Second-order: a change whose real
  fix is out-of-repo should be required to ship the check that detects its absence — which this one
  did, but by judgement rather than by rule.
- **Time invested:** human ~0.05h, agent ~1.0h (wall clock), cost unknown — source: **manual**, and
  broken for two independent reasons, both named rather than absorbed. (1) `OTEL_EXPORTER_OTLP_ENDPOINT`
  was unset when this session's client started, so nothing was exported at all —
  [#337](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/337), whose own setting
  half was fixed *during* this session and therefore binds only the next one; `usage.jsonl` holds
  **zero** rows for session `254e60f3…`. (2) Even with capture working, this change could not have
  been attributed: `map-session-change.mjs` keys `change` off a directory under `openspec/changes/`,
  which a `lane:spec-less` change never has —
  [#353](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/353), filed from this sync.
- **ADR:** none. The reflections above are structural, and the keying defect is the kind that recurs,
  but per `/aio:sync`'s unattended clause DEC-068 authorises shipping code nobody read and **not**
  deciding architecture nobody read — so the finding became
  [#353](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/353) (ADR-0026) instead.
  The ADR this may owe is written by a person on a later change.

## 2026-08-13 — telemetry-preflight (#337, PR #354)

**Route: `/aio:ship 337`, unattended (DEC-068, ADR-0027). No human read this spec or this diff before
merge. The three reflection points below are UNCONFIRMED — nobody confirmed them.**

- **Worked:** The defect was partly in the remedy text, and fixing that was most of the value. The old
  wording told the reader to *"set it in the shell profile the app inherits"* — true, useless, and the
  direct cause of this fix's own first failed attempt, which put the export in `~/.zshrc` and watched
  the check keep reporting `UNSET`. zsh reads `.zshrc` only for interactive shells, so a
  non-interactive client inherits nothing. The remedy now names the file, the anti-file, and the
  reason. Both branches were then exercised rather than only the happy one: healthy prints one line
  and exits 0, endpoint-unset prints the failing check and its remedy and **still** exits 0.
- **Didn't:** **A `SessionStart` hook cannot be exercised by the session that writes it.** The script
  was verified directly, and the exact configured command string was verified with
  `CLAUDE_PROJECT_DIR` expanded, both paths — but whether the client actually fires this entry and
  surfaces its stdout is **unverified**, because doing so requires a session that starts after the
  change exists. That is precisely ADR-0001's failure mode (shipping a habitat nobody ran), reached by
  a route that has no way to avoid it from inside the change. The same structural fact applies to the
  fix itself: the client reads `OTEL_EXPORTER_OTLP_ENDPOINT` at startup, so the session that fixed the
  variable is not the session that benefits — this change ships unmeasured for the reason it exists to
  remove, the third consecutive entry to report `manual`.
- **Next time:** A change whose deliverable is **read at process start** (an env var, a `SessionStart`
  hook, a client setting) should state in its own PR that it takes effect on the *next* session, and
  the following change's retro should confirm it fired. Otherwise "fixed" and "verified" get conflated
  in the record, which is the same conflation ADR-0004 exists to prevent — and here the conflation is
  invited by the change's own shape rather than by carelessness.
- **Time invested:** human ~0.05h, agent ~0.5h (wall clock), cost unknown — source: **manual**, for
  both of the reasons this change and its predecessor name. (1) `usage.jsonl` was last written
  2026-08-12T14:36Z and holds **zero** rows for session `254e60f3…`: the endpoint was unset when this
  client started, which is [#337](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/337)
  itself. (2) Even with capture working this change could not be attributed, because
  `map-session-change.mjs` keys `change` off a directory under `openspec/changes/` that a
  `lane:spec-less` change never creates —
  [#353](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/353).
- **ADR:** none. The "next time" point is structural and this is its **first** clear occurrence, so
  ADR-0026's graduation rule (the second) is not met; recorded here so the second is recognisable. Per
  `/aio:sync`'s unattended clause, no ADR is written on this route regardless — DEC-068 authorises
  shipping code nobody read, not deciding architecture nobody read.

## 2026-08-13 — worktree-repository-root (#338, PR #355)

**Route: `/aio:ship 338`, unattended (DEC-068, ADR-0027). No human read this spec or this diff before
merge. The three reflection points below are UNCONFIRMED — nobody confirmed them.**

- **Worked:** Running the thing before fixing it found a different and worse defect than the one
  filed. #338 said the ArchTest *fails* in a worktree; it passes. This repository's worktrees live
  **inside** the main checkout at `.claude/worktrees/<name>`, so the walk does not run off the
  filesystem root — it steps past the worktree's `.git` **file** and stops at the main checkout's
  `.git` **directory**. The actual defect was a **silent false green**:
  `Terraform_Should_NeverConfigureTheLocalOwner` was validating `main`'s `infra/*.tf` while running
  from a worktree, so since #331 made worktrees routine, every Terraform change made in one has passed
  #119's first lock unexamined. Proven behaviourally rather than argued — a violating `.tf` planted in
  the worktree passes before the fix and fails after it. A red test gets ignored; a green test that
  read the wrong checkout gets believed.
- **Didn't:** **The grill claimed to have checked the premise, and had checked two of its three
  claims.** It confirmed the code location (`DeploymentIdentity_Should_Constraint.cs:40`) and that
  `.git` is a file in a worktree, then asserted the premise verified — without running the test. The
  skipped claim was the symptom, and the symptom was the false one. This is the **second consecutive**
  ADR-0026 filing whose central claim did not hold: #341 asserted that `AGENTS.md` guidance had failed,
  and `rtk` appeared nowhere in `AGENTS.md` at all. In both cases the fix was unchanged and the real
  defect was worse, so nothing wrong shipped — but the PR body, commit message and retro all argued
  from a premise that was false, which is the part that persists.
- **Next time:** `/aio:grill` should require an asserted **symptom** to have been *observed*, not
  merely located in the code — the Definition of Ready does not currently distinguish those, and both
  failures fell exactly in the gap. **ADR-0026's graduation rule is now met** (second occurrence), so
  an ADR is owed.
- **Time invested:** human ~0.05h, agent ~0.4h (wall clock), cost unknown — source: **manual**, for
  the two named reasons that also applied to the previous two entries: capture was not running for
  this session ([#337](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/337), whose
  fix binds the next session, not this one) and a `lane:spec-less` change cannot be attributed at all
  ([#353](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/353)).
- **ADR:** owed but deliberately **not written**. Per `/aio:sync`'s unattended clause, DEC-068
  authorises shipping code nobody read and not deciding architecture nobody read, so the graduated
  finding became [#356](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/356)
  instead. It is a person's to write on a later change — and it is the third ADR this batch has
  deferred by that rule, which is itself worth noticing.

## 2026-08-13 — local-folder-project (#347, PR #350)

**Route: `/aio:ship 347`, unattended (DEC-068, ADR-0027), on a blanket approval to run the waves. No
human read this diff before merge. The three reflection points below are UNCONFIRMED.**

- **Worked:** The change was assessed before it was continued, and the assessment was the whole value.
  `tasks.md` was **0 of 35 checked** while sections 1–5 were substantively complete — so the checkbox
  record was useless in both directions, and reading it either way would have been wrong. Checking the
  artifacts the tasks name (`CredentialReference`, `IConnectorCredentialResolver`,
  `IHostCredentialResolver`, `GitCredentialHelperResolver`, the `ConnectorHostCredential` migration)
  established what was really done in one pass, and `git diff --name-only origin/main...` returning
  **zero** `src/frontend/` files established what was not: section 6, the portal — which is the issue's
  **headline** acceptance criterion. A change with all seven CI checks green was one unimplemented
  section away from shipping its mechanism without its capability.
- **Didn't:** **Task 6.5 could not be done as written, and nothing said so until it was attempted.** It
  asks for "frontend tests", and this repository has **no frontend test framework** — no vitest, no
  testing-library, no `test` script, zero `*.test.tsx`. The task had been sitting in an approved,
  spec-validated bundle since the proposal, and `openspec-validate` passes happily on a task that
  cannot be performed. It was resolved by using the E2E suite, the only lane that exercises the built
  UI, which needed no new dependency — but that was a choice made at implementation time about a
  question the proposal should have settled. Related: criterion 6.2 says the derived coordinates are
  "editable before saving" while the issue also forbids a new HTTP surface and derivation happens
  inside the create handler, so there is nothing to edit before saving; it was implemented as a
  read-back with editing in the Connector's own form, which is a *reading* of the criterion rather
  than a transcription of it.
- **Next time:** A task that names a **test kind the repository does not have** should fail the grill,
  not the implementation. The Definition of Ready checks that criteria are evaluable; it does not check
  that they are *performable with what exists*. Cheap test: for each task naming a tool, lane or
  framework, confirm the repository has one. This is the same family as
  [#356](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/356) — a claim accepted
  without being exercised — reached from the tasks side rather than the symptom side.
- **Time invested:** human ~0.1h, agent ~1.5h (wall clock), cost unknown — source: **manual**, for the
  reasons the previous three entries name and this one inherits: capture was not running for this
  session ([#337](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/337) — its fix
  binds the next session, not this one) and attribution keys off a live `openspec/changes/<name>`
  directory, which this change had while implementing and no longer has once archived
  ([#353](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/353)).
- **ADR:** none. The "next time" point is structural but this is its first clear occurrence as a
  *tasks-side* failure, so ADR-0026's graduation rule is not met; recorded here so the second is
  recognisable, and noted on #356 as adjacent evidence. Per `/aio:sync`'s unattended clause no ADR is
  written on this route regardless.

## 2026-08-13 — local-terminal-decision (#357, PR #359)

**Route: `/aio:ship 357`, unattended (DEC-068, ADR-0027), on a blanket approval to run the waves. No
human read this decision before it merged. The three reflection points below are UNCONFIRMED.**

- **Worked:** The decision was not allowed to inherit its answer. The tempting move was to read DEC-065
  ("a machine its operator owns is not one somebody else pays for or administers") as already permitting
  a host terminal and skip straight to #358. It does not: DEC-065 was decided about a session *inside a
  Run's sandbox*, its companion requirement literally presumes one exists, and every terminal shipped
  under it opens inside a microVM. Naming that gap is what turned "obviously allowed" into a real
  question with a real cost — and the cost is now written where an implementer will hit it: the child's
  environment must **not** be inherited from the server process, because `posix_spawn` takes the whole
  environment and the sbx path deliberately inherits, which is harmless behind a boundary and not
  harmless without one. That would have been found at runtime, on a host, with the server's environment
  in a shell.
- **Didn't:** **This is the second time a decision-shaped issue reached `main` unread**, and the first
  time it happened over a stated objection. #223's retro recorded the reservation exactly — that
  `/aio:ship`'s halting contract *"should treat an issue whose deliverable is a decision as a question
  the issue does not answer, and halt"* — logged as a first occurrence awaiting a second. This is the
  second. It was raised before proceeding and overridden by an explicit approval, which is a legitimate
  thing for the product authority to do; what is **not** legitimate is the record being ambiguous about
  it, so ADR-0029 and DEC-070 both say plainly that this had no review. A reader may treat option (c) as
  a proposal that shipped rather than a position anyone defended.
- **Next time:** ADR-0026's graduation rule is now **met** for the decision-shaped-issue finding —
  second occurrence — so the ADR it owes is due, and it cannot be written on this route. It should say
  whether a blanket approval is sufficient authorisation for a `DEC-*`, or whether decision-closure
  issues need a per-issue acknowledgement even when the loop is authorised. Tracked rather than written
  here, per the unattended clause.
- **Time invested:** human ~0.05h, agent ~0.6h (wall clock), cost unknown — source: **manual**, same two
  causes as the four entries before it (#337 for capture, #353 for attribution). Five consecutive
  `manual` entries is now itself the measurement: DEC-068 was authorised on measured per-change time,
  and the route it created has produced none.
- **ADR:** ADR-0029 is authored *by* this change (closing OPN-008), not graduated from it. The
  graduated finding above is filed as its own issue rather than written as an ADR, per `/aio:sync`'s
  unattended clause.

## 2026-08-13 — local-run-terminal (#358, PR #361)

**Route: `/aio:ship 358`, unattended (DEC-068, ADR-0027), on a blanket approval to run the waves. No
human read this diff before merge. The three reflection points below are UNCONFIRMED.**

- **Worked:** **Deciding first paid off in code, not just in paperwork.** The temptation was to skip
  #357 and implement straight away — the gap looked like a composition oversight, and the fix looked
  like moving two registrations past an early return. Writing the decision first surfaced the actual
  hazard: `posix_spawn` takes the child's **whole** environment, and the sandbox path deliberately
  inherits it because the sbx CLI panics without `$HOME`. Behind a microVM that is harmless, since
  nothing crosses the boundary; on the host there is none, so reusing that path would have handed
  whoever is typing everything the habitat resolved into the server process — a Connector's credential
  among it. It would have looked correct in review and been wrong in use. The other pleasant surprise
  was the opposite of a blocker: `posix_spawn_file_actions_addchdir_np` is **not** variadic, unlike the
  `ioctl` the same file documents .NET as unable to call, so the working directory was a missing
  parameter rather than a platform limitation.
- **Didn't:** **Two of the five new tests passed for the wrong reason first, and the second one is the
  interesting failure.** The missing-directory refusal did not name the directory, because
  `addchdir_np` only *records* the action — the path is not resolved until the spawn, so the error
  surfaced somewhere that said "command not found on PATH". And the environment-leak probe matched the
  command the **tty echoed back** rather than the shell's reply: a terminal echoes what you type, so
  waiting for a substring that occurs in the command itself returns before anything has been answered.
  It reported a pass on the echo. That is now the third time today a test was green for a reason
  unrelated to its claim, and the only thing that caught any of them was deliberately breaking the
  thing under test. Separately: **this change has no OpenSpec bundle**, because #357 landed its
  requirements — so `/aio:sync`'s archive step had nothing to do on a Product change that is not on the
  spec-less lane, which no rule anticipates.
- **Next time:** When a test asserts an **absence** — no leak, no inheritance, nothing present — the
  sentinel must be constructed so it *cannot* appear in the stimulus. Here that meant a marker that
  reads `probe--end` in the output and `probe-$VAR-end` in the echoed command. An absence test whose
  sentinel appears in its own input cannot fail, and an assertion that cannot fail is worse than none
  because it is counted as coverage.
- **Time invested:** human ~0.1h, agent ~1.4h (wall clock), cost unknown — source: **manual**, the
  sixth consecutive entry, for the same two reasons (#337 capture, #353 attribution). Six in a row is
  no longer an anecdote about two defects; it is the state of the measurement DEC-068 rests on.
- **ADR:** none. ADR-0029 already governs this change and was authored by #357. The absence-test point
  is worth recording but is its first occurrence in that form; the sync-step gap for a bundle-less
  Product change is noted on #360's neighbourhood rather than filed separately, since it is the same
  route under examination there.

## 2026-08-13 — terminal-pump-off-the-thread-pool (#330, PR #362)

**Route: `/aio:ship 330`, unattended (DEC-068, ADR-0027), on a blanket approval to run the waves. No
human read this diff before merge. The three reflection points below are UNCONFIRMED.**

- **Worked:** **The filed fix was wrong, and measuring it is what found that.** This issue proposed
  `TaskCreationOptions.LongRunning`, and #327 had already tried exactly that — seen it pass locally three
  times, change nothing in CI, and reverted it. Rather than repeat the experiment, a probe recorded
  `Thread.CurrentThread.IsThreadPoolThread` either side of a suspending await: **false** before, **true**
  after. `StartNew(Func<Task>, …, LongRunning)` dedicates a thread only until the first suspending await,
  then the delegate returns its `Task`, the thread exits, and every continuation — including every
  subsequent blocking `Read` — resumes on a pool worker. A pump is `Read` → send → `Read`, so the
  proposed fix would have moved exactly one read off the pool. That explains #327's result, and it means
  the issue as filed would have shipped a change that satisfied neither of its own first two criteria.
- **Didn't:** **Criterion 3 was an invitation to repeat #327's mistake, in the same file, in the same
  month.** It asked for the absence of contention to be *"demonstrated rather than asserted"*, which
  reads as: open N terminals, race unrelated work, measure. That is precisely the shape #327 removed for
  being unrunnable on a two-core runner behind a full suite — and #329 exists to put that coverage back
  in a form CI can run. An acceptance criterion phrased as *demonstrate* rather than *assert* pushed
  toward a benchmark without anyone intending it. It was met with a deterministic thread property
  instead, on the ground that a pump which is not on a pool thread cannot contend for one.
- **Next time:** When a criterion says **"demonstrated rather than asserted"**, ask what it forbids
  rather than what it invites. The intent was "do not just claim it" — but the literal reading is "write
  a measurement", and measurements of contention are the flakiest tests there are. The grill should
  translate such phrasing into the deterministic property that carries the same claim, at the point the
  criterion is written rather than when it is implemented.
- **Time invested:** human ~0.05h, agent ~0.7h (wall clock), cost unknown — source: **manual**, the
  seventh consecutive entry (#337 capture, #353 attribution).
- **ADR:** none. The sync-over-async choice is argued in the code where it is made, and is a scheduling
  decision inside one hub rather than an architectural one. The criterion-phrasing point is its first
  occurrence in that form.

## 2026-08-13 — terminal-output-covered-again (#329, PR #363)

**Route: `/aio:ship 329`, unattended (DEC-068, ADR-0027), on a blanket approval to run the waves. No
human read this diff before merge. The three reflection points below are UNCONFIRMED.**

- **Worked:** **The hard part was removed by a change nobody planned it for.** #329 had been open since
  #327 because the test needed a deterministic transport, and making SignalR's long-polling deterministic
  under `TestServer` is genuinely difficult. Then #330 turned the pump into a **synchronous** loop for an
  entirely unrelated reason — thread-pool occupancy — and the test stopped needing a transport at all: it
  calls `Pump` directly and the call *returns* when the terminal ends. Three tests in ~12ms against a
  ten-second budget, with no wall-clock number in the file. Sequencing #330 first was chosen for
  criterion 2's "one helper" argument, and the payoff turned out to be somewhere else entirely.
- **Didn't:** **Criterion 2 is not met yet, and ticking it would have been the easy lie.** It asks that
  the test pass on a CI runner *"demonstrated by consecutive green runs, not by one"*. There has been
  **one**. The merge produces a second and the next pull request a third, so the criterion accrues after
  this change rather than within it — which means the box is ticked on evidence that does not exist yet.
  Recorded here instead of quietly counting the one run as satisfaction. The honest reading is that #329
  is *implemented* now and *demonstrated* in two more runs' time.
- **Next time:** An acceptance criterion whose evidence can only accumulate **after** the change merges
  cannot be satisfied by the change that carries it. Either the grill rewrites it to something the change
  can show (this test passes in CI, and here is the run), or the issue is explicitly left open pending
  observation. Silently treating "it passed once" as "consecutive green runs" is how a criterion stops
  meaning anything — and this repository already has an ADR about measurements licensing only what they
  measured (ADR-0018).
- **Time invested:** human ~0.05h, agent ~0.5h (wall clock), cost unknown — source: **manual**, the
  eighth consecutive entry (#337 capture, #353 attribution).
- **ADR:** none. The criterion-accrual point is its first occurrence in that form; it is close kin to
  #330's "demonstrated rather than asserted" finding, and if a third appears the family is worth an ADR
  about how acceptance criteria are phrased.
