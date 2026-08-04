import { useEffect, useState } from "react";
import { t } from "@/shared/i18n";
import { CopyLine } from "@/shared/ui/copy-line";
import { useSecretResolves } from "./useBacklog";

/**
 * BR-010's split, said where the name is typed (design review 5d). The quickstart's second step
 * is where self-hosters get lost today: the split lives only in the docs, so people paste the
 * PAT into the name field, or type a name that resolves to nothing and learn it when the
 * Connector fails. This renders the exact line to add, answers "does it resolve?" live through
 * the resolution seam, and catches the pasted-token mistake before it is saved.
 *
 * Rendered only on the self-host posture: the `Secrets__` line is the compose habitat's
 * vocabulary, and on a vaulted deployment it would be a wrong instruction said confidently.
 */
export function SecretNameAside({ projectId, name }: { projectId: string; name: string }) {
  // Checked on idle, like the path check beside it: the resolver may sit in front of a vault,
  // and one round trip per keystroke would be a request storm about half-typed names.
  const [settled, setSettled] = useState(name);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(name), 500);
    return () => clearTimeout(timer);
  }, [name]);

  const resolves = useSecretResolves(projectId, settled);

  // The one mistake this field invites (5d): the value pasted where the name goes. The vendor's
  // own prefixes, or token-shaped length with no separator — names read like names
  // (connector-github-…, hyphenated), tokens read like entropy. A heuristic, so it warns and
  // never blocks; length alone would flag the product's own 49-character derived names.
  const trimmed = name.trim();
  const looksLikeToken =
    /^(gh[pousr]_|github_pat_)/.test(trimmed) || (trimmed.length >= 40 && !trimmed.includes("-"));

  return (
    <div className="flex flex-col gap-1.5">
      <p className="text-xs text-muted-foreground">{t("secret.envExplainer")}</p>
      <CopyLine text={`Secrets__${name.trim() || "<name>"}=<value>`} />
      {looksLikeToken ? (
        <p className="text-xs text-warning" role="alert">
          {t("secret.looksLikeToken")}
        </p>
      ) : null}
      {/* The live verdict, announced politely; an empty field says nothing. */}
      <p aria-live="polite" className="text-xs">
        {!settled.trim() || settled !== name ? null : resolves.isPending ? (
          <span className="text-muted-foreground">{t("secret.checking")}</span>
        ) : resolves.data?.resolves ? (
          <span className="text-success">{t("secret.resolves")}</span>
        ) : resolves.data ? (
          <span className="text-destructive">{t("secret.notYet")}</span>
        ) : null}
      </p>
    </div>
  );
}
