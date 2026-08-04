import { FolderKanban, Inbox, Menu, PanelLeftClose, PanelLeftOpen, X } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link, NavLink } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { useRememberedPreference } from "@/shared/lib/useRememberedPreference";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/shared/ui/sheet";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";
import { ApiError } from "@/shared/http/client";
import { useInbox } from "@/features/inbox/useInbox";
import { EnvironmentChip } from "@/features/pods/EnvironmentChip";
import { useCurrentPrincipal } from "@/shared/identity/useCurrentPrincipal";

/**
 * Loopback in the browser's own vocabulary. The hazard the exposed banner names is "reachable
 * from other machines with no sign-in", and the address bar is the evidence: a page loaded via
 * anything but loopback was, demonstrably, reached over a network interface.
 */
function loopbackHost(hostname: string): boolean {
  return (
    hostname === "localhost" ||
    hostname === "127.0.0.1" ||
    hostname === "::1" ||
    hostname === "[::1]" ||
    hostname.endsWith(".localhost")
  );
}

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

  // The one hazard that still earns a banner (design review 5a): the local-owner posture,
  // reached from another machine. Dismissible for the session — it is a warning about a
  // standing condition, not a gate — and per session rather than forever, because the
  // condition outlives any single acknowledgement.
  const exposed = me.data?.id === "local-owner" && !loopbackHost(window.location.hostname);
  const [exposedDismissed, setExposedDismissed] = useState(
    () => sessionStorage.getItem("aio:exposed-dismissed") === "true",
  );

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
            being truncated into something unreadable. Nothing is lost: it is not a destination.
            The environment chip stays in both widths (5a): the posture must survive the fold,
            which is the same rule that keeps the inbox count on the rail. */}
        <div className={cn("flex flex-col", collapsed ? "gap-2" : "gap-3")}>
          <EnvironmentChip collapsed={collapsed} />
          {collapsed ? null : <UserBlock me={me} />}
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
              <div className="flex h-full flex-col justify-between px-4 pb-6">
                <NavItems waiting={waiting} onNavigate={() => setNavOpen(false)} />
                {/* One identity, two containers (#178): the sheet IS the phone's sidebar, and a
                    phone user who cannot see who they are cannot end a session either — which is
                    exactly what the owner hit on the first mobile sign-in. The environment facts
                    render inline (5a): a popover inside a drawer would be a flyout on a flyout. */}
                <div className="flex flex-col gap-4">
                  <EnvironmentChip inline />
                  <UserBlock me={me} />
                </div>
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

        {/* The banner that remains (design review 5a): not the posture — that lives in the
            environment chip now — but the hazard. Rendered only when this page was reached over
            a non-loopback address while every caller is the administrator. */}
        {exposed && !exposedDismissed ? (
          <div
            role="alert"
            className="flex items-start gap-2 border-b border-destructive/40 bg-destructive/10 px-4 py-2.5 md:px-8"
          >
            <span aria-hidden="true" className="text-sm leading-none text-destructive">
              ⚠
            </span>
            <span className="flex-1 text-sm">{t("env.exposedBanner")}</span>
            <Button
              variant="ghost"
              size="icon-xs"
              type="button"
              aria-label={t("env.exposedDismiss")}
              title={t("env.exposedDismiss")}
              onClick={() => {
                sessionStorage.setItem("aio:exposed-dismissed", "true");
                setExposedDismissed(true);
              }}
            >
              <X className="size-3.5" />
            </Button>
          </div>
        ) : null}

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
 * Who is signed in, and the way out (#12, #178). One component in two containers — the expanded
 * desktop sidebar and the mobile sheet — for the same reason the nav items are: two copies is how
 * the phone lost this block in the first place.
 */
function UserBlock({ me }: { me: ReturnType<typeof useCurrentPrincipal> }) {
  return (
    <div className="flex flex-col gap-0.5 px-3">
      {/* A 401 here is a session that ended mid-use (#12): the initial navigation was challenged
          before the SPA ever loaded, so this state only appears when the cookie expired under an
          open tab. Sign-in is offered, not forced — plain anchors, because both destinations are
          server navigations, not SPA routes. */}
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
          {/* No role here any more (#13): roles are per project, so a single line in the shell
              could only ever be one project's answer or an average of several. The count is the
              honest ambient version — where you have standing, not what it is. */}
          <span className="text-xs text-muted-foreground">
            {me.data
              ? tCount(me.data.projects.length, "shell.user.project", "shell.user.projects")
              : t("shell.user.hint")}
          </span>
          {/* Only a provider session can end; the local owner and the stopgap have nothing to
              sign out of, and their ids are the seam's two fixed sentinels. */}
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
