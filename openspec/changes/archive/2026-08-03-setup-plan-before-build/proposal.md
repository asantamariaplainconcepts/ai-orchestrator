# Proposal: setup-plan-before-build

## Why

[#233](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/233). An Admin pressed
**Build the workflow** without being told what it would build. The card offered prose — *"No prompt
files found. Looked in: …"* — a checkbox, and a button. The per-step detail existed, but only as a
report **after** the fact, which is the wrong side of an action that writes to somebody's repository.

## What changes

- **The plan is computed at read time and shown before the button.** One row per step: the trigger,
  the prompt file it wires, whether that file is already in the repository or a starter would be
  installed, and which step waits for a person.
- **The install-missing checkbox is gone.** It was standing in for a preview. Once the rows say
  which steps install a starter, a toggle asking whether to install them is a confirmation of a
  confirmation.
- **The draft-pull-request sentence sits beside the button**, where the decision is taken.
- **Long pipelines collapse** after three rows, because a plan that fills the screen stops being
  read.

## Where the plan comes from

The **discovery response**, which already lists every candidate directory and the files in it. The
canonical steps come from `PipelineSteps.All`, itself derived from the embedded starter catalogue.
So: no new endpoint, and **no extra vendor read** — the listing the card already fetched is enough.

## What does not change

- What the build creates, and the draft-PR mechanism that installs starters.
- The candidate chooser #229 added.
- The after-the-fact report, which stays as the record of what happened.

## Impact

`DiscoverPipeline` gains a `Plan` per candidate; the card renders it. No migration, no new
permission, no change to the build endpoint.
