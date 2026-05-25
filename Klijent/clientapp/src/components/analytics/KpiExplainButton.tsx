import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  getMetricLabel,
  type AnalyticsMetricKey,
} from "../../utils/analyticsMetricDefinitions";
import MetricMethodologyPanel from "./MetricMethodologyPanel";
import "./MetricMethodologyPanel.css";

type KpiExplainButtonProps = {
  metricKey: AnalyticsMetricKey;
  className?: string;
  label?: string;
  ariaLabel?: string;
  dataQualityHref?: string | null;
};

export default function KpiExplainButton({
  metricKey,
  className,
  label = "Kako je izračunato?",
  ariaLabel,
  dataQualityHref = "/analytics/data-quality",
}: KpiExplainButtonProps) {
  const [open, setOpen] = useState(false);
  const titleId = useId();
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const resolvedAriaLabel = ariaLabel ?? `Kako je izračunato: ${getMetricLabel(metricKey)}`;

  useEffect(() => {
    if (!open) return undefined;

    const previousOverflow = document.body.style.overflow;
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    document.body.style.overflow = "hidden";
    const focusTarget = dialogRef.current?.querySelector<HTMLElement>(
      "button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])"
    );
    focusTarget?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKeyDown);
      const focusTarget = previousFocusRef.current ?? buttonRef.current;
      focusTarget?.focus();
    };
  }, [open]);

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        className={`kpi-explain-button${className ? ` ${className}` : ""}`}
        onClick={() => setOpen(true)}
        aria-label={resolvedAriaLabel}
      >
        {label}
      </button>
      {open
        ? createPortal(
            <div className="metric-methodology-overlay" onClick={() => setOpen(false)}>
              <div
                ref={dialogRef}
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
