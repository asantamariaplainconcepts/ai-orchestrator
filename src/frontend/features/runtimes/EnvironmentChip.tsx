import { Monitor } from "lucide-react";
import { Link } from "react-router";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { useCurrentPrincipal } from "@/shared/identity/useCurrentPrincipal";
import { Popover, PopoverContent, PopoverTrigger } from "@/shared/ui/popover";
import { useSandboxes } from "@/features/sandboxes/useSandboxes";
import { RuntimesUnavailableCard } from "./RuntimesReadiness";
import { runtimesBlocked, useRuntimes } from "./useRuntimes";

/**
 * The self-host posture as an environment chip (design review 5a). The permanent ⚠ banner
 * treated the product's primary mode as an anomaly and spent a row of every screen saying so;
 * the chip states the same facts — identity, where it listens, whether the runtimes are ready — in the
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
  // Ambient cadence — the runtimes panel itself re-polls at run cadence when watched.
  const runtimes = useRuntimes({ enabled: owner });

  if (!owner) return null;

  const blocked = runtimesBlocked(runtimes.data);

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
          // The accessible name carries the readiness (issue acceptance): a screen reader hears
          // what the sighted user infers from the warning dot.
          aria-label={`${t("env.thisMachine")} — ${t("env.ownerNoSignIn")}${
            blocked ? ` — ${t("runtimes.hostNotReady")}` : ""
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
  const runtimes = useRuntimes();
  // Ambient cadence: the sandboxes screen re-reads at its own pace when watched, and this shares
  // that query's cache — the fastest visible consumer sets it.
  const sandboxes = useSandboxes({ refetchInterval: 60_000 });

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
      </dl>

      {runtimes.data ? <RuntimesUnavailableCard view={runtimes.data} /> : null}
      {runtimes.data?.hosted ? (
        <Link
          className="self-start text-xs text-primary underline-offset-4 hover:underline"
          to="/runtimes"
        >
          {t("env.viewRuntimes")}
        </Link>
      ) : null}
      {/* Gated on the sandboxes surface's own answer rather than the runtimes' (#311). They agree
          today, and inferring one habitat question from another is how they would stop agreeing:
          ADR-0021 refuses a terminal where a Run may still execute perfectly well. */}
      {sandboxes.data?.hosted ? (
        <Link
          className="self-start text-xs text-primary underline-offset-4 hover:underline"
          to="/sandboxes"
        >
          {t("env.viewSandboxes")}
        </Link>
      ) : null}

      <p className="rounded-md border border-warning/50 bg-warning/10 p-2 text-[11px] leading-snug">
        {t("env.networkWarning")}
      </p>
    </>
  );
}
