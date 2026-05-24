import { Link } from "react-router-dom";
import "./AnalyticsErrorState.css";

type AnalyticsErrorStateProps = {
  title: string;
  message: string;
  errorCode?: string | null;
  correlationId?: string | null;
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
  errorCode,
  correlationId,
  suggestions,
  retryLabel = "Pokušaj ponovo",
  onRetry,
  helpHref,
  helpLabel,
}: AnalyticsErrorStateProps) {
  const resolvedSuggestions = suggestions && suggestions.length > 0 ? suggestions : DEFAULT_SUGGESTIONS;
  const displayMessage = message || "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan.";

  return (
    <section className="analytics-error-state" role="alert" aria-live="assertive">
      <h2>{title}</h2>
      <p>{displayMessage}</p>
      {errorCode ? <p className="aes-code">Šifra greške: {errorCode}</p> : null}
      {correlationId ? <p className="aes-code">Correlation ID: {correlationId}</p> : null}
      {resolvedSuggestions.length > 0 ? (
        <ul className="aes-suggestions">
          {resolvedSuggestions.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : null}
      <div className="aes-actions">
        {onRetry ? (
          <button type="button" onClick={onRetry}>
            {retryLabel}
          </button>
        ) : null}
        {helpHref ? renderLink(helpHref, helpLabel || "Otvori kvalitet podataka") : null}
      </div>
    </section>
  );
}

export type { AnalyticsErrorStateProps };
