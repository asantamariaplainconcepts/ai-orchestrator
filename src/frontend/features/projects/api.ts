import { api } from "@/shared/http/client";
import type { CreateProjectRequest, Project } from "./types";

const projectsPath = "/api/projects";

export const projectsApi = {
  list: () => api.get<Project[]>(projectsPath),
  create: (request: CreateProjectRequest) => api.post<Project>(projectsPath, request),
};
