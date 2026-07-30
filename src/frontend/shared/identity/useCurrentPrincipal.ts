import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

export type ProjectRole = "Member" | "Admin";

export interface ProjectStanding {
  projectId: string;
  name: string;
  role: ProjectRole;
}

export interface CurrentPrincipal {
  id: string;
  displayName: string;
  /** One entry per project the caller holds a role on (#13, design D5). */
  projects: ProjectStanding[];
}

/**
 * Who the server believes you are (#119). Read rather than assumed: the habitat decides, and a
 * page that decided for itself would be describing its own assumptions.
 *
 * There is no single `role` any more (#13). Roles are per project, so "your role" is not a fact
 * without naming one — a screen that cares asks {@link useProjectRole} about the project it is on.
 */
export function useCurrentPrincipal() {
  return useQuery({
    queryKey: ["me"] as const,
    queryFn: () => api.get<CurrentPrincipal>("/api/me"),
    staleTime: Infinity,
  });
}

/**
 * The caller's role on one project, or undefined while it loads or if they hold none.
 *
 * Undefined is deliberately not treated as Member by callers: a screen must not read "I do not know
 * yet" as a permission. The server refuses either way — this only decides what is worth showing.
 */
export function useProjectRole(projectId: string | undefined): ProjectRole | undefined {
  const me = useCurrentPrincipal();
  if (!projectId) return undefined;
  return me.data?.projects.find((project) => project.projectId === projectId)?.role;
}
