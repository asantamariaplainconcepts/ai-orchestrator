# Local code source — design

## Context

Execution today: `RunCreator` writes a Run, `IRunDispatcher` enqueues its id, the DispatchWorker
claims it and `RunExecutor` assembles everything in process — Story and Automation via Contracts,
credentials resolved by name at the last moment, a workspace from `GitCodeWorkspace` (fresh
`git clone --depth 1` with the token on the command line), and an `IAgentRuntime` subprocess. In
the self-host flavour (DEC-049) the server and the worker already run on the user's machine, so
"run against my folder" is not a routing question — the queue and worker stay exactly as they are.

## Goals / Non-Goals

**Goals:**

- A Connector can name a folder on the host as the project's code source; Stories still come
  from the vendor.
- Every Run records where it executed (locus), against what (folder), and what it produced
  (branch) — BR-014 stays a complete audit.
- Local execution is refused, not attempted, when its preconditions fail (dirty tree, missing
  folder, cloud posture).
- A Repository project's behaviour is byte-for-byte unchanged.

**Non-Goals:**

- No UI (issue #211). No Browse/file picker. No push or PR from a local run. No execution on the
  *browser's* machine against a remote deployment. No multi-worker locus routing.

## Decisions

- **D1 — locus is a workspace decision, not a dispatch decision.** The queue message stays
  `{runId}`; `RunExecutor` picks the workspace by `run.Locus` behind the existing `ICodeWorkspace`
  seam. Alternative — a second queue and a "local worker" — adds routing, scaling and idle-cost
  questions for zero benefit in the flavour where this feature exists at all, because the worker
  already lives on the host.
- **D2 — the posture switch is the LocalOwner switch.** The code-source surface (configure +
  validate) exists exactly where the LocalOwner identity flavour is composed, reusing the
  deployment's existing self-host discriminator rather than inventing a second flag two settings
  could disagree over. A cloud deployment 404s the surface — absent, not disabled.
- **D3 — refuse before any write (the BR-001 pattern).** The clean-tree check runs at dispatch
  (in `RunCreator`, where BR-001/BR-002 already refuse) and again in the workspace at execution
  (the check races with a human typing in that folder). The second failure ends the Run with the
  same sentence; the first never creates a Run at all. New rule recorded as **BR-016**.
- **D4 — branch, never push.** `LocalFolderWorkspace` switches to `ai/{storyId}-{slug}`, lets the
  runtime work, commits what changed, and restores the previous branch checkout on failure. The
  Run records `BranchName`; `OutputLink` stays null. Pushing from a host with the user's own
  credentials would act as the user without asking — the review surface is their editor
  (design mock 3c says exactly this).
- **D5 — credentials: skip resolution, say so.** Local runs use whatever the host CLI already
  holds; the secret-resolution step is skipped and one log line states it. Nothing lands at rest
  (DEC-052 unchanged). Alternative — resolving the vendor PAT anyway — would surprise: the folder
  may have a different remote than the Connector's coordinates.
- **D6 — recents are a query, not storage.** "Recent folders" (for #211) is the distinct
  `LocalPath` set across the caller's visible projects — permission-scoped by construction,
  nothing new persisted.

## Risks / Trade-offs

- [Path validation reads the host filesystem over HTTP] → gated twice: posture (D2) and project
  Admin; the endpoint answers four booleans/strings about one path and never lists directories.
- [The folder changes under a running Agent] → the execution-time re-check (D3) catches the
  entry race; mid-run interference is accepted and lands as an ordinary failed Run with the
  vendor-neutral log — the same exposure `aspire run` developers accept today.
- [`git switch` on the user's checkout mutates their HEAD] → the workspace restores the prior
  checkout on failure paths and leaves the new branch checked out on success (stated in the Run's
  Execution output — #211 renders it); documented in the BR-016 wording.
- [EF migrations on two modules in one change] → additive columns with defaults only; no
  backfill, no destructive step; rollback is dropping the columns.

## Migration Plan

Two additive migrations (Backlog: `CodeSource` + `LocalPath`; Runs: `Locus`, `WorkingFolder`,
`BranchName`) with `Repository`/`Pod` defaults. Deploy order irrelevant; rollback drops columns.

## Open Questions

None — the two the design review left (Browse picker, dirty-at-configure semantics) are resolved
as out of scope / warning-only in the elaborated spec (design project,
`design-system-proposals/local-code-source.md`).
