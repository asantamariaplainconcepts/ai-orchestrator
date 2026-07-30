import { FolderKanban, Inbox, Menu, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link, NavLink } from "react-router";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { useRememberedPreference } from "@/shared/lib/useRememberedPreference";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/shared/ui/sheet";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";
import { ApiError } from "@/shared/http/client";
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
 *
 * From the medium breakpoint up the sidebar collapses to an icon rail (#126). Collapsed is a rail and
 * not a hidden panel for the reason the design contract already gives at phone width: a person cannot
 * navigate from, or be warned by, a panel that is not there. Both widths come from the canonical
 * variables — the shell used to hard-code 16rem, which is 24px narrower than the token it was meant to
 * be honouring.
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
  const [sidebar, setSidebar] = useRememberedPreference<"expanded" | "collapsed">(
    "aio:sidebar",
    "expanded",
    (value): value is "expanded" | "collapsed" => value === "expanded" || value === "collapsed",
  );
  const collapsed = sidebar === "collapsed";

  return (
    <div
      className={cn(
        "min-h-dvh bg-background font-sans text-foreground antialiased md:grid",
        collapsed
          ? "md:grid-cols-[var(--sidebar-w-collapsed)_1fr]"
          : "md:grid-cols-[var(--sidebar-w-expanded)_1fr]",
      )}
    >
      <aside
        className={cn(
          "sticky top-0 hidden h-dvh flex-col justify-between border-r bg-card py-4 md:flex",
          collapsed ? "px-2" : "px-4",
        )}
      >
        <div className="flex flex-col gap-6">
          <div className={cn("flex items-center", collapsed ? "justify-center" : "gap-2 px-3")}>
            {collapsed ? null : (
              <span className="min-w-0 flex-1 truncate text-base font-bold">{t("app.title")}</span>
            )}
            {/* The control the rail is reached by. Icon-only in both states, because a label here
                would be the first thing the collapse was supposed to give back. */}
            <Button
              variant="ghost"
              size="icon"
              type="button"
              onClick={() => setSidebar(collapsed ? "expanded" : "collapsed")}
              aria-label={collapsed ? t("shell.nav.expand") : t("shell.nav.collapse")}
              title={collapsed ? t("shell.nav.expand") : t("shell.nav.collapse")}
            >
              {collapsed ? (
                <PanelLeftOpen className="size-4" />
              ) : (
                <PanelLeftClose className="size-4" />
              )}
            </Button>
          </div>
          <NavItems waiting={waiting} collapsed={collapsed} />
        </div>
        {/* Identity is a label, and a label is what a rail has no room for — so it goes rather than
            being truncated into something unreadable. Nothing is lost: it is not a destination. */}
        {collapsed ? null : (
          <div className="flex flex-col gap-0.5 px-3">
            {/* A 401 here is a session that ended mid-use (#12): the initial navigation was
                challenged before the SPA ever loaded, so this state only appears when the cookie
                expired under an open tab. Sign-in is offered, not forced — plain anchors, because
                both destinations are server navigations, not SPA routes. */}
            {me.error instanceof ApiError && me.error.status === 401 ? (
              <a className="text-sm font-medium underline" href="/auth/signin">
                {t("shell.auth.signIn")}
              </a>
            ) : (
              <>
                {/* What the server says, not what the page assumes (#119). */}
                <span className="text-sm font-medium">
                  {me.data?.displayName ?? t("shell.user.name")}
                </span>
                <span className="text-xs text-muted-foreground">
                  {me.data ? me.data.role : t("shell.user.hint")}
                </span>
                {/* Only a provider session can end; the local owner and the stopgap have nothing
                    to sign out of, and their ids are the seam's two fixed sentinels. */}
                {me.data && me.data.id !== "local-owner" && me.data.id !== "anonymous" ? (
                  <a
                    className="text-xs text-muted-foreground underline hover:text-foreground"
                    href="/auth/signout"
                  >
                    {t("shell.auth.signOut")}
                  </a>
                ) : null}
              </>
            )}
          </div>
        )}
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

/**
 * One nav, three containers now: the desktop sidebar expanded, that sidebar collapsed to a rail, and
 * the mobile sheet. All three render the same items and the same destinations — the rail drops the
 * label from the screen, never the entry (#126, design D2).
 */
function NavItems({
  waiting,
  collapsed = false,
  onNavigate,
}: {
  waiting: number;
  collapsed?: boolean;
  onNavigate?: () => void;
}) {
  // NavLink stamps aria-current="page" when active; the className callback styles exactly that,
  // so active state cannot drift from reality. `end={false}` keeps the projects item active on
  // project detail routes.
  const item = ({ isActive }: { isActive: boolean }) =>
    cn(
      "flex items-center rounded-md py-2 text-sm font-medium transition-colors",
      collapsed ? "justify-center px-2" : "justify-between px-3",
      isActive
        ? "bg-accent text-accent-foreground"
        : "text-muted-foreground hover:bg-muted hover:text-foreground",
    );

  return (
    <nav aria-label={t("shell.nav.section")} className="flex flex-col gap-1">
      {/* The section heading is a label with no destination, so the rail has nothing to keep. */}
      {collapsed ? null : (
        <div className="px-3 pb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
          {t("shell.nav.section")}
        </div>
      )}
      <NavLink
        className={item}
        to="/projects"
        end={false}
        onClick={onNavigate}
        // The name has to exist somewhere once it is off the screen (design D4): here for assistive
        // technology, and as a title for a sighted reader who has not memorised the glyph.
        aria-label={collapsed ? t("shell.nav.projects") : undefined}
        title={collapsed ? t("shell.nav.projects") : undefined}
      >
        <span className="flex items-center gap-2">
          <FolderKanban className="size-4" />
          {collapsed ? null : t("shell.nav.projects")}
        </span>
      </NavLink>
      <NavLink
        className={item}
        to="/inbox"
        onClick={onNavigate}
        aria-label={collapsed ? t("shell.nav.inbox") : undefined}
        title={collapsed ? t("shell.nav.inbox") : undefined}
      >
        <span className="relative flex items-center gap-2">
          <Inbox className="size-4" />
          {collapsed ? null : t("shell.nav.inbox")}
          {/* UC-026's ambient count has to survive the collapse, which is the whole reason this is a
              rail: on the icon when there is no room beside it. */}
          {collapsed && waiting > 0 ? (
            <span
              aria-hidden="true"
              className="absolute -top-1.5 -right-2 min-w-4 rounded-full bg-secondary px-1 text-center text-[10px] leading-4 font-semibold text-secondary-foreground"
            >
              {waiting}
            </span>
          ) : null}
        </span>
        {/* Same query as the page, so they cannot disagree. Zero renders nothing — an empty
            inbox needs no advertising. */}
        {!collapsed && waiting > 0 ? <Badge variant="secondary">{waiting}</Badge> : null}
      </NavLink>
    </nav>
  );
}
