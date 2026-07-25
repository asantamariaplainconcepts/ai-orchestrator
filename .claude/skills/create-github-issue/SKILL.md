---
name: create-github-issue
description: Create a GitHub issue from a prepared Definition-of-Ready draft via gh. Use when a grilled draft is ready to become an issue.
---

Create one GitHub issue from a prepared draft — one responsibility. Do not grill (that's `grill-to-ready`) or transition status beyond the initial label.

## Steps

1. **Confirm.** Show the human the exact issue title, body, and labels you will create. Proceed only on confirmation — this mutates shared GitHub state.
   - Done when: the human approves the exact content.
2. **Create.** Run `gh issue create --title "…" --body "…" --label "…"`. Include the change/spec-ID field in the body (the telemetry correlation key).
   - Done when: `gh` returns the new issue URL.
3. **Report.** Return the issue number and URL for the orchestrating command to use.
   - Done when: number + URL handed back.

## Do not

- Set workflow status labels — that's `set-issue-status`.
- Create an issue whose body isn't Definition-of-Ready complete.
- Include secrets or personal data in the body.
