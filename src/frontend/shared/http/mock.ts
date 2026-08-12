/**
 * The mock adapter behind the one HTTP seam — `pnpm dev:mock` only (Vite mode "mock").
 *
 * Exists so UI work needs no backend: every screen renders from this file, including the Run
 * states that are tedious to manufacture against a real stack (AwaitingApproval, AwaitingInput,
 * Failed). Deterministic on purpose: screenshots are reproducible, demos are rehearsable.
 *
 * Never ships: client.ts guards the dynamic import behind `import.meta.env.MODE === "mock"`,
 * which Vite replaces at build time, so production builds dead-code-eliminate this module —
 * and the build asserts that by grepping the bundle for AIO_MOCK_MARKER below.
 */
export const AIO_MOCK_MARKER = "AIO_MOCK_MARKER";

const now = Date.now();
const at = (minutesAgo: number) => new Date(now - minutesAgo * 60_000).toISOString();

const projectAlpha = "00000000-0000-7000-8000-00000000000a";
const projectBeta = "00000000-0000-7000-8000-00000000000b";

/** The projects that ship configured; anything created at runtime starts empty. */
const seeded = new Set([projectAlpha, projectBeta]);

const projects: { id: string; name: string; archivedAt: string | null }[] = [
  { id: projectAlpha, name: "Alpha portal", archivedAt: null },
  { id: projectBeta, name: "Beta warehouse", archivedAt: null },
];

let automations = [
  // The shipped pipeline (#310): grill claims the transition into `ready-for-proposal`, which claims
  // no transition of its own — so the board draws the boundary after it as a person's turn. The last
  // one also carries a mark, which is what makes the transition/mark split demonstrable here: the
  // board must draw a column for the stage and none for the mark (AC 7).
  auto("ai:grill", "RepositoryPrompt", "ready-for-proposal"),
  auto("ready-for-proposal", "RepositoryPrompt", null, ["needs-design"]),
  auto("ai:implement", "RepositoryPrompt", null, ["hitl"]),
  auto("ai:refine", "RepositoryPrompt", null),
  auto("ai:estimate", "RepositoryPrompt", null),
  auto("ai:transition", "RepositoryPrompt", null),
];

function auto(
  triggerLabel: string,
  action: string,
  toStage: string | null = null,
  marks: string[] = [],
) {
  return {
    id: crypto.randomUUID(),
    triggerLabel,
    triggerState: null,
    action,
    runtime: "OpenCode",
    timeoutMinutes: 30,
    enabled: true,
    // Marks only since #310 — the hand-off left this set and became the claim below.
    outputLabels: marks,
    // The one transition this Automation claims; its from-stage is the trigger label above.
    toStage,
    promptPath: null,
    // Null is the shipped state: an Automation thinks with the deployment's model until an Admin
    // chooses otherwise (#291).
    model: null,
  };
}

/**
 * The stored stage order, derived the way the aggregate that owns it derives it (#310, ADR-0016).
 *
 * The server stores this list; a fixture has nowhere to store it, so it recomputes it from the claims
 * using the same insertion rules `Project.ClaimTransition` applies. Case is folded throughout, exactly
 * as the domain folds it (DEC-056): a claim naming a stage that differs only in spelling uses the stage
 * that exists rather than creating a second spelling of it.
 */
function lifecycleStages(): string[] {
  const stages: string[] = [];
  const at = (name: string) =>
    stages.findIndex((stage) => stage.toLowerCase() === name.toLowerCase());

  for (const automation of automations) {
    if (!automation.enabled || !automation.toStage) continue;
    const from = automation.triggerLabel;
    const to = automation.toStage;
    const fromAt = at(from);
    const toAt = at(to);

    // Both already stages: the claim is stored and the list is untouched. The server refuses a
    // non-adjacent pair, so there is nothing here to reorder either.
    if (fromAt >= 0 && toAt >= 0) continue;
    if (toAt >= 0) {
      stages.splice(toAt, 0, from);
      continue;
    }
    if (fromAt >= 0) {
      stages.splice(fromAt + 1, 0, to);
      continue;
    }
    stages.push(from, to);
  }

  return stages;
}

const stories = [
  story("11", "Close OPN-002: verify Entra ID works", ["status:backlog"], "Verify both paths."),
  story("12", "Sign in via Entra ID", ["ai:grill"], "As a member I want to sign in."),
  story("13", "Admin assigns roles", [], null),
  // No Run of its own: the only Story that can actually be moved on the board, since every
  // other one is mid-Run and BR-001 refuses the gesture.
  story("14", "Rotate the deploy credential", [], "Quarterly."),
];

function story(vendorId: string, title: string, labels: string[], body: string | null) {
  return { vendorId, title, state: "open", labels, body };
}

