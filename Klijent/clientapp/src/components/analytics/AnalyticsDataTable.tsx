import { useEffect, useRef, useState, type ReactNode } from "react";
import "./AnalyticsDataTable.css";

export function useHorizontalOverflow<T extends HTMLElement>() {
  const containerRef = useRef<T>(null);
  const [isOverflowing, setIsOverflowing] = useState(false);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const measure = () => {
      setIsOverflowing(container.scrollWidth > container.clientWidth);
    };

    measure();
    const observer = typeof ResizeObserver === "undefined"
      ? null
      : new ResizeObserver(measure);
    observer?.observe(container);
    const table = container.querySelector("table");
    if (table) observer?.observe(table);
    window.addEventListener("resize", measure);

    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", measure);
    };
  }, []);

  return { containerRef, isOverflowing };
}

type AnalyticsDataTableProps = {
  toolbar?: ReactNode;
  rowCount: number;
  truncationLabel?: string;
  responsivePilot?: boolean;
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
  responsivePilot = true,
  children,
  testId = "analytics-data-table",
}: AnalyticsDataTableProps) {
  const { containerRef, isOverflowing } = useHorizontalOverflow<HTMLDivElement>();

  return (
    <section
      className={`analytics-data-table${responsivePilot ? " analytics-data-table--responsive-pilot" : ""}`}
      data-testid={testId}
    >
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

      {responsivePilot ? (
        <p className="analytics-data-table__scroll-hint" id="analytics-data-table-scroll-hint" role="note">
          Tabela se pomera vodoravno — prevucite ili skrolujte za ostale kolone.
        </p>
      ) : null}
      <div
        ref={containerRef}
        className={`analytics-data-table__scroll analytics-wide-table-scroll${isOverflowing ? " analytics-wide-table-scroll--overflowing" : ""}`}
        tabIndex={responsivePilot ? 0 : undefined}
        role={responsivePilot ? "region" : undefined}
        aria-label={responsivePilot ? "Tabela sa vodoravnim pomeranjem" : undefined}
        aria-describedby={responsivePilot ? "analytics-data-table-scroll-hint" : undefined}
      >
        {children}
      </div>
    </section>
  );
}
