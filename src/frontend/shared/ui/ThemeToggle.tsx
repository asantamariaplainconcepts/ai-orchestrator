import { useState } from "react";
import { t } from "@/shared/i18n";

/**
 * The theme is one attribute on the document element; absent it, the OS preference applies.
 * No component reads it — they consume variables whose values the theme swaps.
 * Shared rather than per-screen so every page is reachable in both themes.
 */
export function ThemeToggle() {
  const [theme, setTheme] = useState<"light" | "dark" | null>(null);

  function toggle() {
    const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    const current = theme ?? (prefersDark ? "dark" : "light");
    const next = current === "dark" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", next);
    setTheme(next);
  }

  return (
    <button className="btn" type="button" onClick={toggle} aria-label={t("theme.toggle")}>
      {t("theme.toggle")}
    </button>
  );
}
