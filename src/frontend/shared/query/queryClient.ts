import { QueryClient } from "@tanstack/react-query";

/** Server state lives in TanStack Query — the only server-state mechanism in this app. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000 },
  },
});
