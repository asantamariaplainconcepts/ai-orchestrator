import * as React from "react";

import { cn } from "@/shared/lib/utils";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/shared/ui/dialog";
import { Sheet, SheetContent, SheetFooter, SheetHeader, SheetTitle } from "@/shared/ui/sheet";

/**
 * The width below which a centred dialog stops being the right container: a modal card on a phone
 * either wastes the edges or overflows them, and the drawer is the pattern this product already
 * uses for a choice made on a phone (the board's move sheet, mock 2b).
 */
const COMPACT = "(max-width: 47.999rem)";

/** True while the viewport is narrower than the dialog breakpoint, tracked live so a rotation lands. */
function useCompactViewport() {
  const [compact, setCompact] = React.useState(() => window.matchMedia(COMPACT).matches);

  React.useEffect(() => {
    const query = window.matchMedia(COMPACT);
    const read = () => setCompact(query.matches);

    read();
    query.addEventListener("change", read);
    return () => query.removeEventListener("change", read);
  }, []);

  return compact;
}

/**
 * One panel, two containers: a centred Dialog at pointer widths and a bottom Sheet below them
 * (design review 6b/6c). Both are the same radix primitive, so what differs is where it comes from
 * and nothing about focus, Esc, or the overlay.
 *
 * It exists because the alternative — mounting the form inline above the content — moved the page
 * under the reader: opening an edit scrolled the tab to the top and pushed everything down, and
 * after Save you had to find where you were. A panel leaves the page exactly where it was.
 *
 * Only one container is mounted at a time, so the children are never duplicated in the DOM. State
 * belongs to the caller for that reason: a viewport that crosses the breakpoint remounts the body.
 */
export function ResponsiveDialog({
  open,
  onOpenChange,
  title,
  hideTitle = false,
  footer,
  className,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Always given: it is the panel's accessible name whether or not it is drawn. */
  title: React.ReactNode;
  /**
   * For a panel whose content already carries its own heading. The title stays in the accessibility
   * tree — a dialog without one is unnamed — and the header bar is not drawn, so the reader does not
   * meet the same words twice.
   *
   * It is rendered as a plain element rather than the heading radix defaults to, because `sr-only`
   * hides pixels and not the accessibility tree: a visually hidden `<h2>` beside the content's own
   * `<h2>` of the same name is two headings a screen reader announces twice, and two elements a
   * role query cannot tell apart. The E2E suite caught exactly that as a strict-mode violation.
   */
  hideTitle?: boolean;
  footer?: React.ReactNode;
  className?: string;
  children: React.ReactNode;
}) {
  const compact = useCompactViewport();

  if (compact) {
    return (
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent
          side="bottom"
          className={cn("max-h-[90svh] gap-0 rounded-t-xl p-0", className)}
        >
          <SheetHeader className={cn("border-b border-border px-4 py-3", hideTitle && "sr-only")}>
            {hideTitle ? (
              <SheetTitle asChild>
                <span>{title}</span>
              </SheetTitle>
            ) : (
              <SheetTitle className="text-sm">{title}</SheetTitle>
            )}
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
          {footer ? (
            <SheetFooter className="border-t border-border bg-muted/40 p-4">{footer}</SheetFooter>
          ) : null}
        </SheetContent>
      </Sheet>
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className={cn(
          // Rows rather than a scrolling block: the body scrolls and the footer stays put, so Save
          // is reachable without reaching the end of a long form.
          "grid max-h-[85vh] grid-rows-[auto_minmax(0,1fr)_auto] gap-0 p-0 sm:max-w-xl",
          className,
        )}
      >
        <DialogHeader className={cn("border-b border-border px-5 py-3.5", hideTitle && "sr-only")}>
          {hideTitle ? (
            <DialogTitle asChild>
              <span>{title}</span>
            </DialogTitle>
          ) : (
            <DialogTitle className="text-sm">{title}</DialogTitle>
          )}
        </DialogHeader>
        <div className="min-h-0 overflow-y-auto">{children}</div>
        {footer ? (
          <DialogFooter className="border-t border-border bg-muted/40 px-5 py-3 sm:justify-between">
            {footer}
          </DialogFooter>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
