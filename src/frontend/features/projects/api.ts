import { api } from "@/shared/http/client";
import type { CreateProjectRequest, Project, ProjectsView } from "./types";

const projectsPath = "/api/projects";

export const projectsApi = {
  list: (includeArchived = false) =>
    api.get<ProjectsView>(`${projectsPath}${includeArchived ? "?includeArchived=true" : ""}`),
  create: (request: CreateProjectRequest) => api.post<Project>(projectsPath, request),
  archive: (projectId: string, confirmName: string) =>
    api.post<void>(`${projectsPath}/${projectId}/archive`, { confirmName }),
  restore: (projectId: string) => api.post<void>(`${projectsPath}/${projectId}/restore`, {}),
};
