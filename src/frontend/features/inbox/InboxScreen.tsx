import { ExternalLink } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { t, type TranslationKey } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { AppShell } from "@/shared/ui/AppShell";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { RunOnChangeDialog } from "./RunOnChangeDialog";
import { useInbox } from "./useInbox";
import { useInboxChanges } from "./useInboxChanges";
import type { InboxChange, InboxEntry } from "./types";

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
  // Which change a Run is being launched on (run-on-a-pr); null while nobody is launching.
  const [launching, setLaunching] = useState<InboxChange | null>(null);
  const entries = inbox.data ?? [];
  // Its own query on its own cadence (design D2): a vendor read per visible project belongs to
  // the page that shows the answer, never to the badge that polls from everywhere.
  const changes = useInboxChanges();

  return (
    <AppShell crumbs={[{ label: t("shell.nav.inbox") }]} title={t("inbox.heading")}>
      <div className="flex flex-col gap-4">
        {inbox.isPending && <p className="text-sm text-muted-foreground">{t("inbox.loading")}</p>}
        {inbox.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("inbox.error")}
          </p>
        )}

        {/* Empty is the good state and should read like one — not like a missing feature. The
            review queue is judged with it: nothing waits only when both kinds say so. */}
        {inbox.data &&
          entries.length === 0 &&
          changes.data &&
          changes.data.changes.length === 0 &&
          changes.data.refusals.length === 0 && (
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
                          aria-label={`${entry.storyTitle ?? entry.changeTitle ?? `#${entry.vendorStoryId ?? entry.changeNumber}`} — ${t(REASON_KEY[kind])}, ${t("inbox.waitingFor")} ${formatWhen(entry.waitingSince)}`}
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
                              {entry.storyTitle ??
                                entry.changeTitle ??
                                `#${entry.vendorStoryId ?? entry.changeNumber}`}
                            </span>
                            <span className="block text-xs text-muted-foreground">
                              {entry.projectName ? `${entry.projectName} · ` : ""}
                              <span className="font-mono">
                                {entry.changeNumber !== null
                                  ? `PR #${entry.changeNumber}`
                                  : `#${entry.vendorStoryId}`}
                              </span>{" "}
                              · {t("inbox.waitingFor")} {formatWhen(entry.waitingSince)}
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

        {/* The review queue (inbox-open-prs): a different kind of wait, visually distinct on
            purpose — no severity spine, an outline treatment, and the action leaves the product.
            A change is answered on the vendor; a Run wait is answered here. */}
        {changes.data && (changes.data.changes.length > 0 || changes.data.refusals.length > 0) && (
          <section className="flex flex-col gap-1.5">
            <h3 className="text-[11px] font-semibold tracking-wide text-info uppercase">
              {t("inbox.changes.heading")} · {changes.data.changes.length}
            </h3>
            <Card className="overflow-hidden border-dashed py-0">
              <CardContent className="p-0">
                <ul className="divide-y">
                  {changes.data.changes.map((change) => (
                    // Two destinations, so two anchors as siblings, never nested (invalid HTML,
                    // and React refuses to hydrate it): the row's body and the Review chip both
                    // go to the vendor, and the Run link stands beside them.
                    <li
                      key={change.url}
                      className="flex min-h-11 items-center gap-3 px-4 py-3 transition-colors hover:bg-muted"
                    >
                      <a
                        href={change.url}
                        target="_blank"
                        rel="noreferrer"
                        aria-label={`${change.title} — ${t("inbox.changes.review")}`}
                        className="min-w-0 flex-1 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                      >
                        <span className="block truncate text-sm font-medium">{change.title}</span>
                        <span className="block text-xs text-muted-foreground">
                          {change.projectName ? `${change.projectName} · ` : ""}
                          <span className="font-mono">#{change.number}</span> ·{" "}
                          {t("inbox.waitingFor")} {formatWhen(change.createdAt)}
                        </span>
                      </a>
                      {/* The gesture the review usually ends in (run-on-a-pr): type what to
                          change, and the same PR updates. */}
                      <Button
                        variant="outline"
                        size="sm"
                        type="button"
                        className="shrink-0"
                        onClick={() => setLaunching(change)}
                      >
                        {t("inbox.changes.run")}
                      </Button>
                      {/* The product's own work says so, and links to the Run that did it. */}
                      {change.runId ? (
                        <Link
                          to={`/projects/${change.projectId}/runs/${change.runId}`}
                          className="shrink-0 text-xs text-primary underline-offset-2 hover:underline"
                        >
                          {t("inbox.changes.byARun")}
                        </Link>
                      ) : null}
                      <a
                        href={change.url}
                        target="_blank"
                        rel="noreferrer"
                        tabIndex={-1}
                        aria-hidden="true"
                        className="flex shrink-0 items-center gap-1.5 rounded-md border border-input px-3 py-1.5 text-xs font-semibold"
                      >
                        {t("inbox.changes.review")}
                        <ExternalLink className="size-3" aria-hidden="true" />
                      </a>
                    </li>
                  ))}
                </ul>
                {/* Refusals render inside the group they degrade, one line per project — a bad
                    Connector explains itself without blanking anybody else's changes. */}
                {changes.data.refusals.map((refusal) => (
                  <p
                    key={refusal.projectId}
                    role="alert"
                    className="border-t px-4 py-2.5 text-xs text-destructive"
                  >
                    {refusal.projectName ?? refusal.projectId}: {t("inbox.changes.refused")}{" "}
                    {refusal.reason}
                  </p>
                ))}
              </CardContent>
            </Card>
          </section>
        )}

        <RunOnChangeDialog change={launching} onClose={() => setLaunching(null)} />
      </div>
    </AppShell>
  );
}
