import { useState } from "react";
import { t } from "@/shared/i18n";
import { useCreateProject, useProjects } from "./useProjects";

export function ProjectsScreen() {
  const [name, setName] = useState("");
  const projects = useProjects();
  const createProject = useCreateProject();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!name.trim()) return;
    createProject.mutate({ name }, { onSuccess: () => setName("") });
  }

  return (
    <main>
      <h1>{t("projects.heading")}</h1>

      <form onSubmit={submit}>
        <label htmlFor="project-name">{t("projects.create.name")}</label>
        <input
          id="project-name"
          value={name}
          onChange={(event) => setName(event.target.value)}
          aria-label={t("projects.create.name")}
        />
        <button type="submit" disabled={createProject.isPending}>
          {createProject.isPending ? t("projects.create.pending") : t("projects.create.submit")}
        </button>
      </form>

      {projects.isPending && <p>{t("projects.loading")}</p>}
      {projects.isError && <p role="alert">{t("projects.error")}</p>}
      {projects.data?.length === 0 && <p>{t("projects.empty")}</p>}

      <ul>
        {projects.data?.map((project) => (
          <li key={project.id}>{project.name}</li>
        ))}
      </ul>
    </main>
  );
}
