import { useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import {
  useInstallStarter,
  useStarterPrompts,
  type StarterPrompt,
  type StarterTier,
} from "./useStarterPrompts";

/**
 * #190 — the starter set, offered against this project. The offer itself still writes nothing;
 * since #214 each starter also carries an explicit *Install* — one bounded git write to a branch,
 * opened as a draft PR a human merges. The default branch is never touched from here.
 */
export function StarterPromptsSection({ projectId }: { projectId: string }) {
  const starters = useStarterPrompts(projectId);

  if (!starters.data?.length) return null;

  return (
    <Card>
      <CardContent className="flex flex-col gap-5">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("starters.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("starters.explainer")}</p>
        </div>

        {starters.data.map((tier) => (
          <Tier key={tier.id} tier={tier} projectId={projectId} />
        ))}
      </CardContent>
    </Card>
  );
}

function Tier({ tier, projectId }: { tier: StarterTier; projectId: string }) {
  return (
    <section className="flex flex-col gap-3">
      <div className="flex flex-col gap-1">
        <h3 className="text-sm font-semibold">{tier.title}</h3>
        <p className="text-sm text-muted-foreground">{tier.summary}</p>
        {/* The prerequisite is read before it is needed, not learned from an agent that cannot find
            a file — the whole reason the set is tiered at all (design D2). */}
        {tier.requires ? (
          <p className="text-sm text-muted-foreground">
            <span className="font-medium text-foreground">{t("starters.requires")}</span>{" "}
            {tier.requires}
          </p>
        ) : null}
      </div>

      <ul className="flex flex-col gap-2">
        {tier.prompts.map((prompt) => (
          <Starter key={`${tier.id}/${prompt.file}`} prompt={prompt} projectId={projectId} />
        ))}
      </ul>
    </section>
  );
}

function Starter({ prompt, projectId }: { prompt: StarterPrompt; projectId: string }) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const install = useInstallStarter(projectId);

  async function copy() {
    await navigator.clipboard.writeText(prompt.content);
    setCopied(true);
  }

  // Install is offered only where it can act: a Connector resolved a target path, and the file is
  // not already there (an existing file always wins — reported above, refused server-side too).
  const installable = prompt.targetPath !== null && prompt.alreadyPresent !== true;

  return (
    <li className="flex flex-col gap-2 rounded-md border border-border p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-sm">{prompt.saveAs}</span>
            {/* Already there: reported, never prevented. The product is not the thing stopping an
                overwrite — the Admin is, and they can only decide if they are told. */}
            {prompt.alreadyPresent === true ? (
              <Badge variant="secondary">{t("starters.alreadyPresent")}</Badge>
            ) : null}
          </div>
          <p className="text-sm text-muted-foreground">{prompt.purpose}</p>
          <p className="text-xs text-muted-foreground">
            <span className="font-medium">{t("starters.assumes")}</span> {prompt.assumes}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button type="button" variant="ghost" size="sm" onClick={() => setOpen(!open)}>
            {open ? t("starters.hide") : t("starters.show")}
          </Button>
          <Button type="button" variant="secondary" size="sm" onClick={() => void copy()}>
            {copied ? t("starters.copied") : t("starters.copy")}
          </Button>
          {installable ? (
            <Button
              type="button"
              size="sm"
              disabled={install.isPending}
              onClick={() => install.mutate(prompt.saveAs)}
            >
              {install.isPending ? t("starters.installing") : t("starters.install")}
            </Button>
          ) : null}
        </div>
      </div>

      {/* Where it goes in this project, resolved by the same rule a Run resolves it with. Null is
          unknown rather than absent — nothing looked, because there is no Connector yet. */}
      <p className="text-xs text-muted-foreground">
        {prompt.targetPath ? (
          <>
            {t("starters.saveTo")} <span className="font-mono">{prompt.targetPath}</span>
          </>
        ) : (
          t("starters.pathUnknown")
        )}
      </p>

      {/* The install's outcome, where the click happened: the draft PR to go review, or the
          stage-named refusal verbatim. */}
      {install.data ? (
        <p className="text-sm" role="status">
          {t("starters.installed")}{" "}
          <a
            className="underline underline-offset-2"
            href={install.data.url}
            target="_blank"
            rel="noreferrer"
          >
            {install.data.url}
          </a>
        </p>
      ) : null}
      {install.isError ? (
        <p className="text-sm text-destructive" role="alert">
          {/* The detail is the answer when the API sent one — "already exists at <path>" tells
              the Admin what to do; a generic sentence would not. */}
          {install.error instanceof ApiError && install.error.detail
            ? install.error.detail
            : t("starters.installFailed")}
        </p>
      ) : null}

      {open ? (
        <pre className="max-h-96 overflow-auto rounded-md bg-muted p-3 text-xs whitespace-pre-wrap">
          {prompt.content}
        </pre>
      ) : null}
    </li>
  );
}
