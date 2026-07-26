import type { ReactNode } from "react";
import { Link, NavLink } from "react-router";
import { t } from "@/shared/i18n";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";

export interface Crumb {
  label: string;
  to?: string;
}

/**
 * The one application shell: sidebar navigation plus top bar. Screens render inside it and
 * declare no layout of their own (frontend-architecture spec).
 *
 * The sidebar lists only routes that exist — a nav item for an unbuilt screen is speculative
 * surface, the same rule that keeps unused components out of the kit. The brand area is text
 * from the catalogue: no logo or imagery enters this repository (DEC-021).
 */
export function AppShell({
  crumbs,
  title,
  actions,
  children,
}: {
  crumbs: Crumb[];
  title: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="sidebar-brand">{t("app.title")}</div>

        <nav aria-label={t("shell.nav.section")}>
          <div className="nav-section">{t("shell.nav.section")}</div>
          {/* NavLink stamps aria-current="page" when active — the kit styles exactly that,
              so active state cannot drift from reality. `end` keeps "/" from matching every
              route; the projects item stays active on project detail via the /projects path. */}
          <NavLink className="nav-item" to="/projects" end={false}>
            {t("shell.nav.projects")}
          </NavLink>
        </nav>

        <div className="user-card">
          <span className="list-title">{t("shell.user.name")}</span>
          <span className="card-hint">{t("shell.user.hint")}</span>
        </div>
      </aside>

      <div>
        <header className="topbar">
          <div>
            <nav className="breadcrumbs" aria-label={t("shell.breadcrumbs")}>
              {crumbs.map((crumb, index) => (
                <span className="row" key={`${crumb.label}-${index}`}>
                  {index > 0 && <span aria-hidden="true">/</span>}
                  {crumb.to ? <Link to={crumb.to}>{crumb.label}</Link> : <span>{crumb.label}</span>}
                </span>
              ))}
            </nav>
            <h1 className="page-title">{title}</h1>
          </div>
          <div className="row">
            {actions}
            <ThemeToggle />
          </div>
        </header>

        <main className="shell-content">{children}</main>
      </div>
    </div>
  );
}
