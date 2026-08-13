# Design — whether a terminal may open outside a sandbox

## The question, precisely

Not *"should the local habitat have a terminal"* — everyone wants that. The question is **what
`run.attach` grants once the microVM is not underneath it**.

Today the grant is bounded by construction. A caller holding `run.attach` gets a shell inside a per-Run
sbx microVM: a disposable machine, with its own filesystem, that dies with the Run. Nothing they type
reaches the operator's disk, their SSH keys, or the server process's environment.

Remove the sandbox and the same grant, unchanged in name and unchanged in the authorization code, hands
that caller a shell **on the operator's machine, as the user the server runs as**. That is not a
degradation of an existing permission; it is a different permission wearing its name.

## Why DEC-065 does not already answer it

DEC-065 permits an attached session in self-host, on the ground that *"a machine its operator owns is
not one somebody else pays for or administers"*. The temptation is to read that as settling this.

It does not, and the reason is in ADR-0021's own framing: it was decided about **a session inside a
Run's sandbox**, and its companion requirement is *"a human attached to a sandbox does not extend its
life"* — a sentence that presumes a sandbox exists. Every terminal shipped under it (#304, #311) opens
inside a microVM. The precedent covers *a human may take the keyboard*; it does not cover *and the
keyboard is attached to the host*.

Treating an unexamined precedent as coverage is the failure this repository has already recorded
twice this month, in different clothes.

## The options

### (a) Status quo — terminals require a sandbox

The local habitat keeps no terminal. Honest and zero-risk: the terminal becomes an argument for
installing sbx rather than a property of the product.

**Cost:** the stated product direction — a local loop inspectable with the tools a developer already
has — stays impossible, and ADR-0021's permission continues to describe a habitat that cannot use it.
The gap is not a bug to be fixed later; it is the answer.

### (b) An unbounded host terminal

A plain login shell on the machine, in the Run's checkout or anywhere else.

**Cost:** the largest change to what the grant means, and the one hardest to describe honestly in the
authorization surface. `run.attach` would grant, to anyone the portal authorises, arbitrary command
execution as the server's user — including reading the operator's credentials, the local secret store
(`Secrets__LocalStorePath`) and their SSH keys. In self-host with `Identity__Mode=LocalOwner` the
holder *is* the operator today, so nothing is lost **now**; the risk is that the grant outlives that
assumption, which is precisely what a locked decision is supposed to prevent.

### (c) A bounded host terminal — **chosen**

A shell on the host, with the bounds stated as requirements rather than intentions:

1. **Working directory** is the Run's own checkout (`aio-checkout-*`), which #331/#332 already create
   outside the operator's folder and reap. Not the operator's repository, not their home directory.
2. **Environment is not inherited from the server process.** This is the sharp edge and the reason
   the option needs writing down: `InteractivePty` uses `posix_spawn`, which takes the child's whole
   environment, and the existing implementation deliberately **inherits and overlays** because the sbx
   CLI needed `$HOME`. Inside a sandbox that inheritance was harmless — nothing crossed the boundary.
   On the host there is no boundary, so the same code would hand a shell the server's environment,
   including whatever the habitat resolved into it. The child gets a named, minimal environment.
3. **A named shell**, not the operator's login shell with their profile, so what starts is a property
   of the product rather than of whatever is in `~/.zshrc`.
4. **Self-host only.** ADR-0021's deployed refusal is unchanged, and stays *not available in this
   habitat* — distinct from *not permitted for you*.

**Cost, stated:** the bound is a **product** boundary, not a kernel one. A shell in the Run's checkout
can still `cd /`. This buys a sane default and an honest description; it does not buy isolation, and
the DEC must not imply otherwise. Anyone who wants isolation runs the sbx launcher, which remains
available and unchanged.

### (d) Neither — make a sandbox cheap instead

Keep the boundary and remove the reason not to have one, via the sandbox-per-thing work in #313/#314.

**Cost:** it answers a different question. #313/#314 are about *which things get sandboxes*; this is
about *what happens where there is none*. It also runs against the direction that prompted this — a
local loop that does not require a sandbox runtime at all — and inherits sbx's own costs: 4 GiB per
microVM, a measured leak of 31 sandboxes and 125 GB, and a substrate unverified on Linux.

## Why (c)

(a) refuses the direction outright. (b) changes the grant most and describes it least. (d) answers a
different question and carries the cost the direction exists to avoid.

(c) keeps the grant's *meaning* closest to what it meant with the microVM — a shell scoped to this
Run's work — while being honest that the scoping is a default and not a jail. It is also the only
option under which the surface can say something true and useful about what the terminal reaches.

## What must change in the names

`IRunTerminalHost` is fine; its vocabulary is not. `LocalSandbox(Name, Status, RunId, Workspace)` is
returned by `List` to describe *a thing a terminal can open on*, and after this decision that is
sometimes a checkout. `MachineSandboxAccess` and `ListMachineSandboxes` read the same way.

The decision requires the rename rather than leaving it to be discovered: a type called `LocalSandbox`
holding a checkout path is the kind of small lie that a later reader takes literally. The minimal
change is `LocalSandbox` → a name that says *where a terminal can open* and carries what it is; the
mechanical rename belongs to #358.

## What the record must say

`IRunAttachRecorder` already records who attached. After this decision it must also distinguish
**what they attached to** — a sandbox or the host — because a log that reads identically for both
would let a reader assume a bound that was not there. This borrows the shape `CredentialSource`
already uses on the agent's side, *"so the source is never left to inference"*, rather than inventing a
second mechanism.
