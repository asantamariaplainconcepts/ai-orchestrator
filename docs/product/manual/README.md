# Using AI Orchestrator

The screens, in the order a project meets them. Each section says what the surface is _for_ —
what it shows is visible in the picture.

This complements [`docs/product/v1/`](../v1/00-product-brief.md), which defines what the product
must do in stable IDs. This one is the tour.

> Screenshots come from the mock preview, not a real tenant, so no project, repository or credential
> here belongs to anybody. Refresh them with `scripts/capture-manual-screenshots.sh` after changing a
> surface — a manual that drifts from the product is worse than none.

---

## 1. Projects

![The projects list](img/01-projects.png)

Everything is scoped to a **Project**: a backlog, its Automations, its Runs, its credentials. A
project is a name and nothing else at first — the connection comes next.

The environment chip at the sidebar's foot is the deployment telling you what it is. Where no
sign-in is configured it says so plainly rather than pretending: _this machine, owner, no
sign-in_ — and its popover carries the identity, the address, the Agent runtimes' readiness and the
network warning. Reach the page from another machine and a red banner appears instead: on an
exposed port with no sign-in, anyone who connects is the administrator.

---

## 2. Settings — connect a backlog

![Connecting a backlog](img/06-settings.png)

A **Connector** points the project at a repository and names the credential to reach it with. The
credential is named, never pasted: what the product stores is the _name_ of a secret, and the value
lives in the vault the deployment provides (BR-010).

Nothing else works until this does — the backlog is the source of truth for stories, and every Run
clones the repository it names.

---

## 3. Automations — decide what happens, and when

![The Automations tab](img/04-automations.png)

An **Automation** is a rule: _when a story is labelled `ai:grill`, run this prompt on this runtime_.
What it does is decided by a prompt file in your own repository, read live at Run time — not by
anything chosen in this product.

Three things share this tab.

**Set up the whole workflow** looks for the prompts your repository already has and shows a plan of
what it would wire before you press anything. Every row is a checkbox, so you can leave a step out
rather than delete its Automation afterwards.

To install a workflow you do not have yet, turn it on first. The switch names what the workflow needs
and lists every path it would write — its prompts, and the documents those prompts read, which is why
some of them sit outside your prompt directory. Nothing is on by default: a workflow is a way of
working, and the product will not choose one for you.

**Anything you already have wins.** A path that exists in your repository is not written, not
modified, and does not appear in the pull request — so a team with its own readiness document keeps
it, and the report afterwards tells you which files were written and which were left alone.

Everything arrives together, in one branch and one draft pull request: a workflow whose prompts and
whose documents landed as two reviews could be merged half-way. Nothing lands on your default branch.

**The catalogue** is every Automation the project has. **The workflow** is the subset that hands work
to one another — one Automation writes a label, the next one's trigger matches it, and a chain
appears. Where nobody hands on, a person must, and you can drag a human review into the flow.

**New Automation** asks three questions in the order the Automation itself runs: when it fires, what
it does, and what happens after. A sentence at the top restates your answers in prose, so a mistake
is visible before you save rather than after the first Run.

---

## 4. Operate — the backlog and its pulse

![The Operate tab](img/02-operate.png)

The project's stories, mirrored from the vendor and read-only here (BR-008). Applying a trigger label
starts a Run; **Run now** starts one without touching the story's labels.

Above them: what is waiting on a human, what the Runs have cost, how long they queue, and which
Automations have actually fired. An Automation that has never fired says so — a rule nobody triggers
is a rule worth deleting.

A story can hold **one active Run at a time** (BR-001), and a project has a cap on how many run at
once (BR-002).

---

## 5. Runs — what an agent did

![The Runs list](img/03-runs.png)

Every Run, with its state, its cost and its output. A Run that requires approval stops after
planning and waits: the plan is readable, and nothing executes until somebody approves it.

Cost is honest about what it does not know — a pass whose usage the runtime never reported reads
**unknown**, never zero (BR-011).

