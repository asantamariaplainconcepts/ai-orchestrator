import { t } from "@/shared/i18n";
import { useRunChanges } from "./useRuns";

/**
 * UC-024 — what the Agent actually changed, under the Plan it was approved against. The patch
 * is the vendor's own (design D2); colour distinguishes added from removed and nothing else
 * (D5). A file whose patch cannot be shown says why rather than showing a partial diff (D3).
 */
export function RunChanges({ projectId, runId }: { projectId: string; runId: string }) {
  const changes = useRunChanges(projectId, runId);
  const change = changes.data?.change ?? null;

  return (
    <section className="card">
      <div className="card-header">
        <div className="row">
          <h2>{t("run.section.changes")}</h2>
          {change ? <span className="badge badge-neutral mono">#{change.number}</span> : null}
        </div>
        {change ? (
          <a className="btn" href={change.url} target="_blank" rel="noreferrer">
            {t("runs.table.openOutput")}
          </a>
        ) : null}
      </div>

      {changes.isPending && <p className="state">{t("run.changes.loading")}</p>}
      {changes.isError && (
        <p className="state state-error" role="alert">
          {t("run.changes.error")}
        </p>
      )}

      {/* Three absences, three answers: no pull request yet, a change that touched nothing,
          and a read that failed (above). */}
      {changes.data && !change && <p className="state">{t("run.changes.noChange")}</p>}
      {change && change.files.length === 0 && <p className="state">{t("run.changes.noFiles")}</p>}

      {change?.files.map((file) => (
        <div className="diff" key={file.path}>
          <div className="diff-file-header">
            <span className="mono">{file.path}</span>
            <span className="pill pill-neutral">{file.status}</span>
            <span className="diff-added-count">+{file.additions}</span>
            <span className="diff-removed-count">−{file.deletions}</span>
          </div>

          {file.patch ? (
            file.patch.split("\n").map((line, index) => (
              <span
                // The patch has no stable per-line id; its index within this file is the only
                // honest key, and the list never reorders.
                key={`${file.path}:${index}`}
                className={`diff-line ${lineClass(line)}`}
              >
                {line}
              </span>
            ))
          ) : (
            <p className="card-hint">
              {file.patchOmittedReason === "Binary"
                ? t("run.changes.binary")
                : t("run.changes.tooLarge")}
            </p>
          )}
        </div>
      ))}
    </section>
  );
}

function lineClass(line: string): string {
  if (line.startsWith("@@")) return "diff-hunk";
  // "+++"/"---" are file headers, not content — they must not read as a huge add/remove.
  if (line.startsWith("+") && !line.startsWith("+++")) return "diff-added";
  if (line.startsWith("-") && !line.startsWith("---")) return "diff-removed";
  return "";
}
