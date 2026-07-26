# Design — agent-implements-pr

## D1 — The ceremony is code; the Agent is only the implementation

Clone, branch, commit, push and PR-open are deterministic and testable; prompting an agent to
perform them makes four reliable steps as flaky as the least reliable one. `ICodeWorkspace`
carries them: `Prepare(coordinates, runId, token)` → workspace; `Publish(workspace, runId,
title, body, token)` → PR URL or a stage-named refusal. The CLI runtime keeps doing exactly
one thing — running the Agent in a directory.

## D2 — Git via the CLI, the PR via Octokit, credentials in memory only

The image already carries git (#18). Clone/push use an in-memory credential URL per
invocation — never written to remote config that survives the job (BR-010); the PR is opened
through Octokit (already the vendor SDK of record) confined to the implementation file. gh CLI
would add a second authenticated tool for one call.

## D3 — "No changes" is a Failed Run, not an empty PR

`git status --porcelain` deciding emptiness is the honesty gate: an empty PR pretends work
happened. The Run fails with exactly that reason, and BR-004 hands the retry to a human.

## D4 — Stage-distinct failures

Prepare, agent, publish — each failure records which stage refused and why (clone auth vs
agent error vs push rejection vs PR API refusal). One generic message would collapse four
different fixes into an hour of guessing; the Backlog error taxonomy set this precedent.

## D5 — OutputLink completes the em-dash contract

run-visibility D2 shipped the Output column as an empty value so this change would be a data
change, not a UI reshape. `OutputLink` lands on the Run, in ListRuns (the exact-shape test
updates deliberately — the field now has a producer), and the column renders a link.