Runs are never retried automatically (BR-004). A failure is terminal and a human re-triggers it,
because an agent that quietly runs again is an agent spending money nobody authorised.

---

## 6. Inbox — everything waiting on a person

![The Inbox](img/07-inbox.png)

One list, across every project, of the things that cannot proceed without somebody: a plan awaiting
approval, a failure nobody has re-triggered.

Entries leave when the human has acted — including the derived case, where a failure's story already
has a newer Run. An inbox that only grows stops being read.

---

## 7. Ask — talk to an agent about the project

![The Ask tab](img/05-ask.png)

A conversation with an agent that has the project's repository cloned, about the project or about one
of its stories. Useful for _why did this fail_ and _what would you do here_.

It is **not a Run**: it occupies no cap slot, locks no story, and blocks nothing. An Automation whose
trigger is applied to a story with an open conversation fires exactly as it would otherwise. Each
message costs one agent pass, and the conversation says what it has cost.

---

## 8. Running it locally

Two things in every screenshot above belong to the **local habitat**, and they are worth naming.

The environment chip — _this machine, owner · no sign-in_, in the sidebar's foot — is the
deployment telling you what it is. Where no sign-in is configured the product says so rather
than pretending there is security it does not have; the chip's popover adds the address, the
Agent runtimes' readiness and the network warning, and links to the **Agent runtimes** panel —
whether each Runtime's CLI answers on the machine that executes Runs, and what to do when it does
not. The projects list's **Local** badge on
_Alpha portal_ is the other: that project's code does not come from a clone.

### A folder instead of a clone

A project can point at a directory on this machine rather than a repository the product clones. The
backlog still comes from the vendor — stories, labels, comments — but the code an agent works in is a
checkout you already have, with your branches and your uncommitted work.

The control is on **Settings → Edit Connector**, under the advanced disclosure. For a project that is
already local the disclosure is locked open: the folder is not an advanced detail once it is the
answer, and hiding it would hide the only place the path can be corrected.

Pointing at a folder asks the vendor for **less**: reading issues and contents plus writing issues,
without the write access that opening a pull request needs. The permissions panel asks for the shape
you are actually configuring rather than the widest one.

### Where each Run executed

Every Run records where it ran, and says so with one vocabulary wherever it appears — a monitor for
_this machine_, a container for _a sandbox_, always beside the word and never colour alone.

It matters because the two are not equivalent. A Run on this machine works in your checkout and can
see work you have not pushed; a Run in a sandbox works in a fresh clone and cannot. When a result
surprises you, this is the first thing to check.

### What the habitat is

One process, one database, no queue container — the dispatch queue lives in the outbox the database
already has, so a Run survives the process dying and is redelivered on restart. The default agent
runtime is a free model, so trying the whole loop costs nothing and needs no AI key. The only
credential anywhere is your own vendor token.

The trade, stated because it is real: with one process, the portal resolves project credentials and
clones repositories itself. That is correct on a machine one person owns and wrong anywhere shared —
which is why a deployment with a session host does it on the other side of that boundary instead.

See [SELF-HOSTING.md](../../../SELF-HOSTING.md) for getting it running.

> Two local surfaces have no picture here, deliberately. The code-source control needs a click the
> screenshot CLI cannot perform, and a Run's detail page has no stable URL in the mock — its run ids
> are generated per page load. Prose rather than a screenshot captured under different conditions and
> quietly inconsistent with the rest.

---

## Where to go next

| You want                                | Go to                                                                                            |
| --------------------------------------- | ------------------------------------------------------------------------------------------------ |
| What the product must do, in stable IDs | [docs/product/v1/](../v1/00-product-brief.md)                                                    |
| How the code is arranged and why        | [ARCHITECTURE.md](../../../ARCHITECTURE.md)                                                      |
| To run the whole thing yourself         | [SELF-HOSTING.md](../../../SELF-HOSTING.md)                                                      |
| What the local habitat trades away      | [ADR-0010](../../adr/0010-a-habitat-contract-is-asked-never-inferred.md) — asked, never inferred |
