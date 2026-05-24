import { Link } from "react-router-dom";
import "./AnalyticsEmptyState.css";

type EmptyStateAction = {
  label: string;
  href?: string;
  onClick?: () => void;
};

type AnalyticsEmptyStateProps = {
  title?: string;
  message?: string;
  reasons?: string[];
  actions?: EmptyStateAction[];
  emptyReason?: string | null;
  dataQualityHref?: string;
  refreshStatusHref?: string;
  variant?: "no_data" | "insufficient_data" | "filtered_out";
};

const VARIANT_DEFAULTS: Record<
  NonNullable<AnalyticsEmptyStateProps["variant"]>,
  { title: string; message: string }
> = {
  no_data: {
    title: "Nema podataka za izabrani period.",
    message: "Sistem nije pronasao zapise koji odgovaraju trenutnim filterima.",
  },
  insufficient_data: {
    title: "Nema dovoljno podataka za pouzdanu analizu.",
    message: "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak.",
  },
  filtered_out: {
    title: "Nema rezultata za trenutne filtere.",
    message: "Promenite filtere ili prosirite period.",
  },
};

export default function AnalyticsEmptyState({
  title,
  message,
  reasons,
  actions,
  emptyReason,
  dataQualityHref,
  refreshStatusHref,
  variant,
}: AnalyticsEmptyStateProps) {
  const defaults = variant ? VARIANT_DEFAULTS[variant] : null;
  const displayTitle = title ?? defaults?.title ?? "Nema podataka.";
  const displayMessage = message ?? defaults?.message ?? null;
  const variantClass = variant ? ` aes-${variant.replace(/_/g, "-")}` : "";
  const normalizedEmptyReason = emptyReason?.trim();
  const showEmptyReason = emptyReason !== undefined && emptyReason !== null;
  const resolvedDataQualityHref = dataQualityHref || "/analytics/data-quality";
  const resolvedRefreshStatusHref = refreshStatusHref || "/admin/configuration?panel=workers";
  const defaultActions: EmptyStateAction[] = variant === "filtered_out"
    ? [
      { label: "Ublazite filtere i pokusajte ponovo." },
      { label: "Otvori kvalitet podataka", href: resolvedDataQualityHref },
      { label: "Proveri refresh status", href: resolvedRefreshStatusHref },
    ]
    : [
      { label: "Prosiri period." },
      { label: "Otvori kvalitet podataka", href: resolvedDataQualityHref },
      { label: "Proveri refresh status", href: resolvedRefreshStatusHref },
    ];
  const resolvedActions = actions && actions.length > 0 ? actions : defaultActions;

  return (
    <section className={`analytics-empty-state${variantClass}`} role="status" aria-live="polite">
      <h2>{displayTitle}</h2>
      {displayMessage ? <p>{displayMessage}</p> : null}
      {showEmptyReason ? <p className="aes-empty-reason">{normalizedEmptyReason || "Nije specificirano"}</p> : null}

      {reasons && reasons.length > 0 ? (
        <div className="aes-reasons">
          <h3>Moguci razlozi</h3>
          <ul>
            {reasons.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {resolvedActions.length > 0 ? (
        <div className="aes-actions">
          <h3>Predlog akcija</h3>
          <ul>
            {resolvedActions.map((action) => (
              <li key={action.label}>
                {action.href ? (
                  <Link to={action.href} className="aes-action-link">{action.label}</Link>
                ) : action.onClick ? (
                  <button type="button" className="aes-action-btn" onClick={action.onClick}>{action.label}</button>
                ) : (
                  action.label
                )}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {(resolvedDataQualityHref || resolvedRefreshStatusHref) ? (
        <div className="aes-footer-links">
          {resolvedDataQualityHref ? <Link to={resolvedDataQualityHref} className="aes-footer-link">Data Quality</Link> : null}
          {resolvedRefreshStatusHref ? <Link to={resolvedRefreshStatusHref} className="aes-footer-link">Refresh Status</Link> : null}
        </div>
      ) : null}
    </section>
  );
}

export type { AnalyticsEmptyStateProps, EmptyStateAction };
