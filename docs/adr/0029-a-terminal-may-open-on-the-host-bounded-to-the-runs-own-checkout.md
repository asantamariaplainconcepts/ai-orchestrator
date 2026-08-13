# ADR-0029: A terminal may open on the host, bounded to the Run's own checkout

- **Status:** Accepted
- **Date:** 2026-08-13
- **Deciders:** ACT-001 Admin (the product authority), via an explicit blanket approval to run the
  waves unattended. **No human read this ADR before it merged** — see *Consequences*.
- **Tags:** security, self-host, terminals, authorization

## Context

A terminal is a property of the **sbx launcher**, not of locality.
`AgentSandboxComposition.AddAgentProcessHost` registers `IRunTerminalHost` and `RunSandboxHost` only
inside the sbx branch; the local branch registers `LocalAgentProcessHost` and returns at line 103,
before either. The local habitat therefore resolves `UnhostedRunTerminalHost` (`RunsModule.cs:131`),
whose `Hosted` is `false`.

The consequence is backwards: the one habitat [ADR-0021](0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
explicitly *permits* attaching in is the one habitat with no terminal. A developer running
`aspire run` without sbx installed can watch a Run's log and nothing else.

Closing that gap is not a configuration change. Today `run.attach` is bounded by construction: it
yields a shell inside a per-Run sbx microVM — a disposable machine that dies with the Run, whose
filesystem is not the operator's. Without the sandbox, the same grant, unchanged in name and unchanged
in the authorization code, yields a shell **on the operator's machine, as the user the server runs
as**. That is a different permission wearing the same name.

**DEC-065 does not already answer this.** It permits an attached session in self-host because *"a
machine its operator owns is not one somebody else pays for or administers"* — but it was decided
about a session **inside a Run's sandbox**, and its companion requirement, *"a human attached to a
sandbox does not extend its life"*, presumes one exists. Every terminal shipped under it (#304, #311)
opens inside a microVM. The precedent covers *a human may take the keyboard*; it does not cover *and
the keyboard is attached to the host*.

## Decision

**We will permit a terminal to open on the host in the self-host habitat, bounded to the Run's own
checkout.** A deployment continues to refuse it, unchanged.

The bounds are requirements, not intentions:

1. **The working directory is the Run's own checkout** (`aio-checkout-*`), which #331/#332 already
   create outside the operator's folder and reap — never the operator's repository or home directory.
2. **The child's environment is not inherited from the server process.** This is the sharp edge.
   `InteractivePty` uses `posix_spawn`, which takes the child's whole environment, and the existing
   implementation deliberately **inherits and overlays** because the sbx CLI needed `$HOME`. Inside a
   sandbox that was harmless — nothing crossed the boundary. On the host there is no boundary, so the
   same code would hand a shell whatever the habitat resolved into the server's environment. The child
   gets a named, minimal environment instead.
3. **A named shell**, not the operator's login shell with their profile, so what starts is a property
   of the product rather than of whatever is in `~/.zshrc`.
4. **Self-host only.** ADR-0021's deployed refusal stands, and stays *not available in this habitat* —
   distinct from *not permitted for you*.
5. **The audit record distinguishes what was attached to** — a sandbox or the host — borrowing the
   shape `IAgentProcessHost.CredentialSource` already uses, *"so the source is never left to
   inference"*.

**Rejected alternatives.** *(a) Status quo* refuses the product direction outright and leaves
ADR-0021's permission describing a habitat that cannot use it. *(b) An unbounded host terminal*
changes the grant most and describes it least: it would grant arbitrary command execution as the
server's user, including the local secret store (`Secrets__LocalStorePath`) and the operator's SSH
keys. In self-host with `Identity__Mode=LocalOwner` the holder *is* the operator today, so nothing is
lost now — the risk is the grant outliving that assumption, which is what a locked decision exists to
prevent. *(d) Make a sandbox cheap instead* answers a different question (#313/#314 are about which
things get sandboxes) and inherits sbx's costs: 4 GiB per microVM, a measured leak of 31 sandboxes and
125 GB, and a substrate unverified on Linux.

## Consequences

- **Positive:** the local habitat gains the terminal ADR-0021 already permits, with no sandbox runtime
  required. The grant keeps its *meaning* — a shell scoped to this Run's work. The surface can say
  something true about what the terminal reaches, which under (b) it could not.
- **Negative — stated plainly:** **the bound is a product boundary, not a kernel one.** A shell opened
  in the Run's checkout can still `cd /`. This buys a sane default and an honest description; it does
  **not** buy isolation, and nothing in the product may imply that it does. Anyone who wants isolation
  runs the sbx launcher, which remains available and unchanged. Second: two terminal hosts now exist
  permanently, because a deployment can never have this one.
- **Neutral:** the seam's vocabulary must change. `LocalSandbox(Name, Status, RunId, Workspace)`
  describes *a thing a terminal can open on*, and after this decision that is sometimes a checkout;
  `MachineSandboxAccess` and `ListMachineSandboxes` read the same way. The rename is required by this
  decision rather than left to be discovered halfway through the implementation (#358) — a type called
  `LocalSandbox` holding a checkout path is the kind of small lie a later reader takes literally.

**Decided unattended.** This ADR was written and merged by `/aio:ship` with no human reading it, on a
blanket approval to run the waves (DEC-068, [ADR-0027](0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md)).
The repository's own retro for #223 recorded that an issue whose deliverable is a *decision* should
arguably halt this route; that reservation was raised, the approval was given anyway, and this line
exists so the record is honest about how much review it had — **none**. A reader who disagrees with
option (c) should treat this as a proposal that shipped, not as a settled position defended by anyone.
