import { createElement } from "react";

export type StatePanelVariant =
  | "permission-denied"
  | "configured-disabled"
  | "blocked"
  | "unauthenticated"
  | "not-found"
  | "network-failure"
  | "empty";

export interface StatePanelAction {
  labelKey: string;
  onAction: () => void;
}

export interface StatePanelProps {
  variant: StatePanelVariant;
  messageKey?: string;
  actions?: StatePanelAction[];
}

const variantMessageKeys: Record<StatePanelVariant, string> = {
  "permission-denied": "state.permissionDenied",
  "configured-disabled": "state.configuredDisabled",
  blocked: "state.blocked",
  unauthenticated: "state.unauthenticated",
  "not-found": "state.notFound",
  "network-failure": "state.networkFailure",
  empty: "state.empty",
};

export function StatePanel({
  variant,
  messageKey,
  actions,
}: StatePanelProps) {
  const resolvedMessageKey =
    variant === "empty" && messageKey
      ? messageKey
      : variantMessageKeys[variant];

  return createElement(
    "section",
    {
      "data-state-panel": variant,
      "aria-live": "polite",
    },
    createElement(
      "p",
      { "data-message-key": resolvedMessageKey },
      resolvedMessageKey,
    ),
    actions?.length
      ? createElement(
          "div",
          { "data-state-actions": true },
          ...actions.map((action) =>
            createElement(
              "button",
              {
                key: action.labelKey,
                type: "button",
                onClick: action.onAction,
              },
              action.labelKey,
            ),
          ),
        )
      : null,
  );
}