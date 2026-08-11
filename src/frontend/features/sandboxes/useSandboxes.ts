import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

/** One of this machine's sandboxes (#311). */
export interface SandboxRow {
  name: string;
  /**
   * The sandbox runtime's own word — `running`, `stopped`. Carried rather than reduced to a boolean
   * because entering a stopped sandbox starts it, and the surface has to say so before it happens.
   */
  status: string;
  /**
   * The Run using it, where one is. Null is a fact rather than a missing lookup: the ledger holds
   * only this process's Runs, so a sandbox an earlier process abandoned really belongs to none.
   */
  runId: string | null;
  workspace: string | null;
}

/**
 * This machine's sandboxes, and the two answers that decide whether any are shown.
 *
 * `hosted` is the habitat's (ADR-0021 permits a terminal in self-host and refuses it in a
 * deployment) and `permitted` is the caller's. They are separate because each has its own remedy:
 * asking for access does not help a habitat that hosts nothing.
 */
export interface SandboxesView {
  hosted: boolean;
  permitted: boolean;
  sandboxes: SandboxRow[];
}

/** The sandboxes of the machine that executes Runs (#311). */
export function useSandboxes(options?: { refetchInterval?: number }) {
  return useQuery({
    queryKey: ["sandboxes"] as const,
    queryFn: () => api.get<SandboxesView>("/api/runs/sandboxes"),
    // A sandbox appears and disappears on the machine's own schedule, not a reader's — the reaper
    // takes probe sandboxes every thirty seconds — so the list is re-read rather than trusted.
    refetchInterval: options?.refetchInterval ?? 10_000,
  });
}

/** Whether entering this sandbox will start it, which the surface must say before it does. */
export function sandboxIsStopped(sandbox: SandboxRow): boolean {
  return sandbox.status !== "running";
}
