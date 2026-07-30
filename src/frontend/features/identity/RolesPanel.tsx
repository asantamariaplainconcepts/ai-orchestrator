import { useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";
import type { ProjectRole } from "@/shared/identity/useCurrentPrincipal";
import { useAssignProjectRole, useProjectRoles, useRevokeProjectRole } from "./useProjectRoles";

/**
 * UC-002 — who may do what on this project (#13, task 4.2).
 *
 * Rendered only for an Admin, and the reason is not tidiness: the list of everybody this
 * deployment has ever seen is exactly what a Member should not be handed. The server refuses the
 * read too, so this is what the refusal looks like rather than the whole guard.
 *
 * The two bundles come from the server's own enum (DEC-034 fixes them at two). A hard-coded pair
 * here is how a third would arrive in one place and not the other.
 */
export function RolesPanel({ projectId, canManage }: { projectId: string; canManage: boolean }) {
  const roles = useProjectRoles(projectId, canManage);
  const assign = useAssignProjectRole(projectId);
  const revoke = useRevokeProjectRole(projectId);

  const [candidate, setCandidate] = useState("");
  const [bundle, setBundle] = useState<ProjectRole>("Member");

  if (!canManage) return null;

  // A refusal carries its own sentence and that sentence is the useful one — "only an
  // administrator" and "that person has never signed in" are different problems with different
  // fixes, and replacing either with "something went wrong" throws the fix away.
  const failure = [assign.error, revoke.error, roles.error].find(
    (error): error is ApiError => error instanceof ApiError,
  );

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("roles.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("roles.explainer")}</p>
        </div>

        {roles.data?.holders.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("roles.nobody")}</p>
        ) : (
          <ul className="flex flex-col divide-y">
            {roles.data?.holders.map((holder) => (
              <li
                key={holder.identityId}
                className="flex flex-wrap items-center justify-between gap-2 py-2"
              >
                <span className="min-w-0 flex-1 truncate text-sm">{holder.displayName}</span>
                <Badge variant={holder.role === "Admin" ? "default" : "secondary"}>
                  {holder.role}
                </Badge>
                <div className="flex items-center gap-2">
                  {/* Changing is the same call as granting: one intent, one endpoint. */}
                  <NativeSelect
                    aria-label={t("roles.changeFor")}
                    value={holder.role}
                    onChange={(event) =>
                      assign.mutate({
                        identityId: holder.identityId,
                        role: event.target.value as ProjectRole,
                      })
                    }
                  >
                    {roles.data?.bundles.map((value) => (
                      <option key={value} value={value}>
                        {value}
                      </option>
                    ))}
                  </NativeSelect>
                  <Button
                    variant="ghost"
                    size="sm"
                    type="button"
                    onClick={() => revoke.mutate(holder.identityId)}
                    disabled={revoke.isPending}
                  >
                    {t("roles.remove")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {/* Only people the deployment has met (design D6). With nobody to offer, the empty state
            says why rather than showing a select with no options. */}
        {roles.data && roles.data.candidates.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("roles.noCandidates")}</p>
        ) : (
          <form
            className="flex flex-wrap items-end gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              if (!candidate) return;
              assign.mutate({ identityId: candidate, role: bundle });
              setCandidate("");
            }}
          >
            <div className="flex min-w-48 flex-1 flex-col gap-1">
              <Label htmlFor="role-candidate">{t("roles.person")}</Label>
              <NativeSelect
                id="role-candidate"
                value={candidate}
                onChange={(event) => setCandidate(event.target.value)}
              >
                <option value="">{t("roles.choosePerson")}</option>
                {roles.data?.candidates.map((person) => (
                  <option key={person.identityId} value={person.identityId}>
                    {person.displayName}
                  </option>
                ))}
              </NativeSelect>
            </div>
            <div className="flex flex-col gap-1">
              <Label htmlFor="role-bundle">{t("roles.bundle")}</Label>
              <NativeSelect
                id="role-bundle"
                value={bundle}
                onChange={(event) => setBundle(event.target.value as ProjectRole)}
              >
                {roles.data?.bundles.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </NativeSelect>
            </div>
            <Button type="submit" disabled={!candidate || assign.isPending}>
              {t("roles.grant")}
            </Button>
          </form>
        )}

        {failure ? (
          <p className="text-sm text-destructive">{failure.detail ?? t("roles.failed")}</p>
        ) : null}
      </CardContent>
    </Card>
  );
}