// One Run per state, so any state-dependent UI can be built against this file.
const run = (
  state: string,
  story: string,
  minutesAgo: number,
  extra: Record<string, unknown> = {},
) => ({
  // Stable, not random: a mock Run has to be reachable by URL like every other mock state, and
  // a fresh uuid per module load meant a reloaded Run detail always answered "no longer exists".
  id: stableId(runSequence++),
  vendorStoryId: story,
  automationId: automations[2]?.id ?? crypto.randomUUID(),
  state,
  createdAt: at(minutesAgo),
  dispatchedAt: at(minutesAgo - 1),
  // Explicit, not absent (turn 10b): the rail shows "Finished · duration" only for a Run that
  // ended, so a mock that omitted these would render every state as still running. A terminal
  // state gets both; anything still in flight has begun and not finished, which is the truth.
  startedAt: at(minutesAgo - 1),
  endedAt: ["Succeeded", "Failed", "Cancelled"].includes(state) ? at(minutesAgo - 4) : null,
  outputLink: null,
  plan: null,
  approvedAt: null,
  failureReason: null,
  inputTokens: null,
  outputTokens: null,
  costUsd: null,
  // Null is the shipped state — a Run launched with no model at all (#291).
  resolvedModel: null,
  dismissedAt: null,
  locus: "Local",
  workingFolder: "/home/ana/repos/portal",
  branchName: `ai/${story}-story`,
  // run-on-a-pr: explicit nulls, not absences — `undefined !== null` renders as a change Run.
  targetChangeNumber: null,
  targetChangeUrl: null,
  targetChangeTitle: null,
  instruction: null,
  ...extra,
});

/**
 * `?previewEnds` needs the Run to END while somebody watches, which is a moment in time rather
 * than a state — so it is the one fixture keyed on elapsed time since load. Six seconds is long
 * enough to see the frame and short enough that nobody waits for it.
 */
const loadedAt = Date.now();
const PREVIEW_ENDS_AFTER_MS = 6_000;
const previewEnded = () =>
  new URLSearchParams(window.location.search).has("previewEnds") &&
  Date.now() - loadedAt > PREVIEW_ENDS_AFTER_MS;

/** BR-001's terminal set, mirrored so the mock cannot claim a finished Run is still live. */
const TERMINAL = ["Succeeded", "Failed", "Cancelled"];

/** Deterministic ids, so a mock Run's URL survives a reload and can be shared in a bug report. */
let runSequence = 0;
const stableId = (index: number) =>
  `00000000-0000-7000-8000-${(index + 1).toString().padStart(12, "0")}`;

const runs = [
  // A change-targeted Run (run-on-a-pr): no story, no automation — its identity is the change.
  {
    ...run("Executing", "", 2, {
      targetChangeNumber: 118,
      targetChangeUrl: "https://github.com/acme/portal/pull/118",
      targetChangeTitle: "feat(portal): the estimate explains itself on the story",
      instruction: "apply the review comments about naming",
      locus: "Sandbox",
      workingFolder: null,
      branchName: null,
    }),
    vendorStoryId: null,
    automationId: null,
  },
  run("Queued", "11", 3),
  run("Planning", "12", 8),
  run("AwaitingApproval", "12", 25, { plan: "## Plan\n\n- touch two files\n- add one test" }),
  run("AwaitingInput", "11", 55),
  run("Executing", "13", 4),
  run("Succeeded", "13", 240, {
    outputLink: "https://github.com/acme/portal/pull/41",
    inputTokens: 48_211,
    outputTokens: 9_102,
    costUsd: 0.0,
    // A finished Run names what spent it, which is the whole reason the field exists.
    resolvedModel: "github-copilot/claude-opus-4.6",
    dismissedAt: null,
  }),
  run("Failed", "11", 480, { failureReason: "The readiness document could not be read." }),
  // The mapped cause (turn 7): the banner links this one to Connector settings.
  run("Failed", "13", 90, {
    failureReason:
      "Credential could not be resolved: No secret named 'anthropic-api-key' was found.",
    locus: "Sandbox",
    workingFolder: null,
    branchName: null,
  }),
  run("Cancelled", "12", 600),
];

/** #244: the Project's runtime settings, mutable so the panel round-trips by hand. */
const runtimeSettings: { defaultRuntime: string | null; credentialNames: Record<string, string> } =
  { defaultRuntime: null, credentialNames: {} };

const connector = {
  vendor: "GitHub",
  owner: "acme",
  repository: "portal",
  secretName: "connector-github-019f9f2bc28e75808f50673005c5232c",
  secretSetAt: at(2),
  codeRepository: null,
  promptDirectory: null,
  lastSyncedAt: at(2),
  lastFailure: null,
  lastFailureAt: null,
  // #211: a local-folder project, so mock mode exercises the code-source surface, the Run now
  // dialog and the locus chip rather than only the repository path.
  codeSource: "LocalFolder",
  localPath: "/home/ana/repos/portal",
};

/** The one tier the catalogue ships (#269), and the paths consenting to it would write. */
const mockTiers = [
  {
    id: "workflow",
    title: "The spec-first workflow",
    summary:
      "The loop this product's own development runs on: an idea is interrogated to readiness, proposed as a spec, implemented against it, then closed out with a retro and one commit.",
    requires:
      "OpenSpec, and the documents these prompts read — a definition of ready, a retro log, and the openspec/ directory layout. Consenting to this tier installs all of them alongside the prompts; anything you already have is left exactly as it is.",
    prerequisites: [
      "docs/process/definition-of-ready.md",
      "docs/process/backlog-shaping-rules.md",
      "docs/process/product-context.md",
      "docs/process/retro-log.md",
      "openspec/config.yaml",
      "openspec/specs/.gitkeep",
      "openspec/changes/archive/.gitkeep",
    ],
  },
];

/**
 * The plan each candidate directory is offered with (#233), shared by discovery and the setup
 * report so the two cannot disagree — the report must only ever name steps the plan proposed.
 *
 * The edges are the catalogue's own (#273): `ai:grill → ai:propose → ai:implement → ai:sync`,
 * decided as the spec-first tier's wiring and carried here so the mock cannot disagree with the
 * manifest. They are what makes #262's broken-hand-off marker demonstrable by hand: unchecking
 * `ai:propose` marks `ai:implement`. Unchecking `ai:status` marks nothing — it hands to nobody.
 *
 * `installable: false` on every row is not an oversight: this tier declares a prerequisite, so a
 * starter is written for it only once the consent above is on. The card adds those rows itself.
 */
