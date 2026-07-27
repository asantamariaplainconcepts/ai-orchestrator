import { useState } from "react";
import { t } from "@/shared/i18n";
import { renderStoryMarkdown } from "./markdown";
import { useStoryDocumentContent, useStoryDocuments } from "./useBacklog";

/**
 * UC-023 — the documents written for a Story: whatever markdown its linked change adds or
 * modifies, read live at that change's head (design D3) and rendered through the same
 * sanitiser as the description (D4). Three absences stay three messages (D5).
 */
export function StoryDocuments({
  projectId,
  vendorStoryId,
}: {
  projectId: string;
  vendorStoryId: string;
}) {
  const documents = useStoryDocuments(projectId, vendorStoryId);
  const [selected, setSelected] = useState<string | null>(null);

  const paths = documents.data?.documents ?? [];
  // The first document is the useful default; an explicit choice wins over it.
  const active = selected ?? paths[0] ?? null;
  const content = useStoryDocumentContent(projectId, vendorStoryId, active);

  return (
    <section className="card">
      <div className="card-header">
        <div className="row">
          <h2>{t("story.documents.heading")}</h2>
          {documents.data?.change ? (
            <span className="badge badge-neutral mono">#{documents.data.change.number}</span>
          ) : null}
        </div>
        {documents.data?.change ? (
          <a className="btn" href={documents.data.change.url} target="_blank" rel="noreferrer">
            {t("story.documents.openChange")}
          </a>
        ) : null}
      </div>

      {documents.isPending && <p className="state">{t("story.documents.loading")}</p>}
      {documents.isError && (
        <p className="state state-error" role="alert">
          {t("story.documents.error")}
        </p>
      )}

      {/* No change references the Story, and a change that adds no markdown, are different
          facts with different next actions — never one shrug. */}
      {documents.data && !documents.data.change && (
        <p className="state">{t("story.documents.noChange")}</p>
      )}
      {documents.data?.change && paths.length === 0 && (
        <p className="state">{t("story.documents.noDocuments")}</p>
      )}

      {paths.length > 0 && (
        <div className="stack">
          <div className="row">
            {paths.map((path) => (
              <button
                className={path === active ? "pill pill-ok" : "pill pill-neutral"}
                type="button"
                key={path}
                onClick={() => setSelected(path)}
              >
                {path}
              </button>
            ))}
          </div>

          {content.isPending && <p className="state">{t("story.documents.contentLoading")}</p>}
          {content.isError && (
            <p className="state state-error" role="alert">
              {t("story.documents.contentError")}
            </p>
          )}
          {content.data && (
            <div
              className="prose"
              // Sanitised by renderStoryMarkdown — a repository document is exactly as
              // untrusted as a repository description (design D4).
              dangerouslySetInnerHTML={{ __html: renderStoryMarkdown(content.data.content) }}
            />
          )}
        </div>
      )}
    </section>
  );
}
