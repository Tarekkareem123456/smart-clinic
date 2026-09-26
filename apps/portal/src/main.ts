// Foundation placeholder for the Patient Portal application surface (T-001).
// No business/clinical functionality is implemented here, and this surface
// must never import from apps/staff (enforced by scripts/check-boundaries.mjs).
import { appName, applyDocumentDirection } from "@smart-clinic/shared";

export const surface = "portal" as const;

// T-008 foundation: dir/lang on <html> follow the selected language
// automatically. "ar" is a placeholder default for this foundation task
// only — real language selection/persistence UI is not part of T-008 and
// is left to a later task; this line is not business functionality, it
// only proves the shared direction mechanism is wired into this app shell.
applyDocumentDirection("ar");

// eslint-disable-next-line no-console
console.log(`${appName}: ${surface} surface placeholder loaded`);
