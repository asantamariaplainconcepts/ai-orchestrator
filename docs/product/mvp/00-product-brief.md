# Product brief — AI Orchestrator

> **Superseded.** The living corpus is [docs/product/v1/](../v1/README.md) (DEC-066,
> [ADR-0024](../../adr/0024-the-product-says-what-it-is-an-open-source-dual-habitat-orchestrator.md),
> 2026-08-11). This folder is the historical record; only the decision log
> ([10-locked-mvp-decisions.md](10-locked-mvp-decisions.md)) and open decisions
> ([07-open-decisions.md](07-open-decisions.md)) remain live here, append-only.

**One line.** An internal web application that connects project backlogs (GitHub, Azure
DevOps) to AI agents: users configure *Automations* that run an Agent in a sandbox of its own
to act on user stories — implementing them as PRs, refining them, transitioning or
estimating them — with every run visible and governable from the website.

## MVP objective

Prove exactly one claim ([DEC-002](10-locked-mvp-decisions.md)):

> From the website, a user connects a project to a real backlog, labels one user story,
> and an Agent starts in a sandbox of its own and performs the configured action on it — with
> the result visible back in the website.

## Business goals

1. Make AI-agent work on backlogs **configurable by non-operators**: a team lead wires a
   project in the UI, no pipeline editing.
2. Make it **governable**: plan-review approval gate ([DEC-040](10-locked-mvp-decisions.md)),
   per-project caps, cancellation, per-run cost.
3. Make it **vendor- and runtime-neutral**: Connector seam (GitHub + Azure DevOps) and
   pluggable Agent runtimes (Claude Code headless, opencode) — adding a third of either
   must be trivial.

## Target users

Plain Concepts delivery teams (internal tool): project admins who configure, team
members who trigger and observe. See [actors](01-actors-and-responsibilities.md).

## What the MVP is not

No notifications (email/Teams) — the website is the only surface ([DEC-037](10-locked-mvp-decisions.md)).
Custom role editing is post-MVP (the permission model underneath exists from day 0,
[DEC-034](10-locked-mvp-decisions.md)). Foundation-vs-product sequencing:
[09](09-foundation-vs-product-split.md).
