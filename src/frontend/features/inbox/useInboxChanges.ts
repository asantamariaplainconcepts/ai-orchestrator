import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { InboxChanges } from "./types";

/**
 * Deliberately its own query, not part of useInbox (design D1/D2): the inbox query feeds the
 * shell badge from every page every 30 s, and this one costs a vendor read per visible project.
 * Mounted only by the Inbox screen, on a slower cadence — the reads happen only while somebody
 * is actually looking at the review queue.
 */
export function useInboxChanges() {
  return useQuery({
    queryKey: ["inbox", "changes"],
    queryFn: () => api.get<InboxChanges>("/api/inbox/changes"),
    refetchInterval: 120_000,
  });
}
