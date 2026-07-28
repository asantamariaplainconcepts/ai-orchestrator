import { QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router";
import { queryClient } from "@/shared/query/queryClient";
import "@/shared/design/design-system.css";
import "@/shared/design/platform.css";
import { applyInitialTheme } from "@/shared/ui/ThemeToggle";
import { AppRoutes } from "./routes";

applyInitialTheme();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
