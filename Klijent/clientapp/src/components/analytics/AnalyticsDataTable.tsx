import type { ReactNode } from "react";
import "./AnalyticsDataTable.css";

type AnalyticsDataTableProps = {
  toolbar?: ReactNode;
  rowCount: number;
  truncationLabel?: string;
  children: ReactNode;
  testId?: string;
};

function formatRowCountLabel(rowCount: number): string {
  if (rowCount === 1) return "1 red";
  return `${rowCount.toLocaleString("sr-RS")} redova`;
}

export default function AnalyticsDataTable({
  toolbar,
  rowCount,
  truncationLabel,
  children,
  testId = "analytics-data-table",
}: AnalyticsDataTableProps) {
  return (
    <section className="analytics-data-table" data-testid={testId}>
      {toolbar || truncationLabel ? (
        <div className="analytics-data-table__toolbar-row">
          {toolbar ? (
            <div className="analytics-data-table__toolbar">{toolbar}</div>
          ) : null}
          <div className="analytics-data-table__meta">
            <span className="analytics-data-table__meta-pill">
              Prikazano: {formatRowCountLabel(rowCount)}
            </span>
            {truncationLabel ? (
              <span className="analytics-data-table__meta-pill analytics-data-table__meta-pill--muted">
                {truncationLabel}
              </span>
            ) : null}
          </div>
        </div>
      ) : null}

      <div className="analytics-data-table__scroll">{children}</div>
    </section>
  );
}
