import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

export interface StarterPrompt {
  /** The file's name in this product's own repository. */
  file: string;
  /** The name it takes in a project — not always `file`, since two tiers ship an `implement.md`. */
  saveAs: string;
  purpose: string;
  /** The capability the prompt still needs. Stated up front so its first failure is not confusing. */
  assumes: string;
  content: string;
  /** Where it would go in this project. Null when there is no Connector to resolve a directory. */
  targetPath: string | null;
  /** Null is **unknown**, not absent: nothing looked, because nothing could. */
  alreadyPresent: boolean | null;
}

export interface StarterTier {
  id: string;
  title: string;
  summary: string;
  /** Null for a tier that assumes only the repository; a sentence for one that does not. */
  requires: string | null;
  prompts: StarterPrompt[];
}

export function useStarterPrompts(projectId: string) {
  return useQuery({
    queryKey: ["starter-prompts", projectId] as const,
    queryFn: () => api.get<StarterTier[]>(`/api/projects/${projectId}/starter-prompts`),
  });
}
