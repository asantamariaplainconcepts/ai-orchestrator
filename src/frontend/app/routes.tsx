import { Route, Routes } from "react-router";
import { ProjectScreen } from "@/features/backlog/ProjectScreen";
import { ProjectsScreen } from "@/features/projects/ProjectsScreen";

/** Thin route wiring only — screens and their data live in feature slices. */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<ProjectsScreen />} />
      <Route path="/projects" element={<ProjectsScreen />} />
      <Route path="/projects/:projectId" element={<ProjectScreen />} />
    </Routes>
  );
}
