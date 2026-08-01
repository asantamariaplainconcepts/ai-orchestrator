import { Container, Monitor } from "lucide-react";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";

/**
 * The one vocabulary for "where code executes" (#211, mocks 3c/3e): a monitor glyph for this
 * machine, a container glyph for an Agent pod — always beside a word, never colour alone. The
 * projects list's Local badge and the Run detail's locus chip both render through here, which is
 * what keeps "local" looking identical everywhere it appears.
 */
export function LocusChip({ locus }: { locus: "Local" | "Pod" }) {
  const Icon = locus === "Local" ? Monitor : Container;
  return (
    <Badge variant="outline" className="gap-1">
      <Icon aria-hidden="true" className="size-3" />
      {locus === "Local" ? t("locus.local") : t("locus.pod")}
    </Badge>
  );
}
