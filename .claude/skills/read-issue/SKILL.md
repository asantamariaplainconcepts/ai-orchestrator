---
name: read-issue
description: Read a GitHub issue's title, body, labels, and status via gh. Use when a command needs the current state of an issue.
---

Fetch one issue's state — one responsibility, read-only.

## Steps

1. **Fetch.** Run `gh issue view <number> --json number,title,body,labels,state,url`.
   - Done when: the JSON is retrieved.
2. **Extract.** Parse the current `status:*` label, **the hold**, the change/spec-ID field from the body, and the acceptance criteria.
   - The hold's name is the value of `holdLabel` in `.claude/workflow.json` — read it from there, never hardcode it. Compare case-insensitively, the way the vendor compares labels, so a differently-cased spelling is still the same hold.
   - The hold is **not** a `status:*` label. A held issue still carries exactly one of the nine; report both facts independently.
   - Done when: status, hold, change/spec-ID, and acceptance criteria are identified (or reported absent).
3. **Return.** Hand the structured result back to the orchestrating command. Report the hold as **present or absent** — never omit the field, so a command can never mistake silence for "not held".
   - Done when: the command has what it needs to gate on status and on the hold.

## Do not

- Change any GitHub state (labels, body, comments) — including the hold. Reading is read-only: this skill neither applies nor removes it.
- Infer a status that isn't present — report it missing so the command can route to `/aio:grill`.
- Refuse anything. What a hold means is each command's decision, not this skill's: `/aio:status` and `/aio:grill` legitimately read a held issue and carry on.
