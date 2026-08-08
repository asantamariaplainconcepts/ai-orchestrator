import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

/** One Run inside the launcher: executing in a container, or waiting for one of the slots. */
export interface PodRow {
  runId: string;
  projectId: string;
  projectName: string | null;
  vendorStoryId: string;
  triggerLabel: string | null;
  runtime: string | null;
  executing: boolean;
  sightedAt: string;
}

/** One agent runtime's readiness, remedies attached (#279). */
export interface RuntimeRow {
  name: string;
  command: string;
  cliReady: boolean;
  /** The copyable fix for a missing CLI, pinned where the sentences live. */
  installCommand: string;
  /** Null when no credential is configured — the machine's own session, not a failure. */
  credentialSecretName: string | null;
  credentialReady: boolean | null;
  /**
   * Why this runtime's session could not be carried to the machine that runs it (#288). Null when
   * the question does not arise. Kept apart from a missing secret because on a machine you are
   * signed into, "the secret is not stored" is the confusing half of the truth.
   */
  sessionUnavailableReason: string | null;
  /** The copyable command that starts the way out; null exactly when the reason is. */
  sessionUnavailableRemedy: string | null;
}

/**
 * Where the agents actually run, when that is not simply the orchestrator's own process. A
 * habitat that executes agents in sandboxes has preconditions of its own, and a runtime's
 * readiness means nothing until they are met — so this is read before the rows below it.
 */
export interface AgentHostRow {
  /** Named in words: "this process", "a per-Run sandbox on this machine". */
  where: string;
  ready: boolean;
  /** What to do when it is not ready — an action, never a value. */
  remedy: string | null;
}

/** The runtimes of the process that executes Runs — beside the pods, because the operator's
 * question is one: "can this machine run my Automations?". */
export interface RuntimesView {
  hosted: boolean;
  checkedAt: string | null;
  retrySeconds: number;
  runtimes: RuntimeRow[];
  /** Null where the question does not arise: the agents are this process's own children. */
  host: AgentHostRow | null;
}

export interface AgentPodsView {
  /** False means pods do not execute in this process — the panel says so, it never renders an
   * empty machine as the answer. */
  hosted: boolean;
  dockerReady: boolean;
  /** Null while docker itself is unreachable: "is the image built?" has no honest answer then. */
  imagePresent: boolean | null;
  checkedAt: string | null;
  /** The probe's own cadence — the copy restates behaviour instead of promising it. */
  retrySeconds: number;
  maxConcurrentPods: number;
  pods: PodRow[];
  runtimes: RuntimesView;
}

/**
 * The machine's pod host (design review 5b): what today only `docker ps` shows. The panel polls
 * at run cadence; ambient consumers (the environment chip) pass a slower interval — same query
 * key, so the cache is one and the fastest visible consumer sets the pace.
 */
export function usePods(options?: { enabled?: boolean; refetchInterval?: number }) {
  return useQuery({
    queryKey: ["pods"] as const,
    queryFn: () => api.get<AgentPodsView>("/api/pods"),
    refetchInterval: options?.refetchInterval ?? 30_000,
    enabled: options?.enabled ?? true,
  });
}

/**
 * The one definition of "pods cannot take work right now" — the chip, the panel and the queued
 * Run's hint all ask this, so they cannot disagree about what blocked means.
 */
export function podsBlocked(view: AgentPodsView | undefined): boolean {
  return Boolean(view?.hosted && (!view.dockerReady || view.imagePresent === false));
}

/** A runtime that would fail a Run right now: its CLI absent, or its named secret unresolvable. */
export function runtimeNotReady(runtime: RuntimeRow): boolean {
  // A session that cannot reach the machine that runs the agent is not-ready too (#288) — the
  // CLI answers and the store may hold nothing to miss, yet no Run on it can authenticate.
  return (
    !runtime.cliReady ||
    runtime.credentialReady === false ||
    runtime.sessionUnavailableReason !== null
  );
}

/** The runtimes' half of the same question (#279), for the chip's warning dot. */
export function runtimesBlocked(view: AgentPodsView | undefined): boolean {
  return Boolean(
    view?.runtimes.hosted &&
    (view.runtimes.host?.ready === false || view.runtimes.runtimes.some(runtimeNotReady)),
  );
}
