import { Monitor } from "lucide-react";
import { Link } from "react-router";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { useCurrentPrincipal } from "@/shared/identity/useCurrentPrincipal";
import { Popover, PopoverContent, PopoverTrigger } from "@/shared/ui/popover";
import { podsHealth, podsHealthLabel, PodsUnavailableCard } from "./PodsHealth";
import { podsBlocked, usePods } from "./usePods";

/**
 * The self-host posture as an environment chip (design review 5a). The permanent ⚠ banner
 * treated the product's primary mode as an anomaly and spent a row of every screen saying so;
 * the chip states the same facts — identity, where it listens, whether pods are ready — in the
 * sidebar's footer, said once and well, with the network warning inside the popover. The banner
 * survives only for the real hazard — reached from another machine with no sign-in — and that
 * one lives in the AppShell.
 *
 * Three containers, like the nav items: the expanded sidebar (chip + popover), the collapsed
 * rail (icon-only trigger, same popover), and the mobile sheet (`inline`, a plain section —
 * a popover inside a drawer would be a flyout on a flyout).
 */
export function EnvironmentChip({
  collapsed = false,
  inline = false,
}: {
  collapsed?: boolean;
  inline?: boolean;
}) {
  const me = useCurrentPrincipal();
  const owner = me.data?.id === "local-owner";
  // Ambient cadence — the pods panel itself re-polls at run cadence when watched.
  const pods = usePods({ enabled: owner });

  if (!owner) return null;

  const health = pods.data?.hosted ? podsHealth(pods.data) : null;
  const blocked = podsBlocked(pods.data);

  if (inline) {
    return (
      <section aria-label={t("env.selfHostedTitle")} className="flex flex-col gap-2.5">
        <span className="flex items-center gap-2 text-xs font-semibold">
          <Monitor aria-hidden="true" className="size-3.5 text-info" />
          {t("env.selfHostedTitle")}
        </span>
        <EnvironmentFacts />
      </section>
    );
  }

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          className={cn(
            "flex items-center rounded-md border border-info/30 bg-card text-left transition-colors hover:bg-accent",
            collapsed ? "justify-center self-center p-2" : "gap-2 px-2.5 py-1.5",
          )}
          // The accessible name carries the pod status (issue acceptance): a screen reader hears
          // what the sighted user infers from the chip's neighbourhood.
          aria-label={`${t("env.thisMachine")} — ${t("env.ownerNoSignIn")}${
            health ? ` — ${t("env.agentPods")}: ${podsHealthLabel(health)}` : ""
          }`}
          title={collapsed ? t("env.thisMachine") : undefined}
        >
          <Monitor aria-hidden="true" className="size-3.5 shrink-0 text-info" />
          {collapsed ? null : (
            <span className="flex min-w-0 flex-col">
              <span className="flex items-center gap-1.5 text-[11px] font-semibold text-info">
                {t("env.thisMachine")}
                {blocked ? (
                  <span aria-hidden="true" className="size-1.5 rounded-full bg-destructive" />
                ) : null}
              </span>
              <span className="truncate text-[10px] text-muted-foreground">
                {t("env.ownerNoSignIn")}
              </span>
            </span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent side="top" align="start" className="flex w-80 flex-col gap-2.5">
        <h4 className="flex items-center gap-2 text-xs font-semibold">
          <Monitor aria-hidden="true" className="size-3.5 text-info" />
          {t("env.selfHostedTitle")}
        </h4>
        <EnvironmentFacts />
      </PopoverContent>
    </Popover>
  );
}

/**
 * The popover's body, shared with the sheet's inline section: the facts, the compact
 * not-ready card when it applies (mock 5c§4), the way to the panel, and the warning.
 */
function EnvironmentFacts() {
  const pods = usePods();
  const health = pods.data?.hosted ? podsHealth(pods.data) : null;

  return (
    <>
      <dl className="flex flex-col gap-1.5 text-xs">
        <div className="flex items-baseline justify-between gap-3">
          <dt className="shrink-0 text-muted-foreground">{t("env.identity")}</dt>
          <dd className="text-right">{t("env.identityValue")}</dd>
        </div>
        <div className="flex items-baseline justify-between gap-3">
          <dt className="shrink-0 text-muted-foreground">{t("env.listeningOn")}</dt>
          {/* What the operator typed to get here — the address that matters is the reachable
              one, and the browser is standing proof of reachability. */}
          <dd className="font-mono text-[11px]">{window.location.host}</dd>
        </div>
        {health ? (
          <div className="flex items-baseline justify-between gap-3">
            <dt className="shrink-0 text-muted-foreground">{t("env.agentPods")}</dt>
            <dd
              className={cn(
                "font-semibold",
                health === "ready" && "text-success",
                health === "checking" && "text-muted-foreground",
                health === "down" && "text-destructive",
                health === "imageMissing" && "text-warning",
              )}
            >
              {podsHealthLabel(health)}
            </dd>
          </div>
        ) : null}
      </dl>

      {pods.data?.hosted ? (
        <>
          <PodsUnavailableCard view={pods.data} compact />
          <Link
            className="self-start text-xs text-primary underline-offset-4 hover:underline"
            to="/pods"
          >
            {t("env.viewPods")}
          </Link>
        </>
      ) : null}

      <p className="rounded-md border border-warning/50 bg-warning/10 p-2 text-[11px] leading-snug">
        {t("env.networkWarning")}
      </p>
    </>
  );
}
