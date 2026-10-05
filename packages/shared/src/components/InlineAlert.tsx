import { createElement } from "react";

export type InlineAlertKind = "success" | "error" | "info";

export interface InlineAlertProps {
  kind: InlineAlertKind;
  messageKey: string;
}

export function InlineAlert({
  kind,
  messageKey,
}: InlineAlertProps) {
  return createElement(
    "div",
    {
      role: kind === "error" ? "alert" : "status",
      "aria-live": kind === "error" ? "assertive" : "polite",
      "data-inline-alert": kind,
      "data-message-key": messageKey,
    },
    messageKey,
  );
}