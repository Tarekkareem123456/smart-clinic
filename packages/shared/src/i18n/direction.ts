import type { Direction, Locale } from "./types";

/**
 * T-008 — direction follows language automatically (UI/UX §9 baseline:
 * Arabic = RTL, English = LTR; direction is never chosen independently
 * of language).
 */
const LOCALE_DIRECTIONS: Record<Locale, Direction> = {
  ar: "rtl",
  en: "ltr",
};

export function getDirectionForLocale(locale: Locale): Direction {
  return LOCALE_DIRECTIONS[locale];
}

/**
 * The minimal shape this module needs from a "document". Accepting this
 * instead of the global `Document` type lets the function be unit-tested
 * with a plain fake object, with no DOM/jsdom dependency required.
 */
export interface DocumentLike {
  documentElement: {
    lang: string;
    dir: string;
  };
}

/**
 * T-008 AC: "`dir` and `lang` change on `<html>` based on the selected
 * language." This sets both attributes on the document's root element.
 *
 * Outside a browser (no global `document`, e.g. Node/tests/SSR) this is a
 * safe no-op unless a `doc` is explicitly passed in — it never throws.
 */
export function applyDocumentDirection(
  locale: Locale,
  doc: DocumentLike | undefined = typeof document !== "undefined"
    ? (document as unknown as DocumentLike)
    : undefined
): void {
  if (!doc) return;
  doc.documentElement.lang = locale;
  doc.documentElement.dir = getDirectionForLocale(locale);
}
