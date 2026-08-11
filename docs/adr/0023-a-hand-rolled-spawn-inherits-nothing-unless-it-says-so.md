# ADR-0023: A hand-rolled spawn inherits nothing unless it says so

- **Status:** Accepted
- **Date:** 2026-08-11
- **Deciders:** Repository owner (solo path, DEC-016)
- **Tags:** backend, agents, reliability

## Context

`InteractivePty` starts a child process through P/Invoked `posix_spawn` rather than
`System.Diagnostics.Process`, and it must: a pseudo-terminal needs the child's stdio bound to a pty
slave file descriptor, which `ProcessStartInfo` cannot express. Every other process this codebase
starts goes through `HeadlessProcess`, which uses `ProcessStartInfo` with `UseShellExecute = false`.

The two are not equivalent, and the differences are silent. `Process.Start` applies conveniences that
the raw syscall does not, and each one that went unreplicated has cost exactly one bug on this same
seam:

- **#304 — the environment.** The first pseudo-terminal passed only the caller's additions to
  `posix_spawn`, which *replaces* the child's environment where `Process.Start` *inherits* and
  overlays. The sbx CLI died with `panic: $HOME is not defined` before any sandbox was touched.
- **#311 — `PATH` resolution.** `posix_spawn` requires a resolved path and answers ENOENT for a bare
  command name; `posix_spawnp` is the variant that searches `PATH`. The default `CommandPath` is the
  bare name `sbx`, so on a machine where sbx is installed outside the default prefix the sandbox
  *listing* worked — it goes through `HeadlessProcess` — while opening a terminal failed with
  `rc 2`. Two ways of starting the same binary that disagreed about how to find it.

Both were found by running the product, not by reading it or by a unit test: a stand-in binary
invoked with an absolute path and a full environment reproduces neither. This is the second
occurrence of the same underlying cause, which is this repository's graduation rule for recording a
decision rather than a note.

## Decision

We will treat a hand-rolled spawn as **inheriting nothing implicitly**, and require every convenience
`Process.Start` supplies to be either replicated deliberately or documented as deliberately absent.

Concretely, for `InteractivePty` and anything that follows it:

1. **Resolve like the rest of the codebase.** Use `posix_spawnp`, so a bare command name is found on
   `PATH` exactly as `HeadlessProcess` finds it. A configuration value that works for one process
   starter and not the other is a defect, not a caller's mistake.
2. **Inherit the environment, then overlay.** Build the child's environment from the current process
   and apply the caller's additions on top; never pass the additions alone.
3. **Say so in the failure.** A spawn failure names the variant used and what its error number means
   in that context — `rc 2` reads as "not found on `PATH`", not as a bare number.
4. **Prove it against the real binary.** Any new divergence from `Process.Start` is exercised by a
   gated real-CLI test (ADR-0020) or by driving the product, because that is what caught both of
   these and what a mock cannot catch by construction.

## Consequences

- **Positive:** the two known divergences are closed and named in one place, so the third is a
  question a reviewer can ask instead of a bug someone finds in production. Fixing `PATH` resolution
  also fixed #304's Run terminal on any machine where sbx is not at the default prefix — the bug was
  latent there, not new here.
- **Negative:** the list is not provably complete. `Process.Start` does more than these two things
  (working-directory semantics, signal disposition, close-on-exec handling), and this ADR records the
  differences that have bitten rather than an audit of the API. A third divergence is likely and will
  be found the same way.
- **Neutral:** `InteractivePty` stays hand-rolled. The pty requirement is real, so the answer is to
  make the divergence explicit, not to remove it. A future .NET that can bind a child's stdio to an
  arbitrary file descriptor would supersede this.

## Alternatives considered

- **Require an absolute `CommandPath` in configuration instead of resolving `PATH`** — rejected
  because it makes one setting mean two different things depending on which process starter reads it,
  and the failure lands on the operator as an unexplained ENOENT rather than on the code that caused
  it. The listing already accepted the bare name; making the terminal the odd one out is the drift,
  not the fix.
- **Route the terminal through `Process.Start` and drop the pty** — rejected because `sbx exec -it`
  refuses a plain pipe, which is the whole reason the pty exists (#304). Without a terminal there is
  no shell worth attaching to.
- **Audit the full `Process.Start` surface now and replicate all of it** — rejected as speculative:
  the two differences that mattered were both discovered by exercising a real CLI, and a
  from-documentation audit would spend effort on conveniences this seam may never need while still
  missing whatever the next real binary objects to.
- **Leave it in the retro log only** — rejected because this is the second occurrence on the same
  seam. The first was recorded as a retro note and the second happened anyway, which is precisely
  what the graduation rule exists to stop.

## References

- Related: [ADR-0020](0020-a-launcher-is-unverified-until-it-meets-its-real-cli.md) — a launcher is
  unverified until it meets its real CLI; both bugs here are instances of it.
- Related: [ADR-0021](0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md) — why
  a terminal exists in self-host at all.
- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — verify claims by exercising them,
  never by reading configuration. Both bugs were invisible to reading and visible immediately on being
  run.
- Related: [ADR-0006](0006-a-capability-is-not-added-until-a-user-can-reach-it.md) — a capability is
  not added until a user can reach it. #304 merged with the browser path unproven; reaching it is what
  exposed both faults.
- #304 — the environment-replacement bug, recorded in `docs/process/retro-log.md`.
- #311 / OpenSpec change `terminal-on-any-local-sandbox` — the `PATH` bug, and this ADR.
