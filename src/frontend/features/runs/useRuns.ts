import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { api } from "@/shared/http/client";
import type { ProjectCost, ProjectPulse, RunView } from "./types";

const runsKey = (projectId: string, vendorStoryId: string | null) =>
  ["runs", projectId, vendorStoryId] as const;

export function useRuns(projectId: string, vendorStoryId: string | null) {
  return useQuery({
    queryKey: runsKey(projectId, vendorStoryId),
    queryFn: () =>
      api.get<RunView[]>(
        vendorStoryId
          ? `/api/projects/${projectId}/runs?vendorStoryId=${encodeURIComponent(vendorStoryId)}`
          : `/api/projects/${projectId}/runs`,
      ),
    // Runs are born asynchronously (poll → event → match), so a static snapshot would miss the
    // one thing this page exists to show. A slow poll is not streaming (DEC-031 — logs are
    // fetched, not streamed); it just keeps the list honest while the page is open.
    refetchInterval: 10_000,
  });
}

/**
 * UC-013 — the decision. Approve re-enqueues the Run for execution; reject ends it. Both
 * re-read the list, because either way the Run's state just changed.
 */
export function useDecideOnPlan(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ runId, approve }: { runId: string; approve: boolean }) =>
      api.post<unknown>(
        `/api/projects/${projectId}/runs/${runId}/${approve ? "approve" : "reject"}`,
        {},
      ),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: ["runs", projectId] }),
  });
}

export interface RunChangedFile {
  path: string;
  status: string;
  additions: number;
  deletions: number;
  patch: string | null;
  patchOmittedReason: "Binary" | "TooLarge" | null;
}

export interface RunChangesView {
  change: { number: number; url: string; files: RunChangedFile[] } | null;
}

/** UC-024 — read live at the change (BR-008), so there is nothing to invalidate. */
export function useRunChanges(projectId: string, runId: string) {
  return useQuery({
    queryKey: ["run-changes", projectId, runId] as const,
    queryFn: () => api.get<RunChangesView>(`/api/projects/${projectId}/runs/${runId}/changes`),
  });
}

/** UC-014 — stop a Run. Terminal Runs refuse, so the button hides once one finishes. */
export function useCancelRun(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (runId: string) =>
      api.post<unknown>(`/api/projects/${projectId}/runs/${runId}/cancel`, {}),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: ["runs", projectId] }),
  });
}

/** #108 — same cadence as the runs list it summarises, so the two cannot visibly disagree. */
export function usePulse(projectId: string) {
  return useQuery({
    queryKey: ["pulse", projectId] as const,
    queryFn: () => api.get<ProjectPulse>(`/api/projects/${projectId}/pulse`),
    refetchInterval: 10_000,
  });
}

export function useProjectCost(projectId: string) {
  return useQuery({
    queryKey: ["project-cost", projectId] as const,
    queryFn: () => api.get<ProjectCost>(`/api/projects/${projectId}/runs/cost`),
    refetchInterval: 15_000,
  });
}

/**
 * Money as money, and unknown as unknown. Since #30's free models a cost of zero is a real
 * reported value, so null must never fall through to "0.00" (design D1).
 */
export function formatCost(cost: number | null): string | null {
  return cost === null
    ? null
    : new Intl.NumberFormat("en", {
        style: "currency",
        currency: "USD",
        minimumFractionDigits: 2,
        maximumFractionDigits: 4,
      }).format(cost);
}

export interface RunLog {
  content: string;
  complete: boolean;
  /** Where the next chunk will be (#144). Used to drop a push that overlaps what the read returned. */
  nextSequence: number;
}

/** Whether a Run has a live preview, and whether this habitat could host one at all. */
export interface RunPreview {
  /** False means previews are not hosted here — not that this Run failed to make one. */
  hosted: boolean;
  available: boolean;
}

/** A pushed frame, carrying where it starts so an overlap can be dropped rather than appended. */
interface LogFrame {
  from: number;
  lines: string[];
}

/**
 * UC-027: polls every 3 seconds while the Run is not done, then stops itself (design D3). The
 * flush interval server-side is 2s, so observed lag stays inside the stated ≤5s budget.
 */
/**
 * The Run's output, followed live. The poll is the guarantee (DEC-050) and the hub is speed on
 * top (#106, design D3): both read the same table, so they cannot disagree, and a hub that never
 * connects costs latency and nothing else.
 */
