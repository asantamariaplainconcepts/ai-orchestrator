import { ShieldCheck } from "lucide-react";

import { t } from "@/shared/i18n";

/**
 * "A person approves before this proceeds."
 *
 * Shared rather than duplicated (#232): the board's column header and the workflow canvas both say
 * it, and to a reader they mean one thing. Two chips that merely looked alike would drift the first
 * time one of them was restyled, and the design gate would not catch it — the tokens would be right
 * in both.
 */
export function GateChip({ hint }: { hint?: string }) {
  return (
    <span
      // The hint is the caller's, because the *reason* differs by surface: on the board it explains
      // what dropping here will do; on the canvas nothing is being dropped. Sharing the component
      // without parameterising this shipped the board's sentence onto a surface it made no sense
      // on — which is the failure mode of sharing by appearance rather than by meaning.
      title={hint ?? t("board.gated.hint")}
      className="inline-flex shrink-0 items-center gap-1 rounded border border-info/40 bg-info/10 px-1.5 text-[10px] font-semibold text-info"
    >
      <ShieldCheck className="size-2.5" aria-hidden="true" />
      {t("board.gated")}
    </span>
  );
}
