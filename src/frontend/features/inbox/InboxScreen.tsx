import { Link } from "react-router";
import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
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
} as const satisfies Record<InboxEntry["waitingFor"], string>;

/**
 * UC-026 — everything waiting on a human, across projects. Each entry links to its Run, where
 * the relevant action lives; the inbox itself acts on nothing (v1, by design).
 */
export function InboxScreen() {
  const inbox = useInbox();
  const entries = inbox.data ?? [];

  return (
    <AppShell crumbs={[{ label: t("shell.nav.inbox") }]} title={t("inbox.heading")}>
      <section className="card">
        <div className="card-header">
          <div className="row">
            <h2>{t("inbox.heading")}</h2>
            {inbox.data ? <span className="badge badge-neutral">{entries.length}</span> : null}
          </div>
        </div>

        {inbox.isPending && <p className="state">{t("inbox.loading")}</p>}
        {inbox.isError && (
          <p className="state state-error" role="alert">
            {t("inbox.error")}
          </p>
        )}

        {/* Empty is the good state and should read like one — not like a missing feature. */}
        {inbox.data && entries.length === 0 && <p className="state">{t("inbox.empty")}</p>}

        {entries.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th>{t("inbox.table.story")}</th>
                <th>{t("inbox.table.reason")}</th>
                <th>{t("inbox.table.waiting")}</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={entry.runId}>
                  <td>
                    <Link to={`/projects/${entry.projectId}/runs/${entry.runId}`}>
                      {entry.storyTitle ?? `#${entry.vendorStoryId}`}
                    </Link>
                  </td>
                  <td>
                    <span
                      className={
                        entry.waitingFor === "failure" ? "pill pill-danger" : "pill pill-neutral"
                      }
                    >
                      {t(REASON_KEY[entry.waitingFor])}
                    </span>
                  </td>
                  <td className="card-hint">{formatWhen(entry.waitingSince)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </AppShell>
  );
}
