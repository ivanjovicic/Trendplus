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
  showDefaultLinks?: boolean;
  variant?: "no_data" | "insufficient_data" | "filtered_out";
  onRetry?: () => void;
};

const VARIANT_DEFAULTS: Record<
  NonNullable<AnalyticsEmptyStateProps["variant"]>,
  { title: string; message: string }
> = {
  no_data: {
    title: "Nema podataka za izabrani period.",
    message: "Sistem nije pronašao zapise koji odgovaraju trenutnim filterima.",
  },
  insufficient_data: {
    title: "Nema dovoljno podataka za pouzdanu analizu.",
    message: "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak.",
  },
  filtered_out: {
    title: "Nema rezultata za trenutne filtere.",
    message: "Promenite filtere ili proširite period.",
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
  showDefaultLinks = true,
  variant,
  onRetry,
}: AnalyticsEmptyStateProps) {
  const defaults = variant ? VARIANT_DEFAULTS[variant] : null;
  const displayTitle = title ?? defaults?.title ?? "Nema podataka.";
  const displayMessage = message ?? defaults?.message ?? null;
  const variantClass = variant ? ` aes-${variant.replace(/_/g, "-")}` : "";
  const normalizedEmptyReason = emptyReason?.trim();
  const showEmptyReason = emptyReason !== undefined && emptyReason !== null;
  const resolvedDataQualityHref = dataQualityHref || "/analytics/data-quality";
  const resolvedRefreshStatusHref = refreshStatusHref || "/admin/configuration?panel=workers";
  const defaultActionLabels = variant === "filtered_out"
    ? ["Promenite filtere ili proširite period", "Otvori kvalitet podataka", "Proveri status osvežavanja"]
    : ["Proširi period", "Otvori kvalitet podataka", "Proveri status osvežavanja"];
  const defaultActions: EmptyStateAction[] = variant === "filtered_out"
    ? [
      { label: defaultActionLabels[0] },
      { label: defaultActionLabels[1], href: resolvedDataQualityHref },
      { label: defaultActionLabels[2], href: resolvedRefreshStatusHref },
      ...(onRetry ? [{ label: "Pokušaj ponovo", onClick: onRetry }] : []),
    ]
    : [
      { label: defaultActionLabels[0] },
      { label: defaultActionLabels[1], href: resolvedDataQualityHref },
      { label: defaultActionLabels[2], href: resolvedRefreshStatusHref },
      ...(onRetry ? [{ label: "Pokušaj ponovo", onClick: onRetry }] : []),
    ];
  const resolvedActions = actions && actions.length > 0 ? actions : defaultActions;

  function renderActionLink(href: string, label: string, className: string) {
    if (href.startsWith("/")) {
      return <Link to={href} className={className}>{label}</Link>;
    }

    return <a href={href} className={className}>{label}</a>;
  }

  return (
    <section className={`analytics-empty-state${variantClass}`} role="status" aria-live="polite">
      <h2>{displayTitle}</h2>
      {displayMessage ? <p>{displayMessage}</p> : null}
      {showEmptyReason ? <p className="aes-empty-reason">{normalizedEmptyReason || "Nije specificirano"}</p> : null}

      {reasons && reasons.length > 0 ? (
        <div className="aes-reasons">
          <h3>Mogući razlozi</h3>
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
                  renderActionLink(action.href, action.label, "aes-action-link")
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

      {showDefaultLinks && (resolvedDataQualityHref || resolvedRefreshStatusHref) ? (
        <div className="aes-footer-links">
          {resolvedDataQualityHref ? renderActionLink(resolvedDataQualityHref, "Kvalitet podataka", "aes-footer-link") : null}
          {resolvedRefreshStatusHref ? renderActionLink(resolvedRefreshStatusHref, "Status osvežavanja", "aes-footer-link") : null}
        </div>
      ) : null}
    </section>
  );
}

export type { AnalyticsEmptyStateProps, EmptyStateAction };
