import { Moon, Sun } from "lucide-react";
import { useState } from "react";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/button";

/**
 * One choice drives both styling systems while the migration runs (DEC-051): the legacy kit
 * reads `data-theme` on the document element, the Platform theme reads the `.dark` class.
 * Setting them together is what keeps a half-migrated page from splitting into two themes.
 * No component reads the choice — each system consumes variables whose values its hook swaps.
 */
export function applyTheme(dark: boolean) {
  document.documentElement.classList.toggle("dark", dark);
  document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
}

/** A first visit follows the operating system, exactly as the kit's media query always did. */
export function applyInitialTheme() {
  applyTheme(window.matchMedia("(prefers-color-scheme: dark)").matches);
}

export function ThemeToggle() {
  const [dark, setDark] = useState(() => document.documentElement.classList.contains("dark"));

  function toggle() {
    applyTheme(!dark);
    setDark(!dark);
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      type="button"
      onClick={toggle}
      aria-label={t("theme.toggle")}
    >
      {dark ? <Sun className="size-4" /> : <Moon className="size-4" />}
    </Button>
  );
}
