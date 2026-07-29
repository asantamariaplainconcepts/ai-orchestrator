# Design: repo-defined-actions

## D1 — The prompt comes from the repository, like the rubric already does

DEC-048's argument was that a readiness bar belongs to a team, not to a product. A prompt is the same
kind of thing, only more so: it is the whole instruction, and a catalogue of them is a catalogue of
what one team imagined.

So the action reads a markdown file from the connected repository at execution time, through the same
`IDocumentReader` the grill's rubric and sync's procedure use. Live, never mirrored — BR-008's spirit:
the vendor holds the file and this product holds no copy that could be stale.

The path reuses `RubricPath` rather than adding a field. That column already means "the document this
action reads", it already flows through the API, the form and the canvas, and `sync-action` already
reused it for its close-out procedure. A second path column would be a second thing to keep in step
for no new meaning.

## D2 — The body is the prompt, and frontmatter is somebody else's wiring

Agentic workflow files in the Platform's own convention carry YAML frontmatter: a model, tools,
triggers. That block is how *another runner* is told what to do with the file.

Here the Automation is already that wiring — it names the runtime, the timeout, the approval gate and
the trigger. So the frontmatter is stripped and ignored, which is what makes an existing
`.github/workflows/*.md` reusable as-is rather than needing a fork.

Ignoring it is deliberate rather than lazy, and the alternative is worse: honouring a `model:` line
would let a file in somebody's repository choose what this product spends money on, and honouring a
`tools:` line would let it grant itself powers the Automation did not give it. Silence is the safe
reading, and the requirement says so out loud so nobody later mistakes it for an omission.

## D3 — One comment, because a prompt must not be able to widen its own surface

The answer is posted as a Story comment, which is RefineOrComment's surface and nothing more: no
label, no state, no workspace, no pull request.

That is the whole safety argument for shipping this at all. The prompt is untrusted text from a
repository — it can ask for anything, and what it can *do* is decided here, not there. A shell that
grew capabilities in response to what a prompt requested would be a product taking instructions from
its input.

The PR shell is a separate slice for the same reason: it is a bigger surface, and it should be opened
deliberately rather than as a consequence.

## D4 — Both refusals precede the agent

A path that does not resolve, and a file whose body is empty once frontmatter is stripped, are both
known before any money is spent. They fail there, naming the path.

No fallback prompt, and no substituting a catalogue action: an Automation configured to run the
repository's prompt and silently running something else is worse than one that stops. This is
sync-action's ordering (#123) and the grill's refusal (DEC-048) applied to a third case.

## D5 — Recorded, because the catalogue was closed on purpose

DEC-026 fixed the MVP action catalogue and DEC-048 opened the lane for it to grow with a stated
reason each time. This is the next entry: what the action reads, that frontmatter is ignored, and
that the write surface is one comment.
