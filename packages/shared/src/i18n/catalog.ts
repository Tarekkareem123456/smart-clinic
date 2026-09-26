import type { Locale } from "./types";

/**
 * T-008 — foundation-only message catalogs.
 *
 * These are placeholder, non-clinical strings whose only job is to prove
 * the ar/en lookup and the "server sends a code, client translates it"
 * mechanism (Technical Plan §6.4) actually works end to end. Real UI copy
 * and the real error-code catalog belong to later tasks (e.g. T-006 Error
 * Envelope, and whichever Epic raises a given error) — adding them here
 * would be business functionality, which T-008 explicitly excludes.
 */
const catalogs: Record<Locale, Record<string, string>> = {
  en: {
    "common.appName": "Smart Clinic",
    "errors.UNKNOWN": "Something went wrong. Please try again.",
  },
  ar: {
    "common.appName": "سمارت كلينك",
    "errors.UNKNOWN": "حدث خطأ ما. حاول مرة أخرى.",
  },
};

/**
 * Looks up a UI string by a dotted key for the given locale. Falls back to
 * returning the key itself when missing, so a missing translation is
 * obviously wrong in the UI instead of silently showing another language
 * or throwing.
 */
export function t(locale: Locale, key: string): string {
  return catalogs[locale]?.[key] ?? key;
}

/**
 * Translates a server-sent error `code` into a localized message.
 * Per Technical Plan §6.4: the server only ever returns a `code`, never a
 * ready-made message; the client is always responsible for translating it.
 * This is the client-side half of that mechanism. Falls back to the
 * generic "errors.UNKNOWN" entry for any code this foundation doesn't yet
 * know about (the real catalog of codes grows with later tasks).
 */
export function translateErrorCode(locale: Locale, code: string): string {
  const key = `errors.${code}`;
  const message = catalogs[locale]?.[key];
  return message ?? t(locale, "errors.UNKNOWN");
}
