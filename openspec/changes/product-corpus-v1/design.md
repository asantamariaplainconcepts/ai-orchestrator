# Design: product-corpus-v1

## Context

The corpus at `docs/product/mvp/` was written for a product that no longer exists as described:
DEC-001's "internal web app" predates DEC-049 (open source, self-hostable), DEC-013's
supersession (#296, microVM sandboxes replaced the pod/queue substrate), and DEC-065 (the
self-host habitat's attach affordances). The draft replacement corpus already exists at
`docs/product/v1/` (nine files, written 2026-08-11 from the audit in this change's issue),
alongside the Orca study that sources its intended capabilities. 58 files in the repo reference
`product/mvp`; most are historical records that must not change.

## Goals / Non-Goals

**Goals**

- v1 is the corpus every live document and ceremony points at; mvp/ is legible history.
- The identity change is a formal, numbered decision with an ADR — not a drive-by doc edit.
- Stable-ID continuity is verifiable: same IDs, same concepts, one named correction (UC-028).

**Non-Goals**

- No renumbering beyond UC-024→UC-028, no moving the decision log, no editing ADRs/bootstrap/
  archived changes, no code, no grilling of UC-030..032 (each owes its own grill).

## Decisions

1. **The decision log stays in `mvp/`, append-only.** The new DEC lands in
   `docs/product/mvp/10-locked-mvp-decisions.md` like every DEC before it, and v1 documents
   link into it. Rejected: moving the log to v1 (breaks ~40 historical links for zero product
   value) and starting a second log in v1 (two logs, one numbering — a collision factory).
2. **One DEC + one ADR, not two DECs.** The identity revision and the corpus adoption are one
   cohesive decision — the corpus was rewritten *because* the identity changed. The DEC number
   is allocated against origin/main at implementation time per the decision-records spec
   (expected DEC-066; the number is not claimed in advance). Rejected: separate DECs for
   identity and location — the second is meaningless without the first.
3. **Live docs repoint; history does not.** "Live" = README.md, AGENTS.md, ARCHITECTURE.md,
   ONBOARDING.md, CONTRIBUTING.md, docs/process/*, openspec/config.yaml (its project-context
   still says "internal web application… KEDA-scaled ACA Jobs", which DEC-013's supersession
   retired — this change makes the context tell the truth the specs already tell). "History" =
   docs/adr/*, BOOTSTRAP*, docs/process/retro-log.md, openspec/changes/archive/* — byte-identical.
   The `run-orchestration` spec's reference to `mvp/05-business-rules.md` (line ~921) records
   what a past change did and stays.
4. **UC-028 resolves the UC-024 collision.** The grill capability takes UC-028 (never used;
   the old corpus skips from UC-027 to UC-029). Verified at grill time: no spec cites UC-024
   in the grill sense, and closed issues (#79) are read against the corpus of their day.
   Rejected: renumbering the backlog-section UC-024 instead — it is cited by the run-preview
   surface's history and is the older assignment in spirit (BC-002's read family).
5. **`mvp/` gets exactly one edit**: a supersession note atop `00-product-brief.md` pointing at
   v1 — a reader landing there must learn where the living corpus went without following a
   broken trail. Rejected: zero edits (no signpost) and per-file banners (noise across a
   record that should read as it was).

No backend/frontend architectural conventions are touched (docs + two doc-facing specs only).
No infrastructure claims enter this design: the only "state" asserted — that specs, ADRs and
issues reference the paths named above — was verified by grep during the grill, in this
worktree.

## Risks / Trade-offs

- **Two corpora coexist** → mitigated by the supersession note, the v1 README's authority
  section, and repointing every live entry point in one change.
- **Stale-link regressions** (a live doc still pointing at mvp/ semantics) → the tasks include
  a verification sweep: after the cutover, `grep -rn "product/mvp"` outside
  `docs/product/mvp/`, `docs/adr/`, `BOOTSTRAP*`, retro log and archives must return only the
  deliberate references this design names.
- **The openspec context edit changes what future proposals are told** → intended; the context
  must describe the sandbox substrate and dual-habitat identity, or every future proposal
  starts from a false premise.

## Migration Plan

Single change, no phases: add v1 + study, append DEC + ADR, repoint live docs, add the
supersession note, update the two spec deltas, run the verification sweep. Rollback is
`git revert` of one squash commit — nothing outside the repo changes.

## Open Questions

None blocking. OPN-006 is untouched by design.
