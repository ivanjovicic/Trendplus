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

/**
 * Shared chart chrome and series palette. Values resolve to the per-theme
 * `--chart-*` tokens defined in ThemeContext/themes.css, so axes, grid lines,
 * legends and series stay readable in light, dark and high-contrast themes.
 */
export const CHART_AXIS_TICK = { fill: "var(--chart-axis)", fontSize: 12 } as const;
export const CHART_GRID_STROKE = "var(--chart-grid)";
export const CHART_LEGEND_STYLE: CSSProperties = { color: "var(--chart-axis)" };

export const CHART_SERIES_COLORS = [
  "var(--chart-series-1)",
  "var(--chart-series-2)",
  "var(--chart-series-3)",
  "var(--chart-series-4)",
  "var(--chart-series-5)",
  "var(--chart-series-6)",
  "var(--chart-series-7)",
  "var(--chart-series-8)",
] as const;

/** One colour per business meaning, reused across analytics charts. */
export const CHART_METRIC_COLORS = {
  revenue: "var(--chart-series-1)",
  documents: "var(--chart-series-2)",
  items: "var(--chart-series-3)",
  revenueAverage: "var(--chart-series-4)",
  itemsAverage: "var(--chart-series-2)",
  longAverage: "var(--chart-series-2)",
} as const;
