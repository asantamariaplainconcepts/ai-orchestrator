import { useState } from "react";
import { Link } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";
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
    <div className="app-shell">
      <header className="app-header">
        <span className="app-title">{t("app.title")}</span>
        <ThemeToggle />
      </header>

      <main className="app-main">
        <div className="stack">
          <div>
            <div className="row">
              <h1>{t("projects.heading")}</h1>
              {projects.data ? (
                <span className="badge badge-neutral">
                  {tCount(projects.data.length, "projects.count.one", "projects.count.other")}
                </span>
              ) : null}
            </div>
            <p className="card-hint">{t("projects.subtitle")}</p>
          </div>

          <section className="card">
            <form className="row" onSubmit={submit}>
              <div className="field" style={{ flex: 1 }}>
                <label className="label" htmlFor="project-name">
                  {t("projects.create.name")}
                </label>
                <input
                  id="project-name"
                  className="input"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder={t("projects.create.placeholder")}
                />
              </div>
              <button className="btn btn-primary" type="submit" disabled={createProject.isPending}>
                {createProject.isPending
                  ? t("projects.create.pending")
                  : t("projects.create.submit")}
              </button>
            </form>
          </section>

          {/* No heading here: the page's h1 already names this content, and repeating it would
              give screen-reader users two identical landmarks to choose between. */}
          <section className="card" aria-label={t("projects.heading")}>
            {/* All four states, every time — the kit provides each one. */}
            {projects.isPending && <p className="state">{t("projects.loading")}</p>}
            {projects.isError && (
              <p className="state state-error" role="alert">
                {t("projects.error")}
              </p>
            )}
            {projects.data?.length === 0 && <p className="state">{t("projects.empty")}</p>}

            {projects.data && projects.data.length > 0 && (
              <ul className="list">
                {projects.data.map((project) => (
                  <li className="list-row" key={project.id}>
                    <Link className="list-title" to={`/projects/${project.id}`}>
                      {project.name}
                    </Link>
                    <span className="mono">{project.id}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      </main>
    </div>
  );
}
