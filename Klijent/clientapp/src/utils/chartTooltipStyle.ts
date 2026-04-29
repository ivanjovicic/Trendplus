import type { CSSProperties } from "react";

export const CHART_TOOLTIP_STYLE: CSSProperties = {
  background: "var(--analytics-chart-tooltip-bg, var(--surface-elevated))",
  border: "var(--border-width-sm) solid var(--analytics-chart-tooltip-border, var(--border-default))",
  color: "var(--analytics-text, var(--text-primary))",
  borderRadius: "var(--analytics-radius-sm, var(--radius-md))",
  boxShadow: "var(--analytics-chart-tooltip-shadow, var(--chart-tooltip-shadow, var(--card-shadow)))",
};

export const CHART_TOOLTIP_LABEL_STYLE: CSSProperties = {
  color: "var(--analytics-title-text, var(--text-primary))",
  fontWeight: "var(--font-weight-bold)",
  marginBottom: "var(--space-1)",
};
