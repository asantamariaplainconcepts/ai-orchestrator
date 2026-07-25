import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";

// The build output lands in the host's wwwroot: the SPA is served same-origin in production,
// exactly as the Aspire dev proxy serves it in development. No CORS, no absolute API base URL.
export default defineConfig({
  plugins: [react()],
  // Mirrors the "@/*" paths mapping in tsconfig.json — both resolvers must agree.
  resolve: {
    alias: { "@": fileURLToPath(new URL(".", import.meta.url)) },
  },
  build: {
    outDir: "../root/AiOrchestrator.Server/wwwroot",
    emptyOutDir: true,
  },
  server: {
    port: Number(process.env.PORT ?? 5173),
    strictPort: true,
  },
});
