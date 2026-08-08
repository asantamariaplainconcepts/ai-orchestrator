import { TriangleAlert } from "lucide-react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";
import { CopyLine } from "@/shared/ui/copy-line";
import { type AgentHostRow, runtimeNotReady, type RuntimeRow, type RuntimesView } from "./usePods";

/**
 * The agent runtimes of the process that executes Runs (#279) — the pods panel's sibling
 * question: docker can be ready and the machine still unable to run an Automation, because the
 * runtime's CLI is absent or its named secret resolves to nothing. Each not-ready row carries
 * its remedy, and the remedy for a missing CLI is copyable — the same command the failure
 * reason names, because both read the one place the sentences live.
 *
 * Where those runtimes live is now a habitat's choice, decided when the server starts, so the
 * host is named above the rows: the rows describe THAT machine, and reading them without
 * knowing which one would be reading them about the wrong place.
 */
export function RuntimesReadiness({ view }: { view: RuntimesView }) {
  if (!view.hosted) return null;

  return (
    <div className="flex flex-col gap-2">
      <span className="flex items-baseline gap-2">
        <h2 className="text-sm font-semibold">{t("runtimes.heading")}</h2>
        <span className="text-xs text-muted-foreground">{t("pods.onThisMachine")}</span>
      </span>
      <Card className="gap-0 py-0">
        {view.host ? <AgentHostRowItem host={view.host} /> : null}
        {view.runtimes.length === 0 ? (
          <p className="px-4 py-3 text-sm text-muted-foreground">{t("runtimes.empty")}</p>
        ) : (
          <ul className="flex flex-col">
            {view.runtimes.map((runtime) => (
              <RuntimeRowItem key={runtime.name} runtime={runtime} />
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

/**
 * The machine the runtimes below describe. Its state is not decoration: while it cannot answer,
 * every row under it is moot, so its remedy is the one to apply first — said in that order.
 */
function AgentHostRowItem({ host }: { host: AgentHostRow }) {
  return (
    <div className="flex flex-col gap-1.5 border-b bg-muted/40 px-3.5 py-2.5">
      <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
        {host.ready ? (
          <Badge variant="outline" className="border-success/40 bg-success/10 text-success">
            {t("runtimes.ready")}
          </Badge>
        ) : (
          <Badge variant="outline" className="border-warning/40 bg-warning/15 text-warning">
            <TriangleAlert aria-hidden="true" className="size-3" />
            {t("runtimes.hostNotReady")}
          </Badge>
        )}
        <span className="min-w-0 flex-1 text-xs">
          <span className="text-muted-foreground">{t("runtimes.hostLabel")}</span>{" "}
          <span className="font-semibold">{host.where}</span>
        </span>
      </span>
      {host.remedy ? (
        <span className="flex flex-col gap-1.5">
          <span className="text-xs leading-relaxed text-muted-foreground">
            {t("runtimes.hostNotReadyBody")}
          </span>
          <CopyLine text={host.remedy} />
        </span>
      ) : null}
    </div>
  );
}

/**
 * One runtime: state first, then what it authenticates as, then — only when something is
 * missing — the remedy. A switched-off credential is a fact ("this machine's session"), never
 * a warning: it is the supported configuration a signed-in machine runs on.
 */
function RuntimeRowItem({ runtime }: { runtime: RuntimeRow }) {
  const cliMissing = !runtime.cliReady;
  const secretMissing = runtime.credentialReady === false;
  // Said instead of the secret state, not beside it: a reader signed into this CLI needs to
  // learn their login cannot travel to where the agent runs, not to chase a secret they never
  // meant to store (#288).
  const sessionStuck = !cliMissing && runtime.sessionUnavailableReason !== null;

  return (
    <li className="flex flex-col gap-1.5 border-b px-3.5 py-2.5 last:border-0">
      <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
        {cliMissing || sessionStuck || secretMissing ? (
          <Badge variant="outline" className="border-warning/40 bg-warning/15 text-warning">
            <TriangleAlert aria-hidden="true" className="size-3" />
            {cliMissing
              ? t("runtimes.cliMissing")
              : sessionStuck
                ? t("runtimes.sessionCantTravel")
                : t("runtimes.secretMissing")}
          </Badge>
        ) : (
          <Badge variant="outline" className="border-success/40 bg-success/10 text-success">
            {t("runtimes.ready")}
          </Badge>
        )}
        <span className="min-w-0 flex-1 text-xs font-semibold">{runtime.name}</span>
        <span
          className={cn(
            "text-[11px]",
            secretMissing && !sessionStuck ? "text-warning" : "text-muted-foreground",
          )}
        >
          {runtime.credentialSecretName === null
            ? t("runtimes.sessionAuth")
            : `${t("runtimes.secret")} ${runtime.credentialSecretName}`}
        </span>
      </span>
      {cliMissing ? (
        <span className="flex flex-col gap-1.5">
          <span className="text-xs leading-relaxed text-muted-foreground">
            {t("runtimes.cliMissingBody")}
          </span>
          <CopyLine text={runtime.installCommand} />
        </span>
      ) : null}
      {sessionStuck ? (
        <span className="flex flex-col gap-1.5">
          <span className="text-xs leading-relaxed text-muted-foreground">
            {runtime.sessionUnavailableReason}
          </span>
          {runtime.sessionUnavailableRemedy ? (
            <CopyLine text={runtime.sessionUnavailableRemedy} />
          ) : null}
        </span>
      ) : !cliMissing && secretMissing ? (
        <span className="text-xs leading-relaxed text-muted-foreground">
          {t("runtimes.secretMissingBody")}
        </span>
      ) : null}
    </li>
  );
}

/**
 * The chip's compact form (#279): only the runtimes that would fail a Run right now, each with
 * its one-line remedy — the popover is a glance, the panel is the page.
 */
export function RuntimesUnavailableCard({ view }: { view: RuntimesView }) {
  if (!view.hosted) return null;
  const hostDown = view.host?.ready === false ? view.host : null;
  const notReady = view.runtimes.filter(runtimeNotReady);
  if (!hostDown && notReady.length === 0) return null;

  return (
    <div className="flex flex-col gap-1.5">
      {/* First, because while the host cannot answer the rows below it are moot. */}
      {hostDown ? (
        <div className="flex items-start gap-2.5 rounded-lg border border-warning/40 bg-warning/10 p-3">
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-3.5 shrink-0 text-warning" />
          <span className="flex min-w-0 flex-col gap-1">
            <span className="text-[13px] font-semibold">
              {t("runtimes.hostLabel")} {hostDown.where} — {t("runtimes.hostNotReady")}
            </span>
            {hostDown.remedy ? <CopyLine text={hostDown.remedy} /> : null}
          </span>
        </div>
      ) : null}
      {notReady.map((runtime) => (
        <div
          key={runtime.name}
          className="flex items-start gap-2.5 rounded-lg border border-warning/40 bg-warning/10 p-3"
        >
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-3.5 shrink-0 text-warning" />
          <span className="flex min-w-0 flex-col gap-1">
            <span className="text-[13px] font-semibold">
              {runtime.name} —{" "}
              {!runtime.cliReady
                ? t("runtimes.cliMissing")
                : runtime.sessionUnavailableReason
                  ? t("runtimes.sessionCantTravel")
                  : t("runtimes.secretMissing")}
            </span>
            {!runtime.cliReady ? (
              <CopyLine text={runtime.installCommand} />
            ) : (
              <span className="flex flex-col gap-1">
                <span className="text-xs leading-relaxed text-muted-foreground">
                  {runtime.sessionUnavailableReason ??
                    `${t("runtimes.secret")} ${runtime.credentialSecretName}`}
                </span>
                {runtime.sessionUnavailableRemedy ? (
                  <CopyLine text={runtime.sessionUnavailableRemedy} />
                ) : null}
              </span>
            )}
          </span>
        </div>
      ))}
    </div>
  );
}
