import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { ApiError, api } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";

const RUNTIMES = ["ClaudeCodeHeadless", "OpenCode"] as const;

interface RuntimeSettings {
  defaultRuntime: string | null;
  credentialNames: Record<string, string>;
}

/**
 * project-runtimes (#244) — the Project's default runtime and its credential names per runtime.
 * Admin-scoped both ways (BR-009): the read carries the project's billing identity, so the server
 * refuses it to Members and this panel degrades to its refusal rather than pretending emptiness.
 * Names only, never values (BR-010): the inputs name secrets a vault already holds.
 */
export function RuntimeSettingsPanel({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const settings = useQuery({
    queryKey: ["projects", projectId, "runtimes"],
    queryFn: () => api.get<RuntimeSettings>(`/api/projects/${projectId}/runtimes`),
    retry: false,
  });

  const [defaultRuntime, setDefaultRuntime] = useState("");
  const [credentials, setCredentials] = useState<Record<string, string>>({});

  // Seed from what is stored, once it arrives — the form is a full replace (design: the
  // Automation update's own rule), so it must open on the truth.
  useEffect(() => {
    if (settings.data) {
      setDefaultRuntime(settings.data.defaultRuntime ?? "");
      setCredentials(settings.data.credentialNames);
    }
  }, [settings.data]);

  const save = useMutation({
    mutationFn: () =>
      api.put<RuntimeSettings>(`/api/projects/${projectId}/runtimes`, {
        defaultRuntime: defaultRuntime === "" ? null : defaultRuntime,
        credentialNames: Object.fromEntries(
          Object.entries(credentials).filter(([, name]) => name.trim() !== ""),
        ),
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "runtimes"] }),
  });

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("projectRuntimes.heading")}</h2>
          <p className="text-sm text-muted-foreground">{t("projectRuntimes.explainer")}</p>
        </div>

        {settings.isPending && (
          <p className="text-sm text-muted-foreground">{t("projectRuntimes.loading")}</p>
        )}
        {settings.isError && (
          <p className="text-sm text-muted-foreground" role="status">
            {t("projectRuntimes.unavailable")}
          </p>
        )}

        {settings.data ? (
          <>
            <div className="flex flex-col gap-2">
              <Label htmlFor="default-runtime">{t("projectRuntimes.default")}</Label>
              <NativeSelect
                id="default-runtime"
                value={defaultRuntime}
                onChange={(event) => setDefaultRuntime(event.target.value)}
              >
                <option value="">{t("projectRuntimes.deploymentDefault")}</option>
                {RUNTIMES.map((candidate) => (
                  <option key={candidate} value={candidate}>
                    {candidate}
                  </option>
                ))}
              </NativeSelect>
              <p className="text-xs text-muted-foreground">{t("projectRuntimes.defaultHint")}</p>
            </div>

            <div className="flex flex-col gap-3">
              <span className="text-sm font-medium">{t("projectRuntimes.credentials")}</span>
              {RUNTIMES.map((runtime) => (
                <div key={runtime} className="flex flex-col gap-2">
                  <Label htmlFor={`credential-${runtime}`} className="font-mono text-xs">
                    {runtime}
                  </Label>
                  <Input
                    id={`credential-${runtime}`}
                    value={credentials[runtime] ?? ""}
                    onChange={(event) =>
                      setCredentials((current) => ({
                        ...current,
                        [runtime]: event.target.value,
                      }))
                    }
                    placeholder={t("projectRuntimes.credentialPlaceholder")}
                  />
                </div>
              ))}
              <p className="text-xs text-muted-foreground">{t("projectRuntimes.credentialHint")}</p>
            </div>

            {save.isError ? (
              <p className="text-sm text-destructive" role="alert">
                {(save.error instanceof ApiError && save.error.detail) ||
                  t("projectRuntimes.saveFailed")}
              </p>
            ) : null}

            <div>
              <Button type="button" disabled={save.isPending} onClick={() => save.mutate()}>
                {save.isPending ? t("projectRuntimes.saving") : t("projectRuntimes.save")}
              </Button>
            </div>
          </>
        ) : null}
      </CardContent>
    </Card>
  );
}
