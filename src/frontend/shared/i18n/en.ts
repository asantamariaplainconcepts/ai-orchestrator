/**
 * The English copy catalog — the single home for user-facing text (DEC-021).
 * Adding a key here is what makes it usable; the lint gate rejects literals in JSX.
 */
export const en = {
  "app.title": "AI Orchestrator",
  "projects.heading": "Projects",
  "projects.subtitle": "Each project connects a backlog and runs its Automations.",
  // Empty states name the absence, then the next action (content fundamentals).
  "projects.empty": "No projects yet. Create one to connect a backlog.",
  "projects.create.name": "Project name",
  "projects.create.placeholder": "Phoenix",
  "projects.create.submit": "Create project",
  "projects.create.pending": "Creating…",
  "projects.loading": "Loading projects…",
  "projects.error": "Could not load projects.",
  "projects.count": "projects",
  "theme.toggle": "Switch theme",
} as const;

export type TranslationKey = keyof typeof en;
