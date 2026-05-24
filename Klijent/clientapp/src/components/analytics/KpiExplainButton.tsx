import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";
import type { AnalyticsMetricKey } from "../../utils/analyticsMetricDefinitions";
import MetricMethodologyPanel from "./MetricMethodologyPanel";
import "./MetricMethodologyPanel.css";

type KpiExplainButtonProps = {
  metricKey: AnalyticsMetricKey;
  className?: string;
  label?: string;
  dataQualityHref?: string | null;
};

export default function KpiExplainButton({
  metricKey,
  className,
  label = "Kako je izračunato?",
  dataQualityHref = "/analytics/data-quality",
}: KpiExplainButtonProps) {
  const [open, setOpen] = useState(false);
  const titleId = useId();

  useEffect(() => {
    if (!open) return undefined;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <>
      <button
        type="button"
        className={`kpi-explain-button${className ? ` ${className}` : ""}`}
        onClick={() => setOpen(true)}
      >
        {label}
      </button>
      {open
        ? createPortal(
            <div className="metric-methodology-overlay" onClick={() => setOpen(false)}>
              <div
                className="metric-methodology-dialog"
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleId}
                onClick={(event) => event.stopPropagation()}
              >
                <div id={titleId} className="metric-methodology-screenreader-title">
                  Objašnjenje metrike
                </div>
                <MetricMethodologyPanel metricKey={metricKey} onClose={() => setOpen(false)} dataQualityHref={dataQualityHref} />
              </div>
            </div>,
            document.body
          )
        : null}
    </>
  );
}
