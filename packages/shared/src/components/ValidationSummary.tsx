import { createElement } from "react";

export interface ValidationSummaryItem {
  field?: string;
  messageKey: string;
  messageParams?: Record<string, string | number>;
}

export interface ValidationSummaryProps {
  items: ValidationSummaryItem[];
  titleKey: string;
}

export function ValidationSummary({
  items,
  titleKey,
}: ValidationSummaryProps) {
  if (items.length === 0) {
    return null;
  }

  return createElement(
    "section",
    {
      role: "alert",
      "aria-labelledby": "validation-summary-title",
      "data-validation-summary": true,
    },
    createElement(
      "h2",
      { id: "validation-summary-title" },
      titleKey,
    ),
    createElement(
      "ul",
      null,
      ...items.map((item, index) =>
        createElement(
          "li",
          {
            key: `${item.field ?? "general"}-${item.messageKey}-${index}`,
            "data-field": item.field,
            "data-message-key": item.messageKey,
            "data-message-params": item.messageParams
              ? JSON.stringify(item.messageParams)
              : undefined,
          },
          item.messageKey,
        ),
      ),
    ),
  );
}