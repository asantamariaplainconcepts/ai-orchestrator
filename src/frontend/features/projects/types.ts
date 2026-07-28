export interface Project {
  id: string;
  name: string;
  /** When it was retired, or null while live (#121). */
  archivedAt: string | null;
}

/** The list states how many are archived even while excluding them. */
export interface ProjectsView {
  projects: Project[];
  archivedCount: number;
}

export interface CreateProjectRequest {
  name: string;
}
