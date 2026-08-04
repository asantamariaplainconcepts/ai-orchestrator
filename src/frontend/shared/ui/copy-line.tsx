import { useState } from "react";
import { Check, Copy } from "lucide-react";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/button";

/**
 * A command or configuration line with its copy button (design review 5c/5d) — the mocks' dark
 * row, in tokens: foreground-on-background inverted so the line reads as terminal text in both
 * themes. Copying is the row's whole affordance, which is why the button lives inside it rather
 * than beside it.
 */
export function CopyLine({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);

  return (
    <div className="flex items-center justify-between gap-2.5 rounded-md bg-foreground py-1.5 pr-1.5 pl-3">
      <code className="min-w-0 overflow-x-auto font-mono text-xs whitespace-nowrap text-background">
        {text}
      </code>
      <Button
        type="button"
        variant="secondary"
        size="xs"
        className="shrink-0"
        onClick={() => {
          void navigator.clipboard.writeText(text).then(() => {
            setCopied(true);
            window.setTimeout(() => setCopied(false), 2000);
          });
        }}
      >
        {copied ? (
          <Check aria-hidden="true" className="size-3" />
        ) : (
          <Copy aria-hidden="true" className="size-3" />
        )}
        {copied ? t("ui.copied") : t("ui.copy")}
      </Button>
    </div>
  );
}
