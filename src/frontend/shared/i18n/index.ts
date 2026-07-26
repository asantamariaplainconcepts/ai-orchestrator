import { en, type TranslationKey } from "./en";

/**
 * Lookup is typed: an unknown key is a compile error, so copy cannot drift from the catalog.
 * A second locale would swap the catalog here — no call site changes.
 */
export function t(key: TranslationKey): string {
  return en[key];
}

/**
 * Counted nouns, chosen by the count rather than concatenated. "1 Stories" is the kind of thing
 * that reads as unfinished software, and it only ever appears once real data arrives.
 * English has two forms; a locale with more would select from the catalog the same way.
 */
export function tCount(count: number, one: TranslationKey, other: TranslationKey): string {
  return `${count} ${t(count === 1 ? one : other)}`;
}

export type { TranslationKey };
