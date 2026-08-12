import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { InFlightView } from "./types";

/**
 * What every visible project has in flight (UC-033), for the shell's projects tree.
 *
 * The refetch interval is deliberately the **same** one `useInbox` already runs: the tree refreshes
 * on the cadence the shell has always polled at, so the sidebar gains no second polling channel and
 * no transport of its own. A Run reaching a terminal state leaves the tree here, by simply no longer
 * being reported — nothing is invalidated and nothing is pushed.
 *
 * One read rather than a query per project: the shell cannot know how many projects a caller has
 * until it has them, and a fan-out would make the sidebar's cost a function of that count, from every
 * page.
 */
export function useInFlight() {
  return useQuery({
    queryKey: ["in-flight"],
    queryFn: () => api.get<InFlightView>("/api/in-flight"),
    refetchInterval: 30_000,
  });
}
