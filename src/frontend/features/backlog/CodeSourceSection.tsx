import { useEffect, useRef } from "react";
import { useConnectorHealth } from "@/features/projects/useConnectorHealth";
import { useProjects } from "@/features/projects/useProjects";
import { t } from "@/shared/i18n";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { useValidateLocalPath } from "./useBacklog";

/**
 * Mock 3a's lower half (#211): the Repository / Local folder choice, the typed path with live
 * validation, the recents, and the constraint callout. Rendered only where the code-source
 * surface exists — the caller probes; this component never has to ask "am I on cloud?".
 *
 * The backlog vendor and the code source separate (local-code-source spec): Stories still come
 * from issues; only where the Agent's working copy comes from changes here.
 */
export function CodeSourceSection({
  projectId,
  codeSource,
  onCodeSource,
  localPath,
  onLocalPath,
  localSetupCommand,
  onLocalSetupCommand,
  localFolderReason,
}: {
  projectId: string;
  codeSource: string;
  onCodeSource: (value: string) => void;
  localPath: string;
  onLocalPath: (value: string) => void;
  localSetupCommand: string;
  onLocalSetupCommand: (value: string) => void;
  /** Non-null where the habitat declared the Local locus unavailable (#247). */
  localFolderReason?: string | null;
}) {
  const validate = useValidateLocalPath(projectId);
  const health = useConnectorHealth();
  const projects = useProjects(true);
  const local = codeSource === "LocalFolder";

  // Validated live on idle (debounced): the server owns the disk, so every check is a round
  // trip, and one per keystroke would be a request storm about half-typed paths.
  const debounce = useRef<ReturnType<typeof setTimeout>>(undefined);
  useEffect(() => {
    if (!local || !localPath.trim()) return;
    clearTimeout(debounce.current);
    const timer = setTimeout(() => validate.mutate(localPath.trim()), 500);
    debounce.current = timer;
    return () => clearTimeout(timer);
    // validate is a stable mutation handle; listing it would re-arm the timer on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [local, localPath]);

  // The habitat withheld the locus (#247): the radiogroup never renders a choice that cannot
  // succeed here, and the declared sentence stands where it would have been — "no pipeline
  // here" and "this control is broken" must not look identical. After the hooks, because a
  // hook behind an early return is a different component on every render.
  if (localFolderReason) {
    return (
      <div className="flex flex-col gap-2">
        <span className="text-sm font-medium">{t("connector.codeSource")}</span>
        <p className="text-xs text-muted-foreground" role="note">
          {t("connector.codeSource.unavailable")} {localFolderReason}
        </p>
      </div>
    );
  }

  /**
   * Recent folders: paths other visible projects already configured (mock 3a #4). Derived from
   * the same list the projects screen reads — no new storage, and never a path the caller
   * cannot already see.
   */
  const recents = (health.data ?? [])
    .filter((entry) => entry.localPath && entry.projectId !== projectId)
    .map((entry) => ({
      path: entry.localPath!,
      usedBy:
        projects.data?.projects.find((candidate) => candidate.id === entry.projectId)?.name ?? null,
    }));

  const result = validate.data;
  const failing = result
    ? !result.isDirectory
      ? t("connector.codeSource.notADirectory")
      : !result.isGitRepository
        ? t("connector.codeSource.notAGitRepository")
        : null
    : null;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <span className="text-sm font-medium">{t("connector.codeSource")}</span>
        {/* A radiogroup, not a select: two options whose difference is the whole point (mock 3a). */}
        <div
          role="radiogroup"
          aria-label={t("connector.codeSource")}
          className="flex w-fit rounded-md bg-muted p-0.5"
        >
          {(["Repository", "LocalFolder"] as const).map((option) => (
            <button
              key={option}
              type="button"
              role="radio"
              aria-checked={codeSource === option}
              onClick={() => onCodeSource(option)}
              className={
                codeSource === option
                  ? "rounded-sm bg-background px-3.5 py-1.5 text-sm font-semibold shadow-sm"
                  : "rounded-sm px-3.5 py-1.5 text-sm text-muted-foreground"
              }
            >
              {option === "Repository"
                ? t("connector.codeSource.repository")
                : t("connector.codeSource.localFolder")}
            </button>
          ))}
        </div>
        <p className="text-xs text-muted-foreground">{t("connector.codeSource.hint")}</p>
      </div>

      {local ? (
        <>
          <div className="flex flex-col gap-2">
            <Label htmlFor="local-path">{t("connector.codeSource.folder")}</Label>
            <Input
              id="local-path"
              className="font-mono"
              value={localPath}
              onChange={(event) => onLocalPath(event.target.value)}
              placeholder={t("connector.codeSource.folderPlaceholder")}
              aria-describedby="local-path-validation"
            />
            {/* The live verdict, tied to the input and announced politely (spec: aria-live).
                Loading, invalid-with-the-failing-check, and the valid summary are the three
                states; empty input simply says nothing. */}
            <p id="local-path-validation" aria-live="polite" className="text-xs">
              {!localPath.trim() ? null : validate.isPending ? (
                <span className="text-muted-foreground">
                  {t("connector.codeSource.validating")}
                </span>
              ) : failing ? (
                <span className="text-destructive">{failing}</span>
              ) : result ? (
                <span className="text-success">
                  {t("connector.codeSource.valid")}{" "}
                  <span className="font-mono">{result.branch ?? "?"}</span>
                  {" · "}
                  {result.isClean === false
                    ? t("connector.codeSource.dirtyTree")
                    : t("connector.codeSource.cleanTree")}
                </span>
              ) : null}
            </p>
          </div>

          {/* A Run works in its own checkout of that folder, and a fresh checkout has no installed
              dependencies (#332). Beside the path, because it describes the same folder — and
              optional, because a checkout that needs no preparation is not misconfigured. */}
          <div className="flex flex-col gap-2">
            <Label htmlFor="local-setup-command">{t("connector.codeSource.setup")}</Label>
            <Input
              id="local-setup-command"
              className="font-mono"
              value={localSetupCommand}
              onChange={(event) => onLocalSetupCommand(event.target.value)}
              placeholder={t("connector.codeSource.setupPlaceholder")}
              aria-describedby="local-setup-hint"
            />
            <p id="local-setup-hint" className="text-xs text-muted-foreground">
              {t("connector.codeSource.setupHint")}
            </p>
          </div>

          {recents.length > 0 ? (
            <div className="flex flex-col rounded-md border border-border">
              <span className="px-3 pt-2 pb-1 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">
                {t("connector.codeSource.recent")}
              </span>
              {recents.map((entry) => (
                // Buttons, ≥44px touch targets (spec: not a listbox re-implementation).
                <button
                  key={entry.path}
                  type="button"
                  onClick={() => onLocalPath(entry.path)}
                  className="flex min-h-11 items-center justify-between gap-2 px-3 py-2 text-left font-mono text-sm hover:bg-muted"
                >
                  {entry.path}
                  {entry.usedBy ? (
                    <span className="font-sans text-xs text-muted-foreground">
                      {t("connector.codeSource.usedBy")} {entry.usedBy}
                    </span>
                  ) : null}
                </button>
              ))}
            </div>
          ) : null}

          {/* The physical constraint, as text beside an icon — never colour alone (spec). */}
          <div
            className="border-warning/40 bg-warning/10 flex items-start gap-2 rounded-md border p-3"
            role="note"
          >
            <span aria-hidden="true" className="text-warning mt-0.5 text-sm leading-none">
              ⚠
            </span>
            <span className="text-sm">{t("connector.codeSource.sandboxConstraint")}</span>
          </div>
        </>
      ) : null}
    </div>
  );
}
