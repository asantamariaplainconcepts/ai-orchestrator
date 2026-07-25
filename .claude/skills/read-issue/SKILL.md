---
name: read-issue
description: Read a GitHub issue's title, body, labels, and status via gh. Use when a command needs the current state of an issue.
---

Fetch one issue's state — one responsibility, read-only.

## Steps

1. **Fetch.** Run `gh issue view <number> --json number,title,body,labels,state,url`.
   - Done when: the JSON is retrieved.
2. **Extract.** Parse the current `status:*` label, the change/spec-ID field from the body, and the acceptance criteria.
   - Done when: status, change/spec-ID, and acceptance criteria are identified (or reported absent).
3. **Return.** Hand the structured result back to the orchestrating command.
   - Done when: the command has what it needs to gate on status.

## Do not

- Change any GitHub state (labels, body, comments).
- Infer a status that isn't present — report it missing so the command can route to `/ds:grill`.
