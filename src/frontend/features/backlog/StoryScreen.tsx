import { Link, useParams } from "react-router";
import { RunsSection } from "@/features/runs/RunsSection";
import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { renderStoryMarkdown } from "./markdown";
import { useStory } from "./useBacklog";

/**
 * UC-022 — one Story, with the requirement that actually describes it. The body is rendered
 * through the sanitising pipeline; nothing here trusts vendor content.
 */
export function StoryScreen() {
  const { projectId = "", vendorStoryId = "" } = useParams();
  const story = useStory(projectId, vendorStoryId);

  const title = story.data?.title ?? t("story.title.fallback");

  return (
    <AppShell
      crumbs={[
        { label: t("shell.crumb.projects"), to: "/projects" },
        { label: t("story.crumb.backlog"), to: `/projects/${projectId}` },
        { label: `#${vendorStoryId}` },
      ]}
      title={title}
    >
      <div className="stack">
        <section className="card">
          <div className="card-header">
            <div className="row">
              <span className="badge badge-neutral mono">#{vendorStoryId}</span>
              {story.data ? (
                <span
                  className={story.data.state === "open" ? "pill pill-ok" : "pill pill-neutral"}
                >
                  {story.data.state}
                </span>
              ) : null}
              {(story.data?.labels ?? []).map((label) => (
                <span className="pill pill-neutral" key={label}>
                  {label}
                </span>
              ))}
            </div>
            <Link className="btn" to={`/projects/${projectId}`}>
              {t("story.backToBacklog")}
            </Link>
          </div>

          {story.isPending && <p className="state">{t("story.loading")}</p>}
          {story.isError && (
            <p className="state state-error" role="alert">
              {t("story.error")}
            </p>
          )}

          {story.data &&
            (story.data.body?.trim() ? (
              <div
                className="prose"
                // Sanitised by renderStoryMarkdown — see design D2. React's escaping is
                // bypassed here on purpose, which is exactly why the sanitiser is not optional.
                dangerouslySetInnerHTML={{ __html: renderStoryMarkdown(story.data.body) }}
              />
            ) : (
              <p className="state">{t("story.noDescription")}</p>
            ))}
        </section>

        {/* The Story's own Runs — the per-Story view UC-021 promised, in its natural home. */}
        <RunsSection
          projectId={projectId}
          storyFilter={vendorStoryId}
          onClearFilter={() => undefined}
        />
      </div>
    </AppShell>
  );
}
