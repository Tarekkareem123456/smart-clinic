import { createElement, useEffect } from "react";

export type ToastKind = "success" | "error" | "info";

export interface ToastProps {
  kind: ToastKind;
  messageKey: string;
  onDismiss: () => void;
}

export function Toast({
  kind,
  messageKey,
  onDismiss,
}: ToastProps) {
  useEffect(() => {
    if (kind !== "success") {
      return;
    }

    const timeoutId = window.setTimeout(onDismiss, 5000);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [kind, onDismiss]);

  return createElement(
    "div",
    {
      role: kind === "error" ? "alert" : "status",
      "aria-live": kind === "error" ? "assertive" : "polite",
      "data-toast": kind,
      "data-message-key": messageKey,
    },
    createElement("span", null, messageKey),
    createElement(
      "button",
      {
        type: "button",
        onClick: onDismiss,
        "aria-label": "toast.dismiss",
      },
      "toast.dismiss",
    ),
  );
}