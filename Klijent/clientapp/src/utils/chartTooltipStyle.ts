import type { CSSProperties } from "react";

export const CHART_TOOLTIP_STYLE: CSSProperties = {
  background: "var(--surface-elevated, var(--theme-color-0f1730, #0f1730))",
  border: "1px solid var(--border-default, var(--theme-color-32406b, #32406b))",
  color: "var(--text-primary, var(--theme-color-e5e7eb, #e5e7eb))",
  borderRadius: "8px",
  boxShadow: "var(--chart-tooltip-shadow, var(--card-shadow, none))",
};

export const CHART_TOOLTIP_LABEL_STYLE: CSSProperties = {
  color: "var(--text-primary, var(--theme-color-e5e7eb, #e5e7eb))",
  fontWeight: 600,
  marginBottom: "0.25rem",
};
