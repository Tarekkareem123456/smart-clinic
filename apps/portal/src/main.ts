// Foundation placeholder for the Patient Portal application surface (T-001).
// No business/clinical functionality is implemented here, and this surface
// must never import from apps/staff (enforced by scripts/check-boundaries.mjs).
import { appName } from "@smart-clinic/shared";

export const surface = "portal" as const;

// eslint-disable-next-line no-console
console.log(`${appName}: ${surface} surface placeholder loaded`);
