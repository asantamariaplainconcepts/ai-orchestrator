# Product brief — AI Orchestrator

**One line.** An internal web application that connects project backlogs (GitHub, Azure
DevOps) to AI agents: users configure *Automations* that fire KEDA-scaled Agent jobs to
act on user stories — implementing them as PRs, refining them, transitioning or
estimating them — with every run visible and governable from the website.

## MVP objective

Prove exactly one claim ([DEC-002](10-locked-mvp-decisions.md)):

> From the website, a user connects a project to a real backlog, labels one user story,
> and an AI job spins up via KEDA and performs the configured action on it — with the
> result visible back in the website.

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
