// Foundation placeholder for shared frontend contracts (T-001).
// Intentionally empty of business/domain types. Real shared contracts
// (DTOs, enums, permission constants, etc.) are added by later tasks.
export const appName = "Smart Clinic" as const;

// T-008 — i18n / RTL-LTR foundation (ar/en). See ./i18n for details.
export * from "./i18n";
export * from "./components/StatePanel";
export * from "./components/LoadingFeedback";
export * from "./components/FieldError";
export * from "./components/ValidationSummary";
export * from "./components/Toast";
export * from "./components/InlineAlert";
