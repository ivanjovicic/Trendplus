import { Link } from "react-router-dom";
import { useState } from "react";
import type { AnalyticsResponseMeta } from "../../types/analytics";
import {
  ANALYTICS_EMPTY_ERROR_FALLBACK_MESSAGE,
  getSafeAnalyticsErrorMessage,
} from "../../utils/analyticsErrorMessages";
import { resolveAnalyticsState } from "../../utils/analyticsStateTaxonomy";
import "./AnalyticsErrorState.css";

type AnalyticsErrorStateProps = {
  title?: string;
  message?: string;
  code?: string | null;
  meta?: AnalyticsResponseMeta | null;
  errorCode?: string | null;
  showErrorCode?: boolean;
  correlationId?: string | null;
  readinessId?: string | null;
  recoveryInstruction?: string | null;
  contextMessage?: string | null;
  suggestions?: string[];
  retryLabel?: string;
  onRetry?: () => void;
  helpHref?: string;
  helpLabel?: string;
};

const DEFAULT_SUGGESTIONS = [
  "Proverite status osvežavanja.",
  "Proverite kvalitet podataka.",
  "Pokušajte ponovo.",
  "Ako se greška ponavlja, sačuvajte correlation ID i kontaktirajte podršku.",
];

function renderLink(href: string, label: string, className?: string) {
  if (href.startsWith("/")) {
    return <Link to={href} className={className}>{label}</Link>;
  }

  return <a href={href} className={className}>{label}</a>;
}

export default function AnalyticsErrorState({
  title,
  message,
  code,
  meta,
  errorCode,
  showErrorCode = false,
  correlationId,
  readinessId,
  recoveryInstruction,
  contextMessage,
  suggestions,
  retryLabel = "Pokušaj ponovo",
  onRetry,
  helpHref,
  helpLabel,
}: AnalyticsErrorStateProps) {
  const [copiedCorrelationId, setCopiedCorrelationId] = useState(false);
  const resolvedState = resolveAnalyticsState(code ?? errorCode, meta);
  const stateDefinition = resolvedState.definition?.kind === "error" ? resolvedState.definition : null;
  const resolvedSuggestions = suggestions && suggestions.length > 0 ? suggestions : DEFAULT_SUGGESTIONS;
  const resolvedErrorCode = errorCode ?? meta?.errorCode ?? null;
  const resolvedCorrelationId = correlationId ?? meta?.correlationId ?? null;
  const resolvedReadinessId = readinessId ?? meta?.readinessId ?? null;
  const resolvedRecoveryInstruction = recoveryInstruction ?? meta?.recoveryInstruction ?? null;
  const hasMappedState = Boolean(resolvedState.code && resolvedState.code !== "unknown_code");
  const resolvedMessage = message ?? meta?.errorMessage ?? meta?.message ?? stateDefinition?.message ?? "";
  const displayMessage = getSafeAnalyticsErrorMessage(
    resolvedMessage,
    resolvedErrorCode,
    hasMappedState && stateDefinition ? stateDefinition.message : ANALYTICS_EMPTY_ERROR_FALLBACK_MESSAGE,
  );
  const resolvedTitle = hasMappedState ? stateDefinition?.title ?? title ?? "Analitika nije dostupna" : title ?? "Analitika nije dostupna";
  const showTechnicalCode = Boolean(resolvedState.rawCode || (showErrorCode && resolvedErrorCode));
  const canRetry = Boolean(onRetry || stateDefinition?.retryable);
  const resolvedHelpHref = helpHref ?? (stateDefinition?.action?.label === "Pokušaj ponovo" ? undefined : stateDefinition?.action?.href);
  const resolvedHelpLabel = helpLabel ?? (helpHref ? "Otvori kvalitet podataka" : stateDefinition?.action?.label);

  async function copyCorrelationId() {
    if (!resolvedCorrelationId || !navigator.clipboard?.writeText) return;
    try {
      await navigator.clipboard.writeText(resolvedCorrelationId);
      setCopiedCorrelationId(true);
    } catch {
      setCopiedCorrelationId(false);
    }
  }

  return (
    <section className="analytics-error-state" role="alert" aria-live="assertive" data-state-code={resolvedState.code ?? undefined} data-tone={stateDefinition?.tone}>
      <h2>{resolvedTitle}</h2>
      <p>{displayMessage}</p>
      {resolvedReadinessId ? <p className="aes-code">ID provere: {resolvedReadinessId}</p> : null}
      {resolvedRecoveryInstruction ? <p>{resolvedRecoveryInstruction}</p> : null}
      {contextMessage ? <p>{contextMessage}</p> : null}
      {resolvedCorrelationId ? (
        <div className="aes-correlation-id">
          <p className="aes-code">Correlation ID: {resolvedCorrelationId}</p>
          <button type="button" onClick={() => { void copyCorrelationId(); }}>Kopiraj ID</button>
          {copiedCorrelationId ? <span role="status">ID je kopiran.</span> : null}
        </div>
      ) : null}
      {showTechnicalCode && (resolvedState.rawCode || resolvedErrorCode) ? (
        <details className="aes-code">
          <summary>Detalji tehničkog koda</summary>
          <code>{resolvedState.rawCode ?? resolvedErrorCode}</code>
        </details>
      ) : null}
      {resolvedSuggestions.length > 0 ? (
        <ul className="aes-suggestions">
          {resolvedSuggestions.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : null}
      <div className="aes-actions">
        {canRetry ? (
          <button type="button" onClick={onRetry ?? (() => window.location.reload())}>
            {stateDefinition?.action?.label ?? retryLabel}
          </button>
        ) : null}
        {resolvedHelpHref ? renderLink(resolvedHelpHref, resolvedHelpLabel || "Otvori kvalitet podataka") : null}
      </div>
    </section>
  );
}

export type { AnalyticsErrorStateProps };
