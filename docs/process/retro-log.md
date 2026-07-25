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
- **ADR:** none yet. Two candidates are on their second occurrence and should graduate as soon as
  `docs/adr/` exists in Phase 3: *verify infrastructure claims by exercising them* (the endpoint
  and migration defects were both assumed-working), and *a test tier that provisions its own
  preconditions can hide their absence from the application* (the fixture's private migration).

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
