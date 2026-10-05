import { createElement } from "react";

export interface LoadingFeedbackProps {
  messageKey: string;
}

export function LoadingFeedback({
  messageKey,
}: LoadingFeedbackProps) {
  return createElement(
    "p",
    {
      role: "status",
      "aria-live": "polite",
      "data-loading-feedback": true,
      "data-message-key": messageKey,
    },
    messageKey,
  );
}