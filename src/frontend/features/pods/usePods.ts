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
