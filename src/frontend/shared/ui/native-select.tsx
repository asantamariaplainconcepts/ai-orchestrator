import type { ComponentProps } from "react";
import { cn } from "@/shared/lib/utils";

/**
 * A native `<select>` wearing the theme's Input styling (design D4 of dashboard-tabs).
 * Deliberately not shadcn's Radix Select: the E2E reachability tests read `option` elements out
 * of `#vendor` and `#runtime`, and a listbox of divs would break exactly the assertions whose
 * job is to catch a relocated control. On a phone the OS picker also beats a custom popover.
 */
export function NativeSelect({ className, ...props }: ComponentProps<"select">) {
  return (
    <select
      data-slot="native-select"
      className={cn(
        "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-[color,box-shadow] outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}