const mockPlans: Record<
  string,
  {
    trigger: string;
    promptFile: string;
    exists: boolean;
    gated: boolean;
    installable: boolean;
    toStage: string | null;
    tierId: string;
  }[]
> = {
  // A directory holding some of the loop but not all of it — the adoption case, where the rows that
  // exist are wired and the rest wait on a consent.
  "ai/prompts": [
    {
      trigger: "ai:grill",
      promptFile: "grill.md",
      exists: true,
      gated: false,
      installable: false,
      toStage: "ai:propose",
      tierId: "workflow",
    },
    {
      trigger: "ai:propose",
      promptFile: "aio-propose.md",
      exists: false,
      gated: true,
      installable: false,
      toStage: "ai:implement",
      tierId: "workflow",
    },
    {
      trigger: "ai:implement",
      promptFile: "aio-implement.md",
      exists: false,
      gated: true,
      installable: false,
      toStage: "ai:sync",
      tierId: "workflow",
    },
    {
      trigger: "ai:sync",
      promptFile: "aio-sync.md",
      exists: false,
      gated: true,
      installable: false,
      toStage: null,
      tierId: "workflow",
    },
    {
      trigger: "ai:refine",
      promptFile: "aio-refine.md",
      exists: false,
      gated: false,
      installable: false,
      toStage: null,
      tierId: "workflow",
    },
    {
      trigger: "ai:status",
      promptFile: "aio-status.md",
      exists: false,
      gated: false,
      installable: false,
      toStage: null,
      tierId: "workflow",
    },
  ],
  // A repository that already runs the whole loop from its own command directory: every row exists,
  // so the consent changes nothing about the prompts and only its documents could still be written.
  ".claude/commands/ds": [
    {
      trigger: "ai:grill",
      promptFile: "grill.md",
      exists: true,
      gated: false,
      installable: false,
      toStage: "ai:propose",
      tierId: "workflow",
    },
    {
      trigger: "ai:propose",
      promptFile: "propose.md",
      exists: true,
      gated: true,
      installable: false,
      toStage: "ai:implement",
      tierId: "workflow",
    },
    {
      trigger: "ai:implement",
      promptFile: "implement.md",
      exists: true,
      gated: true,
      installable: false,
      toStage: "ai:sync",
      tierId: "workflow",
    },
    {
      trigger: "ai:sync",
      promptFile: "sync.md",
      exists: true,
      gated: true,
      installable: false,
      toStage: null,
      tierId: "workflow",
    },
  ],
};

type Handler = (match: RegExpMatchArray, body: unknown, params: URLSearchParams) => unknown;

