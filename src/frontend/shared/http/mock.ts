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

const projects = [
  { id: projectAlpha, name: "Alpha portal" },
  { id: projectBeta, name: "Beta warehouse" },
];

const automations = [
  auto("ai:grill", "GrillToReady", false),
  auto("ready-for-proposal", "ProposeSpec", false),
  auto("ai:implement", "ImplementToPullRequest", true),
  auto("ai:refine", "RefineOrComment", false),
  auto("ai:estimate", "Estimate", false),
  auto("ai:transition", "TransitionState", false),
];

function auto(triggerLabel: string, action: string, requiresApproval: boolean) {
  return {
    id: crypto.randomUUID(),
    triggerLabel,
    triggerState: null,
    action,
    runtime: "OpenCode",
    requiresApproval,
    timeoutMinutes: 30,
    enabled: true,
  };
}

const stories = [
  story("11", "Close OPN-002: verify Entra ID works", ["status:backlog"], "Verify both paths."),
  story("12", "Sign in via Entra ID", ["ai:grill"], "As a member I want to sign in."),
  story("13", "Admin assigns roles", [], null),
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
  id: crypto.randomUUID(),
  vendorStoryId: story,
  automationId: automations[2]?.id ?? crypto.randomUUID(),
  state,
  createdAt: at(minutesAgo),
  dispatchedAt: at(minutesAgo - 1),
  outputLink: null,
  plan: null,
  approvedAt: null,
  failureReason: null,
  inputTokens: null,
  outputTokens: null,
  costUsd: null,
  ...extra,
});

const runs = [
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
  }),
  run("Failed", "11", 480, { failureReason: "The readiness document could not be read." }),
  run("Cancelled", "12", 600),
];

const connector = {
  vendor: "GitHub",
  owner: "acme",
  repository: "portal",
  secretName: "acme-pat",
  codeRepository: null,
  lastSyncedAt: at(2),
  lastFailure: null,
  lastFailureAt: null,
};

type Handler = (match: RegExpMatchArray, body: unknown) => unknown;

const routes: [string, RegExp, Handler][] = [
  ["GET", /^\/api\/projects$/, () => projects],
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
      },
      {
        projectId: projectBeta,
        vendor: "GitHub",
        lastSyncedAt: at(300),
        lastFailure: "Credential rejected by the vendor.",
        lastFailureAt: at(10),
      },
    ],
  ],
  [
    "POST",
    /^\/api\/projects$/,
    (_m, body) => {
      const created = { id: crypto.randomUUID(), name: (body as { name: string }).name };
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
  ["GET", /^\/api\/projects\/[^/]+\/backlog$/, () => ({ connector, stories })],
  ["POST", /^\/api\/projects\/[^/]+\/backlog\/refresh$/, () => ({ changes: 0 })],
  ["GET", /^\/api\/projects\/[^/]+\/automations$/, () => automations],
  [
    "POST",
    /^\/api\/projects\/[^/]+\/automations\/defaults$/,
    () => ({
      created: [],
      skipped: automations.map((a) => ({ triggerLabel: a.triggerLabel, reason: "exists" })),
      labelNote: null,
    }),
  ],
  [
    "POST",
    /^\/api\/projects\/[^/]+\/automations$/,
    (_m, body) => {
      const request = body as { triggerLabel: string; action: string; requiresApproval: boolean };
      const created = auto(request.triggerLabel, request.action, request.requiresApproval);
      automations.push(created);
      return created;
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
          action: "ImplementToPullRequest",
          fired: 7,
          failed: 1,
        },
        {
          automationId: "a2",
          triggerLabel: "ai:grill",
          action: "GrillToReady",
          fired: 5,
          failed: 1,
        },
        {
          automationId: "a3",
          triggerLabel: "ai:transition",
          action: "TransitionState",
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
    /^\/api\/projects\/[^/]+\/runs\/[^/]+\/log$/,
    () => ({
      content: "cloning acme/portal\nreading the story\nplanning changes\nwriting src/feature.ts",
      complete: false,
    }),
  ],
  [
    "GET",
    /^\/api\/projects\/[^/]+\/runs\/[^/]+\/changes$/,
    () => ({ number: 41, url: "https://github.com/acme/portal/pull/41", files: [] }),
  ],
];

/** Same contract as the real request(): resolves parsed JSON or throws. */
export async function mockRequest<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const method = init?.method ?? "GET";
  const body = typeof init?.body === "string" ? JSON.parse(init.body) : undefined;

  for (const [routeMethod, pattern, handler] of routes) {
    const match = path.match(pattern);
    if (routeMethod === method && match) {
      // A visible beat keeps loading states honest while staying fast enough to forget.
      await new Promise((resolve) => setTimeout(resolve, 120));
      return handler(match, body) as TResponse;
    }
  }

  throw new Error(`${AIO_MOCK_MARKER}: no mock route for ${method} ${path}`);
}
