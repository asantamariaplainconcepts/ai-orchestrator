import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

/** One row per configured Connector — the projects list joins client-side (#97). */
export interface ConnectorHealth {
  projectId: string;
  vendor: string;
  lastSyncedAt: string | null;
  lastFailure: string | null;
  lastFailureAt: string | null;
  /** Repository | LocalFolder (#211): the list's Local badge and the form's recents read this. */
  codeSource: string;
  localPath: string | null;
}

export type HealthState = "healthy" | "failing" | "neverSynced" | "notConfigured";

export function healthOf(connector: ConnectorHealth | undefined): HealthState {
  if (!connector) return "notConfigured";
  if (connector.lastFailure) return "failing";
  if (!connector.lastSyncedAt) return "neverSynced";
  return "healthy";
}

export function useConnectorHealth() {
  return useQuery({
    queryKey: ["connectors"],
    queryFn: () => api.get<ConnectorHealth[]>("/api/connectors"),
    refetchInterval: 30_000,
  });
}
