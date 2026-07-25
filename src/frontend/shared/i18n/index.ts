import { en, type TranslationKey } from "./en";

/**
 * Lookup is typed: an unknown key is a compile error, so copy cannot drift from the catalog.
 * A second locale would swap the catalog here — no call site changes.
 */
export function t(key: TranslationKey): string {
  return en[key];
}

export type { TranslationKey };
