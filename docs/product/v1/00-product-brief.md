# Product brief — AI Orchestrator

**One line.** An open-source web application that connects project backlogs (GitHub, Azure
DevOps) to AI agents: users configure *Automations* that run an Agent in a **sandbox of its
own** — a microVM created for one Run and gone with it — to act on user stories: implementing
them as PRs, refining them, transitioning or estimating them — with every run visible and
governable from the website.

## One product, two habitats

The same loop runs wherever its operator lives, and the habitat is a product concept, not a
deployment detail ([DEC-049](../mvp/10-locked-mvp-decisions.md),
[DEC-065](../mvp/10-locked-mvp-decisions.md)):

- **Deployment** — a team's governed instance on metered infrastructure (Azure today).
  Approval gates, caps, per-run cost; a live conversation costs a pass per message
  ([DEC-055](../mvp/10-locked-mvp-decisions.md)); nobody attaches to an agent's session.
- **Self-host** — a machine its operator owns, needing only Docker and git. The same loop and
  the same rules, plus what owning the hardware licenses: a local folder as code source
  ([BR-016](05-business-rules.md)), attaching to a Run's sandbox or its agent's session
  ([DEC-065](../mvp/10-locked-mvp-decisions.md)), a terminal on the machine's sandboxes
  ([UC-029](04-capabilities.md)).

A capability may lawfully differ per habitat — but only where a decision names the difference,
and never in what a Run records: different affordances, one audit trail
([BR-014](05-business-rules.md)).

## Proven claim

The claim the MVP existed to prove ([DEC-002](../mvp/10-locked-mvp-decisions.md)) — website →
real backlog → labeled story → Agent Run in a sandbox → configured action → result visible
back in the website — is exercised end to end by the seeded demo loop, and its shape survived
the substrate change ([DEC-013](../mvp/10-locked-mvp-decisions.md) superseded, #296).

## Business goals

1. Make AI-agent work on backlogs **configurable by non-operators**: a team lead wires a
   project in the UI, no pipeline editing.
2. Make it **governable**: plan-review approval gate ([DEC-040](../mvp/10-locked-mvp-decisions.md)),
   per-project caps, cancellation, per-run cost — and every wait on a human visible in one
   place ([UC-026](04-capabilities.md)).
3. Make it **vendor- and runtime-neutral**: Connector seam (GitHub + Azure DevOps) and
   pluggable Agent runtimes (Claude Code headless, opencode) — adding a third of either must
   be trivial.
4. Make it **runnable by anyone** ([DEC-049](../mvp/10-locked-mvp-decisions.md)):
   self-hostability is a product goal, and every infrastructure choice is evaluated against
   "can a stranger with Docker still run it?".

## Target users

Two personas, one loop. **Governed teams**: project admins who configure, team members who
trigger and observe — Plain Concepts delivery teams are the first of these, not the
definition. **The self-hosting developer**: one person, their own machine, their own backlog —
the persona [DEC-065](../mvp/10-locked-mvp-decisions.md) and [UC-029](04-capabilities.md)
serve. See [actors](01-actors-and-responsibilities.md).

## What it is not

- **Not an interactive agent cockpit** — no IDE-like surface where a human pilots agents live;
  the unit of interaction is a governed Run, and a human joins one only where a decision
  permits it (see the [Orca study](../studies/2026-08-11-orca.md)).
- **Not a PR dashboard** — output review happens in the vendor's own PR surface (see the
  [pr-dashboard study](../studies/2026-08-03-pr-dashboard.md)).
- **Not a CI system** — the unit is a Run against a Story, never a pipeline against a commit.
- **No notifications yet** — the website is the only surface
  ([DEC-037](../mvp/10-locked-mvp-decisions.md)); revisiting that is a decision, not a feature
  slipped in.
- **Custom roles not yet** — the permission model underneath exists from day 0
  ([DEC-034](../mvp/10-locked-mvp-decisions.md)).
