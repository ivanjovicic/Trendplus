import { useId, type ReactNode } from "react";

type AnalyticsChartAccessibilityProps = {
  title: string;
  summary: string;
  children: ReactNode;
  tableTargetId?: string;
  decorative?: boolean;
};

export function describeChartProjection<T>(
  points: readonly T[],
  grouping: string,
  series: readonly string[],
): string {
  return `Tačke na grafikonu: ${points.length}. Grupisanje: ${grouping}. Serije: ${series.join(", ")}.`;
}

export function AnalyticsChartAccessibility({
  title,
  summary,
  children,
  tableTargetId,
  decorative = false,
}: AnalyticsChartAccessibilityProps) {
  const id = useId().replace(/:/g, "");

  if (decorative) {
    return <div aria-hidden="true">{children}</div>;
  }

  const titleId = `analytics-chart-title-${id}`;
  const summaryId = `analytics-chart-summary-${id}`;

  return (
    <figure className="m-0 h-full min-w-0" aria-labelledby={titleId} aria-describedby={summaryId}>
      <figcaption id={titleId} className="sr-only">{title}</figcaption>
      <p id={summaryId} className="sr-only">{summary}</p>
      {children}
      {tableTargetId ? (
        <a className="sr-only focus:not-sr-only" href={`#${tableTargetId}`}>
          Prikaži tabelu podataka
        </a>
      ) : null}
    </figure>
  );
}
