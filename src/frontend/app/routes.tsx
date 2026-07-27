import { Navigate, Route, Routes } from "react-router";
import { ProjectScreen } from "@/features/backlog/ProjectScreen";
import { StoryScreen } from "@/features/backlog/StoryScreen";
import { ProjectsScreen } from "@/features/projects/ProjectsScreen";

/** Thin route wiring only — screens and their data live in feature slices. */
export function AppRoutes() {
  return (
    <Routes>
      {/* /projects is canonical; serving the same screen at two paths would leave the sidebar's
          active state wrong on one of them. */}
      <Route path="/" element={<Navigate to="/projects" replace />} />
      <Route path="/projects" element={<ProjectsScreen />} />
      <Route path="/projects/:projectId" element={<ProjectScreen />} />
      <Route path="/projects/:projectId/stories/:vendorStoryId" element={<StoryScreen />} />
    </Routes>
  );
}