/**
 * #145 — the decision UC-026 could not express. Stored rather than derived, because nothing in the
 * data tells "nobody has decided yet" from "somebody decided not to re-run".
 */
export function useDismissFailure(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (runId: string) =>
      api.post<unknown>(`/api/projects/${projectId}/runs/${runId}/dismiss`, {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["runs", projectId] });
      // The inbox and its ambient count read the same condition, so both have to refetch.
      void queryClient.invalidateQueries({ queryKey: ["inbox"] });
      void queryClient.invalidateQueries({ queryKey: ["pulse", projectId] });
    },
  });
}

export function useRunLog(projectId: string, runId: string) {
  const queryClient = useQueryClient();
  const [live, setLive] = useState(false);

  const query = useQuery({
    queryKey: ["run-log", projectId, runId],
    queryFn: () => api.get<RunLog>(`/api/projects/${projectId}/runs/${runId}/log`),
    // Still polls while the hub carries the lines, only slowly: it is the reconciliation that
    // makes a dropped frame invisible, and it stops when the Run does.
    refetchInterval: (q) => (q.state.data?.complete ? false : live ? 30_000 : 3_000),
  });

  const complete = query.data?.complete ?? false;

  useEffect(() => {
    // Mock mode has no server to connect to, and a finished Run has nothing left to say.
    //
    // Deliberately not waiting for the first read (#144, design D5): the effect runs on mount, so
    // the subscription is established while the read is in flight. Lines committed in that window
    // arrive as pushes and the handler drops whatever the read also returned.
    if (import.meta.env.MODE === "mock" || complete) return;

    let cancelled = false;
    let connection: HubConnection | undefined;

    void (async () => {
      const { HubConnectionBuilder } = await import("@microsoft/signalr");
      if (cancelled) return;

      connection = new HubConnectionBuilder()
        .withUrl("/hubs/run-log")
        .withAutomaticReconnect()
        .build();

      connection.on("lines", (frame: LogFrame) => {
        queryClient.setQueryData<RunLog>(["run-log", projectId, runId], (current) => {
          // No read has resolved yet: nothing to append to, and the read that follows will carry
          // these lines anyway — this is the subscribe-before-read window (#144, design D5).
          if (current === undefined) return current;

          // Drop what the read already returned. Subscribing first closes the gap where lines
          // committed during the handshake were only picked up by the slow reconciliation poll;
          // the price is an overlap, and this is what pays it.
          const skip = Math.max(0, current.nextSequence - frame.from);
          const fresh = frame.lines.slice(skip);
          if (fresh.length === 0) return current;

          return {
            ...current,
            content: [current.content, ...fresh].join("\n"),
            nextSequence: frame.from + frame.lines.length,
          };
        });
      });

      connection.onclose(() => setLive(false));
      connection.onreconnected(() => void connection?.invoke("Watch", runId));

      try {
        await connection.start();
        await connection.invoke("Watch", runId);
        if (!cancelled) setLive(true);
      } catch {
        // The poll is already covering this; there is nothing for a reader to do about it.
        setLive(false);
      }
    })();

    return () => {
      cancelled = true;
      setLive(false);
      void connection?.stop();
    };
  }, [projectId, runId, complete, queryClient]);

  return query;
}

/**
 * Whether this Run has a preview to look at right now (run-previews). The live output's sibling,
 * and it stops for the same reason: a preview exists while its Run executes and not one moment
 * longer, so a finished Run has nothing left to ask about.
 *
 * `hosted` is the habitat's answer and `available` is this Run's. Kept apart because "previews
 * are not hosted here" and "this Run has no preview" are different sentences — reading the first
 * as the second would make a habitat's limitation look like a Run that failed.
 */
export function useRunPreview(projectId: string, runId: string, runIsTerminal: boolean) {
  return useQuery({
    queryKey: ["run-preview", projectId, runId],
    queryFn: () => api.get<RunPreview>(`/api/projects/${projectId}/runs/${runId}/preview`),
    // Never asked for a Run that is already done: the answer cannot become yes again.
    enabled: !runIsTerminal,
    refetchInterval: (q) => (q.state.data?.available ? 15_000 : 5_000),
  });
}
