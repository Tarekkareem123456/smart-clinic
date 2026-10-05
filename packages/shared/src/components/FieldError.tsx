import { createElement } from "react";

export interface FieldErrorProps {
  messageKey: string;
  messageParams?: Record<string, string | number>;
  id?: string;
}

export function FieldError({
  messageKey,
  messageParams,
  id,
}: FieldErrorProps) {
  return createElement(
    "p",
    {
      id,
      role: "alert",
      "data-field-error": true,
      "data-message-key": messageKey,
      "data-message-params": messageParams
        ? JSON.stringify(messageParams)
        : undefined,
    },
    messageKey,
  );
}