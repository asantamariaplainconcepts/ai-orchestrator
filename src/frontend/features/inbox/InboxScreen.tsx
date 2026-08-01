import { Link } from "react-router";
import { t, type TranslationKey } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { AppShell } from "@/shared/ui/AppShell";
import { Card, CardContent } from "@/shared/ui/card";
import { useInbox } from "./useInbox";
import type { InboxEntry } from "./types";

/** Relative for recency, absolute past a day — the content fundamentals' rule. */
function formatWhen(iso: string): string {
  const then = new Date(iso);
  const minutes = Math.round((Date.now() - then.getTime()) / 60000);
  if (minutes < 1) return t("inbox.justNow");
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return then.toLocaleDateString();
}

// `as const` keeps the values as literal types, which is what the typed catalogue demands —
// a plain Record<_, string> would erase exactly the guarantee the catalogue exists to give.
const REASON_KEY = {
  approval: "inbox.reason.approval",
  input: "inbox.reason.input",
  failure: "inbox.reason.failure",
} as const satisfies Record<InboxEntry["waitingFor"], TranslationKey>;

/** The verb the row leads with — what acting on it will do, not what state it is in. */
const ACTION_KEY = {
  approval: "inbox.action.approval",
  input: "inbox.action.input",
  failure: "inbox.action.failure",
} as const satisfies Record<InboxEntry["waitingFor"], TranslationKey>;

/** Triage order: the cheap decision first, the failure last — never arrival order. */
const GROUPS = ["approval", "input", "failure"] as const;

/**
 * UC-026 — everything waiting on a human, across projects. Each entry links to its Run, where
 * the relevant action lives; the inbox itself acts on nothing (v1, by design).
 *
 * A triage list grouped by need since the 2026-08 design review (DEC-051): each row names its
 * project — the list is cross-project, so "#491" alone answers nothing — and carries a severity
 * spine plus a verb-first action chip, so the kind of wait is never colour alone.
 */
export function InboxScreen() {
  const inbox = useInbox();
  const entries = inbox.data ?? [];

  return (
    <AppShell crumbs={[{ label: t("shell.nav.inbox") }]} title={t("inbox.heading")}>
      <div className="flex flex-col gap-4">
        {inbox.isPending && <p className="text-sm text-muted-foreground">{t("inbox.loading")}</p>}
        {inbox.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("inbox.error")}
          </p>
        )}

        {/* Empty is the good state and should read like one — not like a missing feature. */}
        {inbox.data && entries.length === 0 && (
          <p className="text-sm text-muted-foreground">{t("inbox.empty")}</p>
        )}

        {GROUPS.map((kind) => {
          const group = entries.filter((entry) => entry.waitingFor === kind);
          if (group.length === 0) return null;

          return (
            <section key={kind} className="flex flex-col gap-1.5">
              <h3
                className={cn(
                  "text-[11px] font-semibold tracking-wide uppercase",
                  kind === "failure" ? "text-destructive" : "text-warning",
                )}
              >
                {t(REASON_KEY[kind])} · {group.length}
              </h3>
              <Card className="overflow-hidden py-0">
                <CardContent className="p-0">
                  <ul className="divide-y">
                    {group.map((entry) => (
                      <li key={entry.runId}>
                        <Link
                          to={`/projects/${entry.projectId}/runs/${entry.runId}`}
                          aria-label={`${entry.storyTitle ?? `#${entry.vendorStoryId}`} — ${t(REASON_KEY[kind])}, ${t("inbox.waitingFor")} ${formatWhen(entry.waitingSince)}`}
                          className="flex min-h-11 items-center gap-3 px-4 py-3 transition-colors outline-none hover:bg-muted focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:ring-inset"
                        >
                          {/* The severity spine — with the heading and the verb, one of three
                              signals, so colour never stands alone. */}
                          <span
                            aria-hidden="true"
                            className={cn(
                              "w-1 shrink-0 self-stretch rounded-full",
                              kind === "failure" ? "bg-destructive" : "bg-warning",
                            )}
                          />
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-sm font-medium">
                              {entry.storyTitle ?? `#${entry.vendorStoryId}`}
                            </span>
                            <span className="block text-xs text-muted-foreground">
                              {entry.projectName ? `${entry.projectName} · ` : ""}
                              <span className="font-mono">#{entry.vendorStoryId}</span> ·{" "}
                              {t("inbox.waitingFor")} {formatWhen(entry.waitingSince)}
                            </span>
                          </span>
                          {/* Visual affordance only — the row is the link, the chip names what
                              following it does. */}
                          <span className="shrink-0 rounded-md bg-accent px-3 py-1.5 text-xs font-semibold text-accent-foreground">
                            {t(ACTION_KEY[kind])}
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </CardContent>
              </Card>
            </section>
          );
        })}
      </div>
    </AppShell>
  );
}
