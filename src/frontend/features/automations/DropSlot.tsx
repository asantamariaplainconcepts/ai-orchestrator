import { t } from "@/shared/i18n";
import type { Automation } from "./types";
import type { DropRefusal } from "./chainDrag";

/**
 * The sentence a slot says while something hovers over it (8a/8c). It is the whole point of the
 * gesture: a drop rewrites two labels, and reading which two before letting go is what makes the
 * drag safe to perform on a workflow somebody depends on.
 */
export function DropSlot({
  preceding,
  following,
  dragged,
  refusal,
}: {
  preceding: Automation;
  following: Automation | null;
  dragged: Automation;
  refusal: DropRefusal | null;
}) {
  if (refusal) {
    return (
      <span className="flex flex-col gap-0.5 py-1 text-center">
        <span className="text-[11px] font-semibold text-destructive">{t("canvas.cantDrop")}</span>
        <span className="text-[11px] leading-snug text-muted-foreground">
          <span className="font-mono">{dragged.triggerLabel}</span>{" "}
          {refusal === "shared"
            ? t("canvas.refuseShared")
            : refusal === "cycle"
              ? t("canvas.refuseCycle")
              : refusal === "self"
                ? t("canvas.refuseSelf")
                : t("canvas.refuseAlready")}
        </span>
      </span>
    );
  }

  return (
    <span className="flex flex-col gap-0.5 py-1 text-center">
      <span className="text-[11px] font-semibold text-primary">
        {following ? t("canvas.dropHere") : t("canvas.dropAtEnd")}
      </span>
      <span className="text-[11px] leading-snug text-muted-foreground">
        {following ? (
          // Two rewrites, both named, in the order they happen.
          <>
            <span className="font-mono">{preceding.triggerLabel}</span> {t("canvas.willHandTo")}{" "}
            <span className="font-mono">{dragged.triggerLabel}</span>
            {" · "}
            <span className="font-mono">{dragged.triggerLabel}</span> {t("canvas.willHandTo")}{" "}
            <span className="font-mono">{following.triggerLabel}</span>
          </>
        ) : (
          // One rewrite at the end, so "it" is the step being dragged and the sentence stays a
          // sentence: naming the dragged step twice read as two hand-offs where there is one.
          <>
            <span className="font-mono">{preceding.triggerLabel}</span> {t("canvas.willHandToIt")}
          </>
        )}
      </span>
    </span>
  );
}
