import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsResponseMeta } from "../../types/analytics";
import { getAnalyticsEmptyReasonMessage } from "../../utils/analyticsResponseMeta";
import { getSafeAnalyticsErrorMessage } from "../../utils/analyticsErrorMessages";
import { resolveAnalyticsState } from "../../utils/analyticsStateTaxonomy";
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
  code?: string | null;
  meta?: AnalyticsResponseMeta | null;
  loading?: boolean;
  loadingDelayMs?: number;
  onRetry?: () => void;
  onCancel?: () => void;
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
  code,
  meta,
  loading = false,
  loadingDelayMs = 8000,
  onRetry,
  onCancel,
}: AnalyticsEmptyStateProps) {
  const [loadingIsSlow, setLoadingIsSlow] = useState(false);
  const [loadingAttempt, setLoadingAttempt] = useState(0);
  useEffect(() => {
    if (!loading) {
      setLoadingIsSlow(false);
      return undefined;
    }

    const delay = Number.isFinite(loadingDelayMs) && loadingDelayMs >= 0 ? loadingDelayMs : 8000;
    const timer = window.setTimeout(() => setLoadingIsSlow(true), delay);
    return () => window.clearTimeout(timer);
  }, [loading, loadingAttempt, loadingDelayMs]);

  const resolvedState = resolveAnalyticsState(code ?? emptyReason, meta, variant);
  const isSlowLoading = code?.trim().toLocaleLowerCase() === "loading_slow"
    || resolvedState.definition?.kind === "loading"
    || (loading && loadingIsSlow);
  const defaults = variant ? VARIANT_DEFAULTS[variant] : null;
  const taxonomyState = isSlowLoading
    ? resolveAnalyticsState("loading_slow").definition
    : resolvedState.code === "unknown_code" || resolvedState.definition?.kind === "empty"
      ? resolvedState.definition
      : null;
  const displayTitle = title ?? (loading && !isSlowLoading ? "Učitavanje podataka…" : taxonomyState?.title ?? defaults?.title ?? "Nema podataka.");
  const rawDisplayMessage = message ?? (loading && !isSlowLoading ? "Sačekajte da se završi učitavanje." : taxonomyState?.message ?? defaults?.message ?? null);
  const displayMessage = rawDisplayMessage
    ? getSafeAnalyticsErrorMessage(rawDisplayMessage, undefined, defaults?.message ?? "Nema podataka za izabrani opseg.")
    : null;
  const variantClass = taxonomyState ? ` aes-${resolvedState.code?.replace(/_/g, "-") ?? "loading-slow"}`
    : variant ? ` aes-${variant.replace(/_/g, "-")}` : "";
  const displayEmptyReason = getAnalyticsEmptyReasonMessage(emptyReason);
  const showEmptyReason = displayEmptyReason !== null && (!taxonomyState || Boolean(emptyReason?.trim() === ""));
  const resolvedDataQualityHref = dataQualityHref || "/analytics/data-quality";
  const resolvedRefreshStatusHref = refreshStatusHref || "/admin/configuration?panel=workers";
  const defaultActionLabels = taxonomyState?.action ? [taxonomyState.action.label, "Otvori kvalitet podataka", "Proveri status osvežavanja"] : variant === "filtered_out"
    ? ["Promenite filtere ili proširite period", "Otvori kvalitet podataka", "Proveri status osvežavanja"]
    : ["Proširi period", "Otvori kvalitet podataka", "Proveri status osvežavanja"];
  const taxonomyAction: EmptyStateAction[] = taxonomyState?.action
    ? [{
      label: taxonomyState.action.label,
      ...(taxonomyState.action.href ? { href: taxonomyState.action.href } : {}),
      ...(["pokušaj ponovo", "ponovo učitaj"].includes(taxonomyState.action.label.toLocaleLowerCase()) && onRetry ? { onClick: onRetry } : {}),
    }]
    : [];
  const defaultActions: EmptyStateAction[] = [...taxonomyAction, ...(variant === "filtered_out"
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
    ])].filter((action, index, allActions) => allActions.findIndex((candidate) => candidate.label === action.label) === index);
  const resolvedActions = loading ? [] : actions && actions.length > 0 ? actions : defaultActions;
  const isExecutableAction = (action: EmptyStateAction) => Boolean(action.onClick || action.href?.trim());
  const executableActions = resolvedActions.filter(isExecutableAction);
  const guidanceActions = resolvedActions.filter((action) => !isExecutableAction(action));

  if (meta && (meta.success !== true || Boolean(meta.errorCode?.trim()))) {
    return null;
  }

  function renderActionLink(href: string, label: string, className: string) {
    if (href.startsWith("/")) {
      return <Link to={href} className={className}>{label}</Link>;
    }

    return <a href={href} className={className}>{label}</a>;
  }

  return (
    <section className={`analytics-empty-state${variantClass}`} role="status" aria-live="polite" data-state-code={isSlowLoading ? "loading_slow" : resolvedState.code ?? undefined} data-tone={taxonomyState?.tone}>
      <h2>{displayTitle}</h2>
      {displayMessage ? <p>{displayMessage}</p> : null}
      {showEmptyReason ? <p className="aes-empty-reason">{displayEmptyReason}</p> : null}
      {resolvedState.rawCode ? (
        <details className="aes-technical-code">
          <summary>Detalji tehničkog koda</summary>
          <code>{resolvedState.rawCode}</code>
        </details>
      ) : null}

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

      {executableActions.length > 0 ? (
        <div className="aes-actions">
          <h3>Predlog akcija</h3>
          <ul>
            {executableActions.map((action) => (
              <li key={action.label}>
                {action.href?.trim() ? (
                  renderActionLink(action.href.trim(), action.label, "aes-action-link")
                ) : action.onClick ? (
                  <button type="button" className="aes-action-btn" onClick={action.onClick}>{action.label}</button>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {isSlowLoading ? (
        <div className="aes-loading-actions">
          {onRetry ? <button type="button" onClick={() => { setLoadingIsSlow(false); setLoadingAttempt((attempt) => attempt + 1); onRetry(); }}>Pokušaj ponovo</button> : null}
          {onCancel ? <button type="button" onClick={onCancel}>Otkaži</button> : null}
        </div>
      ) : null}

      {guidanceActions.length > 0 ? (
        <div className="aes-reasons">
          <h3>Smernice</h3>
          <ul>
            {guidanceActions.map((action) => <li key={action.label}>{action.label}</li>)}
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
