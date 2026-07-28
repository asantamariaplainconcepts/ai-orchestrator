import { FolderKanban, Inbox, Menu } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link, NavLink } from "react-router";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/shared/ui/sheet";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";
import { useInbox } from "@/features/inbox/useInbox";
import { useCurrentPrincipal } from "@/shared/identity/useCurrentPrincipal";

export interface Crumb {
  label: string;
  to?: string;
}

/**
 * The one application shell: sidebar navigation plus top bar. Screens render inside it and
 * declare no layout of their own (frontend-architecture spec).
 *
 * First surface on the Platform theme (DEC-051): shadcn primitives and theme tokens only. The
 * root re-establishes background, foreground and the Outfit stack because the document body is
 * still styled by the legacy kit until the last screen migrates. Below the medium breakpoint
 * the sidebar folds into a sheet, and the inbox count stays reachable from the folded bar.
 *
 * The sidebar lists only routes that exist — a nav item for an unbuilt screen is speculative
 * surface. The brand area is text from the catalogue: no logo or imagery enters this
 * repository (DEC-021).
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
  const inbox = useInbox();
  const waiting = inbox.data?.length ?? 0;
  const me = useCurrentPrincipal();
  const [navOpen, setNavOpen] = useState(false);

  return (
    <div className="min-h-dvh bg-background font-sans text-foreground antialiased md:grid md:grid-cols-[16rem_1fr]">
      <aside className="sticky top-0 hidden h-dvh flex-col justify-between border-r bg-card p-4 md:flex">
        <div className="flex flex-col gap-6">
          <div className="px-3 text-base font-bold">{t("app.title")}</div>
          <NavItems waiting={waiting} />
        </div>
        <div className="flex flex-col gap-0.5 px-3">
          {/* What the server says, not what the page assumes (#119). */}
          <span className="text-sm font-medium">
            {me.data?.displayName ?? t("shell.user.name")}
          </span>
          <span className="text-xs text-muted-foreground">
            {me.data ? me.data.role : t("shell.user.hint")}
          </span>
        </div>
      </aside>

      <div className="flex min-w-0 flex-col">
        {/* The folded bar: everything the sidebar offers, reachable at phone width. */}
        <header className="flex items-center gap-2 border-b px-4 py-2 md:hidden">
          <Sheet open={navOpen} onOpenChange={setNavOpen}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" aria-label={t("shell.nav.openMenu")}>
                <Menu className="size-5" />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72">
              <SheetHeader>
                <SheetTitle>{t("app.title")}</SheetTitle>
              </SheetHeader>
              <div className="px-4">
                <NavItems waiting={waiting} onNavigate={() => setNavOpen(false)} />
              </div>
            </SheetContent>
          </Sheet>
          <span className="min-w-0 flex-1 truncate text-sm font-bold">{t("app.title")}</span>
          {/* The ambient count survives the fold (UC-026, design-contract spec). */}
          {waiting > 0 ? (
            <Link
              to="/inbox"
              aria-label={t("shell.nav.inbox")}
              className="flex items-center gap-1 text-muted-foreground"
            >
              <Inbox className="size-4" />
              <Badge variant="secondary">{waiting}</Badge>
            </Link>
          ) : null}
        </header>

        <header className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-4 md:px-8">
          <div className="min-w-0">
            <nav
              className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground"
              aria-label={t("shell.breadcrumbs")}
            >
              {crumbs.map((crumb, index) => (
                <span className="flex items-center gap-1" key={`${crumb.label}-${index}`}>
                  {index > 0 && <span aria-hidden="true">/</span>}
                  {crumb.to ? (
                    <Link className="transition-colors hover:text-foreground" to={crumb.to}>
                      {crumb.label}
                    </Link>
                  ) : (
                    <span>{crumb.label}</span>
                  )}
                </span>
              ))}
            </nav>
            <h1 className="truncate text-xl font-bold">{title}</h1>
          </div>
          <div className="flex items-center gap-2">
            {actions}
            <ThemeToggle />
          </div>
        </header>

        <main className="min-w-0 flex-1 p-4 md:p-8">{children}</main>
      </div>
    </div>
  );
}

/** One nav, two containers: the desktop sidebar and the mobile sheet render the same items. */
function NavItems({ waiting, onNavigate }: { waiting: number; onNavigate?: () => void }) {
  // NavLink stamps aria-current="page" when active; the className callback styles exactly that,
  // so active state cannot drift from reality. `end={false}` keeps the projects item active on
  // project detail routes.
  const item = ({ isActive }: { isActive: boolean }) =>
    cn(
      "flex items-center justify-between rounded-md px-3 py-2 text-sm font-medium transition-colors",
      isActive
        ? "bg-accent text-accent-foreground"
        : "text-muted-foreground hover:bg-muted hover:text-foreground",
    );

  return (
    <nav aria-label={t("shell.nav.section")} className="flex flex-col gap-1">
      <div className="px-3 pb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
        {t("shell.nav.section")}
      </div>
      <NavLink className={item} to="/projects" end={false} onClick={onNavigate}>
        <span className="flex items-center gap-2">
          <FolderKanban className="size-4" />
          {t("shell.nav.projects")}
        </span>
      </NavLink>
      <NavLink className={item} to="/inbox" onClick={onNavigate}>
        <span className="flex items-center gap-2">
          <Inbox className="size-4" />
          {t("shell.nav.inbox")}
        </span>
        {/* Same query as the page, so they cannot disagree. Zero renders nothing — an empty
            inbox needs no advertising. */}
        {waiting > 0 ? <Badge variant="secondary">{waiting}</Badge> : null}
      </NavLink>
    </nav>
  );
}