const routes: [string, RegExp, Handler][] = [
  [
    "GET",
    /^\/api\/me$/,
    // The CurrentPrincipal shape since #13: standing per project, never a single role.
    () => ({
      id: "local-owner",
      displayName: "Local owner",
      projects: [
        { projectId: projectAlpha, name: "Alpha portal", role: "Admin" },
        { projectId: projectBeta, name: "Beta warehouse", role: "Admin" },
      ],
    }),
  ],
  [
    "GET",
    /^\/api\/projects$/,
    (_m, _b, params) => ({
      projects:
        params.get("includeArchived") === "true"
          ? projects
          : projects.filter((project) => !project.archivedAt),
      archivedCount: projects.filter((project) => project.archivedAt).length,
    }),
  ],
  [
    "POST",
    /^\/api\/projects\/([^/]+)\/archive$/,
    (match) => {
      const found = projects.find((p) => p.id === match[1]);
      if (found) found.archivedAt = new Date().toISOString();
      return {};
    },
  ],
  [
    "POST",
    /^\/api\/projects\/([^/]+)\/restore$/,
    (match) => {
      const found = projects.find((p) => p.id === match[1]);
      if (found) found.archivedAt = null;
      return {};
    },
  ],
  [
    "GET",
    /^\/api\/connectors$/,
    () => [
      {
        projectId: projectAlpha,
        vendor: "GitHub",
        lastSyncedAt: at(2),
        lastFailure: null,
        lastFailureAt: null,
        codeSource: "LocalFolder",
        localPath: "/home/ana/repos/portal",
      },
      {
        projectId: projectBeta,
        vendor: "GitHub",
        lastSyncedAt: at(300),
        lastFailure: "Credential rejected by the vendor.",
        lastFailureAt: at(10),
        codeSource: "Repository",
        localPath: null,
      },
    ],
  ],
  [
    "POST",
    /^\/api\/projects$/,
    (_m, body) => {
      const created = {
        id: crypto.randomUUID(),
        name: (body as { name: string }).name,
        archivedAt: null,
      };
      projects.push(created);
      return created;
    },
  ],
  [
    "GET",
    /^\/api\/inbox$/,
    () =>
      runs
        .filter((r) => ["AwaitingApproval", "AwaitingInput", "Failed"].includes(r.state))
        .map((r) => ({
          runId: r.id,
          projectId: projectAlpha,
          projectName: "Alpha portal",
          vendorStoryId: r.vendorStoryId,
          storyTitle: stories.find((s) => s.vendorId === r.vendorStoryId)?.title ?? null,
          waitingFor:
            r.state === "AwaitingApproval"
              ? "approval"
              : r.state === "AwaitingInput"
                ? "input"
                : "failure",
          waitingSince: r.createdAt,
        })),
  ],
  [
    "GET",
    /^\/api\/inbox\/changes$/,
    // The review queue's four states in one answer (inbox-open-prs): entries, a product-created
    // one linking its Run, and a per-project refusal beside working rows — the empty state is
    // reachable by a project with nothing open, which Beta plays here.
    () => ({
      changes: [
        {
          projectId: projectAlpha,
          projectName: "Alpha portal",
          number: 118,
          title: "feat(portal): the estimate explains itself on the story",
          url: "https://github.com/acme/portal/pull/118",
          createdAt: new Date(Date.now() - 40 * 60_000).toISOString(),
          // The product's own: matches runs[0]'s recorded output link below.
          runId: runs[0]?.id ?? null,
        },
        {
          projectId: projectAlpha,
          projectName: "Alpha portal",
          number: 117,
          title: "chore(deps): bump the ui-theme package",
          url: "https://github.com/acme/portal/pull/117",
          createdAt: new Date(Date.now() - 26 * 3_600_000).toISOString(),
          runId: null,
        },
      ],
      refusals: [
        {
          projectId: projectBeta,
          projectName: "Beta warehouse",
          reason: "the API rate limit was exceeded",
        },
      ],
    }),
  ],
  [
    "GET",
    /^\/api\/projects\/([^/]+)\/backlog$/,
    // A project created in this session has no Connector yet — the state the settings form's
    // essentials-first shape (#220) and the onboarding checklist (#211) both exist for.
    (match) =>
      seeded.has(match[1] ?? "") ? { connector, stories } : { connector: null, stories: [] },
  ],
  // #210/#211 — the self-host posture answers; a cloud deployment would 404 the whole surface.
  // Saving reflects what the form actually sent — without this, mock mode silently discards a
  // configure and the screen keeps showing the old Connector (#220).
  [
    "PUT",
    /^\/api\/projects\/([^/]+)\/connector$/,
    (match, body) => {
      const sent = body as Partial<typeof connector> & { accessToken?: string | null };
      seeded.add(match[1] ?? "");
      Object.assign(connector, {
        vendor: sent.vendor ?? connector.vendor,
        owner: sent.owner ?? connector.owner,
        repository: sent.repository ?? connector.repository,
        codeRepository: sent.codeRepository ?? null,
        promptDirectory: sent.promptDirectory ?? null,
        codeSource: sent.codeSource ?? "Repository",
        localPath: sent.localPath ?? null,
      });
      return {};
    },
  ],
  [
    "POST",
    /^\/api\/projects\/[^/]+\/connector\/validate-path$/,
    (_match, body) => {
      const path = (body as { path: string }).path ?? "";
      return {
        isDirectory: path.startsWith("/") || path.startsWith("~"),
        isGitRepository: path.includes("repos") || path.includes("work"),
        branch: "main",
        isClean: true,
      };
    },
  ],
  // #132 — one capability allowed and one refused, so the panel's two branches are both visible
  // in mock mode rather than only the happy one.
  // #222 — what this deployment can offer. `?noStore` flips the second shape so both branches of
  // the credential control are reachable in mock mode without a second fixture.
  [
    "GET",
    /^\/api\/capabilities$/,
    () => {
      const search = new URLSearchParams(window.location.search);
      const noStore = search.has("noStore");
      // #247 — `?noLocal` renders the compose shape: self-host, but the folder is unreachable.
      const noLocal = search.has("noLocal");
      return {
        hasCodeSource: true,
        canStoreSecret: !noStore,
        storeRemedy: noStore ? "Set Secrets:Directory to store values here." : null,
        canUseLocalFolder: !noLocal,
        localFolderReason: noLocal
          ? "the orchestrator runs in a container here, and a folder on this machine is not visible to it"
          : null,
      };
    },
  ],
  // #279 — the runtimes of the machine that executes Runs. Their not-ready shapes stay reachable
  // without uninstalling anything: `?cliMissing` renders the missing-CLI remedy, `?secretMissing`
  // the unresolvable secret, `?sandboxed` puts the agents in a per-Run sandbox, `?sandboxDown`
  // adds the host being unreachable, and `?sessionStuck` the session no copy can carry (#288).
  [
    "GET",
    /^\/api\/runtimes$/,
    () => {
      const search = new URLSearchParams(window.location.search);
      const cliMissing = search.has("cliMissing");
      const secretMissing = search.has("secretMissing");
      const sandboxDown = search.has("sandboxDown");
      const sessionStuck = search.has("sessionStuck");
      const sandboxed = sandboxDown || search.has("sandboxed");
      return {
        hosted: true,
        checkedAt: at(0.3),
        retrySeconds: 30,
        runtimes: [
          {
            name: "OpenCode",
            command: "opencode",
            cliReady: !cliMissing,
            installCommand: "npm install -g opencode-ai@1.18.6",
            credentialSecretName: null,
            credentialReady: null,
            sessionUnavailableReason: null,
            sessionUnavailableRemedy: null,
          },
          {
            name: "ClaudeCodeHeadless",
            command: "claude",
            cliReady: true,
            installCommand: "npm install -g @anthropic-ai/claude-code@2.0.44",
            credentialSecretName: secretMissing ? "anthropic-api-key" : null,
            credentialReady: secretMissing ? false : null,
            sessionUnavailableReason: sessionStuck
              ? "'claude' keeps its session in this machine's keychain, not in a file, so the sandbox cannot be given a copy of it. Store an API key in the sandbox host and point Agents:ClaudeCodeHeadless:CredentialSecretName at 'claude', or run this Automation on a runtime whose session travels."
              : null,
            sessionUnavailableRemedy: sessionStuck ? "sbx secret set -g claude" : null,
          },
        ],
        host: sandboxed
          ? {
              where: "a per-Run sandbox on this machine",
              ready: !sandboxDown,
              remedy: sandboxDown
                ? "The sandbox daemon is not running, so no Run can execute here. Start it with `sbx daemon start`."
                : null,
            }
          : null,
      };
    },
  ],
  // Design review 5d — the name/value split's live check. Resolves only for the name the
  // fixture Connector already carries, so both verdicts are reachable by typing.
  [
    "GET",
    /^\/api\/projects\/[^/]+\/connector\/secret-resolves$/,
    (_match, _body, params) => ({
      resolves: params.get("name") === connector.secretName,
    }),
  ],
  // #226 — what to grant, for the shape being configured. A local code source asks for less.
  [
    "GET",
    /^\/api\/projects\/[^/]+\/connector\/required-permissions$/,
    (_match, _body, params) => ({
      scopes:
        params.get("codeSource") === "LocalFolder"
          ? ["Issues: read", "Contents: read", "Issues: write"]
          : [
              "Issues: read",
              "Contents: read",
              "Issues: write",
              "Contents: write, Pull requests: write",
            ],
    }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/connector\/test$/,
    () => ({
      satisfied: false,
      capabilities: [
        { capability: "reading the backlog's Stories", succeeded: true, reason: null },
        {
          capability: "pushing a branch and opening a pull request",
          succeeded: true,
          reason: null,
          notVerifiable: "this connector claims no way to ask without performing the write",
        },
        {
          capability: "reading the repository's files",
          succeeded: false,
          reason: "Resource not accessible by personal access token",
        },
      ],
    }),
  ],
  ["POST", /^\/api\/projects\/[^/]+\/backlog\/refresh$/, () => ({ changes: 0 })],
  // The picker's listing (#215): what the repository's prompts directory holds, read live.
  [
    "GET",
    /^\/api\/projects\/[^/]+\/prompts$/,
    () => ({
      directory: "ai/prompts",
      names: ["estimate.md", "triage.md", "explain.md"],
      reason: null,
    }),
  ],
  ["GET", /^\/api\/projects\/[^/]+\/automations$/, () => automations],
  // The lifecycle the server serves (#310). ADR-0016: the fixture *derives what the server derives*,
  // so this applies each claim exactly as `Project.ClaimTransition` does — a claim whose to-stage is
  // already a stage inserts its from-stage immediately before it, a claim out of an existing stage
  // inserts its to-stage immediately after, and a claim naming neither appends both in order. A
  // hand-written list here would have been a second description of the same fact, free to drift.
  ["GET", /^\/api\/projects\/[^/]+\/lifecycle$/, () => ({ stages: lifecycleStages() })],
  [
    "POST",
    /^\/api\/projects\/[^/]+\/automations$/,
    (_match: RegExpMatchArray, body: unknown) => {
      const request = body as {
        triggerLabel: string;
        action: string;
        toStage?: string | null;
        outputLabels?: string[];
      };
      const created = auto(
        request.triggerLabel,
        request.action,
        request.toStage ?? null,
        request.outputLabels ?? [],
      );
      // A new array, for the same reason the update below builds one: React Query keeps previous data
      // when a refetch returns the same reference.
      automations = [...automations, created];
      return created;
    },
  ],
  // The update the canvas actually performs (ADR-0016: a fixture derives what the server
  // derives). Without it every canvas gesture — the human block since #137, drag-to-chain since
  // turn 8 — was undemonstrable here: the request 404'd and the picture never moved, so the mock
  // showed a workflow that could be read and not built.
  [
    "PUT",
    /^\/api\/projects\/[^/]+\/automations\/([^/]+)$/,
    (match: RegExpMatchArray, body: unknown) => {
      const id = match[1] ?? "";
      const index = automations.findIndex((automation) => automation.id === id);
      const held = automations[index];
      if (!held) throw new Error(`No automation ${id}`);

      // A whole-object replace, exactly as the endpoint behaves — which is what makes the mock
      // able to reproduce the field-clearing hazard rather than hide it.
      const patch = body as Partial<(typeof automations)[number]>;
      const updated = { ...held, ...patch, id: held.id };

      // A NEW array, never a mutation in place. React Query keeps the previous data when a
      // refetch returns the same reference, so an in-place fixture shows the change on whichever
      // component happened to re-render and leaves the rest reading the old truth — which is a
      // disagreement the product itself would never have.
      automations = automations.map((automation, at) => (at === index ? updated : automation));
      return updated;
    },
  ],
  // The model chooser's three states (#291), which are three different things to tell somebody.
  // `?modelsUnasked` renders the unreachable machine; `?modelsUndeclared` the runtime that cannot
  // list and has nothing configured. Neither is an empty list, and the panel must not say it is.
  [
    "GET",
    /^\/api\/agent-runtimes\/([^/]+)\/models$/,
    (match: RegExpMatchArray) => {
      const runtimeName = match[1];
      const search = new URLSearchParams(window.location.search);

      if (search.has("modelsUnasked")) {
        return { runtimeName, models: [], source: "couldNotAsk" };
      }

      // Claude Code has no listing command, so it reads an operator's configuration — empty
      // unless this deployment declared any.
      if (runtimeName === "ClaudeCodeHeadless") {
        return {
          runtimeName,
          models: search.has("modelsUndeclared") ? [] : ["sonnet"],
          source: "declared",
        };
      }

      return {
        runtimeName,
        models: [
          "opencode/deepseek-v4-flash-free",
          "github-copilot/claude-haiku-4.5",
          "github-copilot/claude-opus-4.6",
        ],
        source: "enumerated",
      };
    },
  ],
  // The starter set (#190), with one of each presence state so the section exercises them all.
  [
    "GET",
    /^\/api\/projects\/[^/]+\/starter-prompts$/,
    () => [
      {
        id: mockTiers[0]!.id,
        title: mockTiers[0]!.title,
        summary: mockTiers[0]!.summary,
        requires: mockTiers[0]!.requires,
        prompts: [
          {
            file: "grill.md",
            saveAs: "aio-grill.md",
            purpose: "Interrogate a raw idea until it meets the definition of ready.",
            assumes: "A definition-of-ready document, and product context to grill against.",
            content: "---\ndescription: grill\n---\nInterrogate the idea.",
            targetPath: "ai/prompts/aio-grill.md",
            alreadyPresent: false,
          },
          {
            file: "sync.md",
            saveAs: "aio-sync.md",
            purpose: "Close out an approved change: retro, archive, spec sync, then one commit.",
            assumes: "OpenSpec's specs and archive directories, and a retro log.",
            content: "---\ndescription: sync\n---\nClose out the change.",
            targetPath: "ai/prompts/aio-sync.md",
            alreadyPresent: true,
          },
        ],
      },
    ],
  ],
  // Discovery (#229): the motivating shape — a pipeline kept one level under `.claude/commands`,
  // beside a conventional directory, so the surface exercises "two candidates, pick one".
  [
    "GET",
    /^\/api\/projects\/[^/]+\/automations\/discover-pipeline$/,
    () => ({
      candidates: [
        {
          directory: "ai/prompts",
          files: ["grill.md", "sprint-notes.md"],
          steps: ["ai:grill"],
          unmatched: ["sprint-notes.md"],
          plan: mockPlans["ai/prompts"],
        },
        {
          directory: ".claude/commands/ds",
          files: ["grill.md", "propose.md", "implement.md", "sync.md", "sprint-notes.md"],
          steps: ["ai:grill", "ai:propose", "ai:implement", "ai:sync"],
          unmatched: ["sprint-notes.md"],
          plan: mockPlans[".claude/commands/ds"],
        },
      ],
      searchedIn: ["ai/prompts", ".claude/commands"],
      reason: null,
      tiers: mockTiers,
    }),
  ],
  [
    "POST",
    /^\/api\/projects\/[^/]+\/automations\/set-up-defaults$/,
    (_match, body) => {
      const input = body as {
        promptDirectory?: string;
        installMissing?: boolean;
        steps?: string[];
        tiers?: string[];
      };
      const directory = input.promptDirectory ?? "ai/prompts";

      // Absent means every step, an empty list means none (#262) — the mock honours the same
      // distinction the API does, or mock mode would teach the wrong contract.
      const kept = (trigger: string) => input.steps === undefined || input.steps.includes(trigger);

      // Absent means *no* tier (#269) — the opposite default, and the mock has to reproduce that
      // asymmetry too, because a mock that authorised by default would teach the dangerous half.
      const consented = (tierId: string) => (input.tiers ?? []).includes(tierId);

      // Derived from the plan this directory was offered with, never a list of its own: the real
      // action only ever reports steps it would have acted on, and a mock that named others would
      // be teaching a contract the API does not have.
      const offered = mockPlans[directory] ?? mockPlans["ai/prompts"] ?? [];

      // A step is acted on when it is selected *and* either its file is already there or its tier
      // was consented to. An unconsented gap is not created and not installed.
      const actionable = offered.filter(
        (step) => kept(step.trigger) && (step.exists || consented(step.tierId)),
      );

      // One trigger stands in for "the project already had this", so the report shows a skip and
      // an exclusion side by side — they are different facts and must never merge.
      const alreadyTaken = "ai:grill";
      const created = actionable
        .filter((step) => step.trigger !== alreadyTaken)
        .map((step) => step.trigger);
      const skipped = actionable
        .filter((step) => step.trigger === alreadyTaken)
        .map((step) => ({
          trigger: step.trigger,
          reason: "an Automation already uses this trigger",
        }));

      const installedFiles = actionable
        .filter((step) => !step.exists)
        .map((step) => `${directory}/${step.promptFile}`);

      // Two of the tier's paths stand in for "you already had this", so the report shows written and
      // already-present prerequisites side by side.
      const tierPaths = mockTiers
        .filter((tier) => consented(tier.id))
        .flatMap((tier) => tier.prerequisites);
      const alreadyYours = ["docs/process/retro-log.md"];
      const prerequisites = tierPaths.filter((path) => !alreadyYours.includes(path));
      const prerequisitesAlreadyPresent = tierPaths.filter((path) => alreadyYours.includes(path));

      const wrote = installedFiles.length + prerequisites.length;

      return {
        directory,
        created,
        skipped,
        foundNotWired: ["sprint-notes.md"],
        excluded: offered
          .map((step) => step.trigger)
          .filter((trigger) => !kept(trigger))
          .concat(
            // Not excluded by choice of row, but not acted on either: an unconsented gap. Reported
            // here so mock mode shows the same "nothing happened for this" the API reports.
            offered
              .filter((step) => kept(step.trigger) && !step.exists && !consented(step.tierId))
              .map((step) => step.trigger),
          ),
        // No files left to write means no branch and no pull request — the same clean outcome as a
        // repository that already had everything, never a failure.
        installed: input.installMissing
          ? {
              files: installedFiles,
              pullRequestUrl: wrote > 0 ? "https://github.com/acme/portal/pull/8" : null,
              branch: wrote > 0 ? "starter/pipeline" : null,
              failure: null,
              prerequisites,
              prerequisitesAlreadyPresent,
            }
          : null,
        missingPrompts: actionable
          .filter((step) => !step.exists)
          .map((step) => ({
            saveAs: step.promptFile,
            resolvedPath: `${directory}/${step.promptFile}`,
          })),
      };
    },
  ],
  // Install (#214): the draft PR a human goes to review.
  [
    "POST",
    /^\/api\/projects\/[^/]+\/starter-prompts\/install$/,
    (_match, body) => ({
      url: "https://github.com/acme/portal/pull/7",
      path: `ai/prompts/${(body as { saveAs: string }).saveAs}`,
      branch: `starter/${(body as { saveAs: string }).saveAs.replace(/\.md$/i, "")}`,
    }),
  ],
  [
    "POST",
    /^\/api\/projects\/[^/]+\/automations\/defaults$/,
    () => ({
      created: [],
      skipped: automations.map((a) => ({ triggerLabel: a.triggerLabel, reason: "exists" })),
      labelNote: null,
    }),
  ],
  // A second PUT and a second POST for these same two routes used to sit here, shadowed by the ones
  // above because the first match wins — and the shadowed PUT did `Object.assign` on the held object,
  // which is exactly the in-place mutation ADR-0016 forbids in this file. Dead and dangerous is worse
  // than dead: whichever of the two a later edit happened to reorder would silently change behaviour.
  [
    "POST",
    /^\/api\/projects\/[^/]+\/changes\/(\d+)\/runs$/,
    // run-on-a-pr: the launch answers created; the run lists are the surfaces that change.
    (match) => ({
      id: crypto.randomUUID(),
      changeNumber: Number(match[1]),
      state: "Queued",
      dispatched: true,
      waitingAtCap: false,
    }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/runtimes$/,
    // #244: the Project's runtime settings — names only, never values (BR-010).
    () => runtimeSettings,
  ],
  [
    "PUT",
    /^\/api\/projects\/[^/]+\/runtimes$/,
    (_m, body) => {
      const request = body as {
        defaultRuntime: string | null;
        credentialNames: Record<string, string>;
      };
      runtimeSettings.defaultRuntime = request.defaultRuntime;
      runtimeSettings.credentialNames = request.credentialNames;
      return runtimeSettings;
    },
  ],
  ["GET", /^\/api\/projects\/[^/]+\/runs$/, () => runs],
  ["POST", /^\/api\/projects\/[^/]+\/runs$/, () => runs[0]],
  ["POST", /^\/api\/projects\/[^/]+\/runs\/[^/]+\/cancel$/, () => runs[7]],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/cost$/,
    () => ({ totalCostUsd: 0.42, reportedRuns: 6, unknownRuns: 2 }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/pulse$/,
    () => ({
      runsStarted: 12,
      terminalRuns: 10,
      successRate: 0.8,
      knownCostUsd: 0.37,
      reportedRuns: 9,
      unknownCostRuns: 3,
      meanQueueWaitSeconds: 18,
      meanDurationSeconds: 264,
      automations: [
        {
          automationId: "a1",
          triggerLabel: "ai:implement",
          action: "RepositoryPrompt",
          fired: 7,
          failed: 1,
        },
        {
          automationId: "a2",
          triggerLabel: "ai:grill",
          action: "RepositoryPrompt",
          fired: 5,
          failed: 1,
        },
        {
          automationId: "a3",
          triggerLabel: "ai:transition",
          action: "RepositoryPrompt",
          fired: 0,
          failed: 0,
        },
      ],
      storiesTotal: 9,
      storiesNeverRun: 2,
      waiting: { approval: 1, input: 1, failure: 1 },
      oldestOpenQuestionSeconds: 5400,
    }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/stories\/([^/]+)$/,
    (match) => {
      const found = stories.find((s) => s.vendorId === match[1]);
      return { ...found, lastSeenAt: at(2) };
    },
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/stories\/[^/]+\/documents$/,
    () => ({ change: null, documents: [] }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/runs\/([^/]+)\/log$/,
    (match) => ({
      content:
        '{"type":"system","subtype":"init","session_id":"s-1","cwd":"/work"}\n{"type":"assistant","message":{"id":"m-1","content":[{"type":"text","text":"Reading the story, then the two files it names.\\n\\n**Plan:** add the guard, then a test."}]},"usage":{"input_tokens":1840,"output_tokens":96}}\n{"type":"assistant","message":{"id":"m-2","content":[{"type":"tool_use","id":"t-1","name":"Read","input":{"file_path":"src/feature.ts"}}]}}\n{"type":"text","sessionID":"s-1","part":{"id":"p-9","type":"text","text":"The guard belongs before the write, not after."}}\n{"type":"step_finish","sessionID":"s-1","part":{"type":"step-finish","tokens":{"input":420,"output":37},"cost":0.0042}}\nwarning: could not read .git/config (permission denied)\n{"type":"result","subtype":"success","is_error":false,"result":"Added the guard and a regression test.","usage":{"input_tokens":2260,"output_tokens":133},"total_cost_usd":0.0118}',
      // Derived from the Run's own state, as the server derives it from RunStates.IsTerminal.
      // It was hardcoded false, which taught the UI that a Succeeded Run is still live — the
      // exact fault the note below warns about, one field over.
      complete:
        previewEnded() ||
        TERMINAL.includes(runs.find((candidate) => candidate.id === match[1])?.state ?? ""),
      // Four lines read, so the next chunk is 4 (#144): the mock has to carry the field the
      // contract carries, or it teaches the UI a shape the server does not send.
      nextSequence: 4,
    }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/runs\/[^/]+\/preview$/,
    // run-previews: every state reachable by hand, the repository's idiom. Default is the
    // habitat that cannot host previews at all — the honest default and the one whose sentence
    // must not read as "this Run failed to make one". `?preview` hosts one and shows the frame.
    () => {
      const search = new URLSearchParams(window.location.search);
      const hosted =
        search.has("preview") || search.has("previewIdle") || search.has("previewEnds");
      return {
        hosted,
        available: (search.has("preview") || search.has("previewEnds")) && !previewEnded(),
      };
    },
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/runs\/[^/]+\/preview\/serve\//,
    // What the relay would return: somebody else's application, which is exactly the point —
    // the frame must render it without granting it anything.
    () =>
      "<!doctype html><meta charset=utf-8><title>preview</title>" +
      '<body style="font:14px system-ui;padding:24px">' +
      "<h1>The Agent's application</h1><p>Served from inside its sandbox.</p>",
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/runs\/[^/]+\/changes$/,
    // Turn 7's states, reachable by hand: a long hunk (pagination), a second file (collapse on
    // mobile), and a binary (the stated omission).
    // Wrapped in { change } — the envelope the API actually answers with; the old flat shape
    // made every mock run read "no pull request".
    () => ({
      change: {
        number: 41,
        url: "https://github.com/acme/portal/pull/41",
        files: [
          {
            path: "src/frontend/features/runs/very/long/nested/path/to/RunDetailSection.tsx",
            status: "modified",
            additions: 48,
            deletions: 3,
            patch: [
              "@@ -12,6 +12,51 @@",
              ' import { t } from "i18n";',
              "-const OLD_WIDTH = 280;",
              "-const RAIL = true;",
              "-const LEGIBLE = false;",
              "+const BODY_WIDTH = 600;",
              ...Array.from(
                { length: 47 },
                (_, index) => `+const line${index + 1} = 'added content ${index + 1}';`,
              ),
              " export {};",
            ].join("\n"),
            patchOmittedReason: null,
          },
          {
            path: "docs/process/decision-journal.md",
            status: "modified",
            additions: 27,
            deletions: 0,
            patch: [
              "@@ -2020,3 +2020,30 @@",
              " **ADR:** none new, but the harness decision is its own",
              "+## 2026-07-30 — strict-with-language, not strict-with-people",
              '+**Worked:** the owner said "wrong" and the codebase replaced a worse design',
            ].join("\n"),
            patchOmittedReason: null,
          },
          {
            path: "assets/logo.png",
            status: "modified",
            additions: 0,
            deletions: 0,
            patch: null,
            patchOmittedReason: "Binary",
          },
        ],
      },
    }),
  ],
  // The licensed write (UC-008), mutating the in-memory mirror so the board and the label pills
  // actually move things in mock mode. A label whose name starts with "refuse" is rejected —
  // the vendor's refusal is a state the UI must render, and it is the one this file cannot
  // manufacture any other way.
  ["PUT", /^\/api\/projects\/[^/]+\/backlog\/stories\/([^/]+)\/labels\/([^/]+)$/, labelWrite(true)],
  [
    "DELETE",
    /^\/api\/projects\/[^/]+\/backlog\/stories\/([^/]+)\/labels\/([^/]+)$/,
    labelWrite(false),
  ],
];

function labelWrite(apply: boolean): Handler {
  return (match) => {
    const vendorId = decodeURIComponent(match[1] ?? "");
    const label = decodeURIComponent(match[2] ?? "");
    if (label.startsWith("refuse")) throw new Error("The vendor rejected the label.");

    const found = stories.find((candidate) => candidate.vendorId === vendorId);
    if (found) {
      found.labels = apply
        ? [...new Set([...found.labels, label])]
        : found.labels.filter((candidate) => candidate !== label);
    }
    return {};
  };
}

/** Same contract as the real request(): resolves parsed JSON or throws. */
export async function mockRequest<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const method = init?.method ?? "GET";
  const body = typeof init?.body === "string" ? JSON.parse(init.body) : undefined;

  // Routes match the path, never the query: they always did, but nothing had exercised it —
  // the story-filtered runs list has been sending `?vendorStoryId=` past a pattern anchored
  // with `$` since it shipped, and silently getting "no mock route" (#121).
  const route = path.split("?")[0] ?? path;
  const params = new URLSearchParams(path.slice(route.length));

  for (const [routeMethod, pattern, handler] of routes) {
    const match = route.match(pattern);
    if (routeMethod === method && match) {
      // A visible beat keeps loading states honest while staying fast enough to forget.
      await new Promise((resolve) => setTimeout(resolve, 120));
      return handler(match, body, params) as TResponse;
    }
  }

  throw new Error(`${AIO_MOCK_MARKER}: no mock route for ${method} ${path}`);
}
