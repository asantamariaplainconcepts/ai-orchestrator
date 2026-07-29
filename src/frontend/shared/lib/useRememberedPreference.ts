import { useState } from "react";

/**
 * A choice the reader made, remembered across reloads (#126, design D3).
 *
 * Extracted on the second occurrence rather than the third, which is this repository's own rule for
 * when a pattern graduates. The care it encodes is not new — it is the backlog view toggle's, made
 * reusable: a blocked or absent `localStorage` (private mode, a hardened browser, a quota refusal)
 * must cost the **preference** and never the **interaction**. So the read is lazy and guarded, and the
 * write cannot throw into a click handler.
 *
 * Only for genuine preferences: something nothing about the data implies. A value the application can
 * derive should be derived, because a remembered answer to a derivable question goes stale silently.
 */
export function useRememberedPreference<T extends string>(
  key: string,
  fallback: T,
  isValid: (value: string) => value is T,
): [T, (next: T) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const stored = window.localStorage.getItem(key);
      return stored !== null && isValid(stored) ? stored : fallback;
    } catch {
      return fallback;
    }
  });

  return [
    value,
    (next: T) => {
      // State first: the interaction happens whether or not the browser lets us remember it.
      setValue(next);
      try {
        window.localStorage.setItem(key, next);
      } catch {
        // A refused write costs the preference, never the interaction.
      }
    },
  ];
}
