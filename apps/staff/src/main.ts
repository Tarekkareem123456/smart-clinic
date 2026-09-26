// Foundation placeholder for the Staff application surface (T-001).
// No business/domain functionality is implemented here. This file exists
// only to prove the workspace/module boundaries are wired correctly.
import { appName, applyDocumentDirection } from "@smart-clinic/shared";

export const surface = "staff" as const;

// T-008 foundation: dir/lang on <html> follow the selected language
// automatically. "ar" is a placeholder default for this foundation task
// only — real language selection/persistence UI is not part of T-008 and
// is left to a later task; this line is not business functionality, it
// only proves the shared direction mechanism is wired into this app shell.
applyDocumentDirection("ar");

// eslint-disable-next-line no-console
console.log(`${appName}: ${surface} surface placeholder loaded`);
