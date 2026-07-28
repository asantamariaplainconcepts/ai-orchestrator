import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { InboxEntry } from "./types";

/**
 * One query feeds both the page and the shell badge (design D1) — they must never disagree.
 * The refetch interval is the ambient part: the count stays honest on whatever page is open.
 */
export function useInbox() {
  return useQuery({
    queryKey: ["inbox"],
    queryFn: () => api.get<InboxEntry[]>("/api/inbox"),
    refetchInterval: 30_000,
  });
}
