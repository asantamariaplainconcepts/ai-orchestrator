# Design: sync-action

## D1 — The product knows *that* a change closes, never *how*

This repository closes a change with a retro entry, a spec sync, an archive move and a
squash-merge with a linted message. None of that is a property of software development; it is a
property of *this project*. An action that hardcoded it would be one team's process smuggled into
everyone else's tool — the failure DEC-048 avoided when the grill read the project's own
definition of ready instead of imposing one.

So the action reads a document from the connected repository and follows it. The default path is
the framework's convention, because a project that adopted this framework has that document; any
other project points the setting at its own. The agent is told to follow it exactly and to refuse
rather than improvise, which is the grill's contract too.

## D2 — It closes a change that exists, found where propose already looks

The Story's open change is discoverable through the workspace seam propose already uses to
enforce one-open-change-per-Story. Sync asks the same question and gets the same answer, so the
two cannot disagree about what "the Story's change" means.

No open change is a **refusal**, not an attempt to find one: an agent that went looking would
eventually close somebody else's work, and the whole value of this step is that it is the one
place the product touches a merge.

## D3 — Gated by default, because the irreversible step should ask

Merging is the least reversible thing in the pipeline. DEC-040 exists for exactly this, and the
seeded default therefore carries `requiresApproval`. An Admin who wants an autonomous close
clears it deliberately — the same gesture #116's canvas made visible — rather than discovering
afterwards that the product merged something unattended.

## D4 — Refusals before the workspace, every one

Cloning a repository and starting an agent costs money and time. Every condition this action can
check first — no open change, no process document, an unreadable one — is checked before either.
That ordering is propose's (#80) and it is worth repeating because the alternative is a Run that
spends its budget to discover something the caller already knew.

## D5 — The pull request is left exactly as it was on failure

A half-closed change is worse than an open one: the next person cannot tell whether to finish it
or start again. So a failing sync Run records why and touches nothing — no partial merge, no
branch deleted, no comment claiming completion. The agent is told this too, because the runtime
is what actually holds the tools.
