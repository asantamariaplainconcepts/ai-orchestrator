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
  /**
   * An absolute path on the machine running the orchestrator (#347). Self-host only — a deployment
   * refuses it rather than ignoring it, so a caller can never believe it configured something the
   * habitat dropped.
   */
  folder?: string;
}

/**
 * What a named folder yielded: the coordinates it derived, or the one check that stopped it (#347).
 * Never both, and never a generic failure — the four checks have four different fixes, so collapsing
 * them into "that folder didn't work" would take away the only thing that tells an Admin what to do.
 */
export interface FolderOutcome {
  configured: boolean;
  vendor: string | null;
  owner: string | null;
  repository: string | null;
  codeRepository: string | null;
  /** One of `notADirectory`, `notAGitRepository`, `noOrigin`, `unknownVendor`. */
  failedCheck: string | null;
}

/** The create response carries the folder's outcome where one was named. */
export interface CreatedProject extends Project {
  connector: FolderOutcome | null;
}
