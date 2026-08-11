import { Navigate, Route, Routes } from "react-router";
import { ProjectScreen } from "@/features/backlog/ProjectScreen";
import { StoryScreen } from "@/features/backlog/StoryScreen";
import { RunScreen } from "@/features/runs/RunScreen";
import { ProjectsScreen } from "@/features/projects/ProjectsScreen";
import { InboxScreen } from "@/features/inbox/InboxScreen";
import { RuntimesScreen } from "@/features/runtimes/RuntimesScreen";
import { SandboxesScreen } from "@/features/sandboxes/SandboxesScreen";

/** Thin route wiring only — screens and their data live in feature slices. */
export function AppRoutes() {
  return (
    <Routes>
      {/* /projects is canonical; serving the same screen at two paths would leave the sidebar's
          active state wrong on one of them. */}
      <Route path="/" element={<Navigate to="/projects" replace />} />
      <Route path="/projects" element={<ProjectsScreen />} />
      <Route path="/inbox" element={<InboxScreen />} />
      {/* Machine-scoped, not project-scoped (design review 5b) — reached from the environment
          chip, which is where "this machine" already lives in the shell. */}
      <Route path="/runtimes" element={<RuntimesScreen />} />
      {/* Machine-scoped for the same reason, and more strictly (#311): the sandbox most worth
          entering is one a killed process left behind, which belongs to no project. */}
      <Route path="/sandboxes" element={<SandboxesScreen />} />
      <Route path="/projects/:projectId" element={<ProjectScreen />} />
      <Route path="/projects/:projectId/stories/:vendorStoryId" element={<StoryScreen />} />
      <Route path="/projects/:projectId/runs/:runId" element={<RunScreen />} />
    </Routes>
  );
}
