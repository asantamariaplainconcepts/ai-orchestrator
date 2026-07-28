import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

export interface CurrentPrincipal {
  id: string;
  displayName: string;
  role: "Member" | "Admin";
}

/**
 * Who the server believes you are (#119). Read rather than assumed: the habitat decides, and a
 * page that decided for itself would be describing its own assumptions.
 */
export function useCurrentPrincipal() {
  return useQuery({
    queryKey: ["me"] as const,
    queryFn: () => api.get<CurrentPrincipal>("/api/me"),
    staleTime: Infinity,
  });
}
