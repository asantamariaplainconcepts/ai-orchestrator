# Design: repo-defined-actions

## D1 — The prompt comes from the repository, like the rubric already does

DEC-048's argument was that a readiness bar belongs to a team, not to a product. A prompt is the same
kind of thing, only more so: it is the whole instruction, and a catalogue of them is a catalogue of
what one team imagined.

So the action reads a markdown file from the connected repository at execution time, through the same
`IDocumentReader` the grill's rubric and sync's procedure use. Live, never mirrored — BR-008's spirit:
the vendor holds the file and this product holds no copy that could be stale.

The value reuses `RubricPath` rather than adding a field. That column already means "the document this
action reads", it already flows through the API, the form and the canvas, and `sync-action` already
reused it for its close-out procedure. A second path column would be a second thing to keep in step
for no new meaning. What it holds here is a **file name**, not a repository path — see D6 for why the
directory is the project's business rather than each Automation's.

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

## D6 — Where prompts live is the project's convention, not each Automation's

An Automation stores a **name** — `estimate.md`, subfolders allowed — and the project says which
directory names resolve against. Unset means `ai/prompts/`, the Platform's own `ai/` home, so a
project that configures nothing still works.

Storing the full path on each Automation was the obvious alternative, and it is wrong for a reason
worth stating: a team that moves its prompts would have to edit every Automation, and each edit is a
chance to leave one behind pointing at a file that no longer exists. With the directory held once,
moving the prompts is one field, and every Automation follows on its next Run. Nothing needs
migrating, because the file was never mirrored — the live read D1 already requires is what makes the
change take effect.

**The setting lives on the Connector**, beside `CodeRepository`. That field is already "where the code
lives, when that is not where the backlog lives"; this is "where the prompts live inside it" — the
same kind of fact, the same panel, the same use case (UC-004). It also gets the dependency right: no
Connector means no repository, and no repository means there is no prompt to read.

**Resolution happens in exactly one place** — inside the module that owns the Connector, behind
`IDocumentReader`. The Runs module passes the stored name and never learns that a directory exists.
Two reasons: the Backlog module owns Connector facts and this keeps them there, and one resolution
site means one place that composes the path and therefore one place that can name it in a refusal.
That is what makes D4's message trustworthy — the failure reports the **resolved** path, so a
misconfigured directory gives itself away instead of looking like a missing file.

**A name may not escape the directory.** A leading `/` or an upward `..` segment is refused, not
normalized. The whole point of holding the directory once is that it bounds where prompts come from,
and a boundary that can be stepped over is decoration. The issue put repo-absolute paths out of scope;
this is that rule enforced rather than merely stated, because "one rule" only holds if the other path
is closed.
