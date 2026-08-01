# Design: starter-prompts

## D1 — The portal offers; nothing writes

Settled before the proposal, with the argument in full in the proposal's own section: deterministic
content does not need a nondeterministic process; the no-overwrite criterion is structural in this
shape and unenforceable in the other; and an agent's pull request is not less human work than a
commit.

The consequence worth stating separately: **this change adds no repository write capability of any
kind.** #162 removed the orchestrator's writes and the argument for removing them does not weaken
because the files would have been ours.

## D2 — Two tiers, labelled by prerequisite

The issue named `ds-connect`'s six commands as the reference implementation. Measured against a fresh
repository, five of the six instruct an agent to read documents that will not be there:

| Prompt | Depends on |
| --- | --- |
| `grill` | `docs/process/definition-of-ready.md`, `docs/product/mvp/` |
| `propose` | `openspec` the tool, the `openspec-propose` skill |
| `implement` | `openspec`, `openspec-apply-change`, a `tasks.md` convention |
| `sync` | `openspec/specs/`, `openspec/changes/archive/`, `docs/process/retro-log.md` |
| `refine` | `docs/process/retro-log.md` |
| `status` | — |

So "the six, offered as starters" would ship a methodology installer with its documents missing. The
owner's decision, taken with that measurement on the table: **both tiers, each labelled by what it
requires.**

- **Portable** — assumes a cloned repository and, where the Automation names one, a Story. Nothing
  else. This is the tier that answers "a new project's first Run fails on a missing file".
- **Workflow** — the spec-first loop this product was built with, presented as a bundle whose
  prerequisites are stated on the surface, not discovered by an agent that cannot find a file.

The tiering is the substance of the decision rather than a hedge. Presented as one list, somebody
takes a `sync` prompt that folds delta specs into `openspec/specs/` in a project that has no
`openspec/`, and the failure arrives as an agent's confused output rather than as a prerequisite they
declined.

## D3 — The catalogue is files in this repository, not strings in a class

Each starter is a real `.md` file with frontmatter, embedded into the Projects module as a resource.
Three reasons, in order of weight:

1. **The frontmatter has to be real.** The acceptance criterion is that a file taken from here
   behaves identically run by this product or by a local agent CLI. That is only checkable if the
   artifact *is* the file, with its `---` block, and the test runs the product's own
   `RunExecutor.StripFrontmatter` over it.
2. **A prompt is edited as prose.** A contributor improving a starter should be editing markdown, not
   a C# verbatim string with escaped quotes.
3. **The test can read what ships.** Embedded resources are what the endpoint serves, so a test
   asserting "every starter loads and has a body" is asserting it about the shipped bytes.

## D3a — Where the catalogue lives (corrected, #207)

Originally `prompts/starter/` at the repository root, embedded with a `..\..\..\..` glob. That was
wrong in a way nothing in this bundle could catch: all four container images build the Projects
module, every Dockerfile copies `src/`, and none copies `prompts/` — so the resource was absent from
every build context and broke all four images at `dotnet publish`. The solution build passed, and no
workflow builds the images, so it surfaced at the release.

The catalogue now lives at `src/modules/Projects/AiOrchestrator.Modules.Projects/Starter/`, inside
the project that embeds it. Resource names are unchanged. Four Dockerfiles each remembering to copy
a directory is the failure mode, not the fix.

## D4 — The workflow tier is a copy, and the cost is stated

The workflow tier's content is this repository's own `.claude/commands/aio/*.md` — literally the
prompts this product's development runs on, which is what makes the tier honest rather than
illustrative.

They are **copied** into the catalogue rather than referenced. If the catalogue read the live command
files, editing `/aio:sync` for this repository's convenience would silently change what every project
is offered, and a starter set that changes underneath its consumers is not a starter set.

The cost is real and is not hidden: the two can drift. The test asserts the copies **load**, not that
they **match** — a match test would recreate the coupling the copy exists to avoid, by making every
edit here a two-file edit. Drift is the accepted price, and the accepted mitigation is that the
workflow tier is versioned content with its own review like anything else in this repository.

## D5 — One project-scoped endpoint, with the collision report in it

`GET /api/projects/{projectId}/starter-prompts`, `[Requires(ProjectPermissions.ReadAutomations)]` and
`IScopedToProject`.

Project-scoped rather than a global catalogue, even though the *content* is not project-specific,
because the useful answer is not the list — it is the list *against this project*: which of these do
you already have, and where would they go. A global endpoint would force the portal to make a second
call to answer the only question that matters, and would need a second permission for the same act.

## D6 — A collision is reported by reading, not assumed by not writing

Since nothing writes, "never overwritten" is true by construction and says nothing useful. The
promise worth keeping is the other half: **tell the Admin which starters they already have.**

Where the project has a Connector, the endpoint resolves each starter's target path through the same
`PromptPath` the Run path uses, reads it, and marks the ones that exist. Where there is no Connector
there is nothing to read, and the answer says so — an absent Connector is an ordinary state here, not
an error, because looking at the catalogue before configuring anything is exactly the first hour this
change is about.

The read is bounded by the catalogue's size, not by the repository's — one document read per starter,
and the catalogue is small by construction.

## D7 — What the portable tier contains, and what each still assumes

Five prompts. "Portable" means they name no document that does not exist in an arbitrary repository;
it does **not** mean they need no capability, and each states what it needs:

| File | One sentence | Still assumes |
| --- | --- | --- |
| `triage.md` | Read the Story and say what is missing before anybody writes code. | nothing beyond the Story |
| `explain.md` | Explain how the area of the code this Story touches actually works. | nothing beyond the clone |
| `implement.md` | Implement the Story on a branch and open a pull request. | push access, a vendor CLI |
| `tests.md` | Write the tests this Story's change needs, and run them. | the project's test command |
| `review.md` | Review the change this Story produced and report what is wrong with it. | an existing branch or PR |

Stating the assumption per file is the same discipline as D2's labelling, one level down: a prompt
that quietly needs push access is a prompt whose first failure is confusing.
