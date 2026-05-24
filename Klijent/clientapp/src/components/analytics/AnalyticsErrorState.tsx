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
  "Proverite refresh status.",
  "Proverite kvalitet podataka.",
  "Pokusajte ponovo.",
  "Ako se greska ponavlja, sacuvajte correlation ID i kontaktirajte podrsku.",
];

export default function AnalyticsErrorState({
  title,
  message,
  errorCode,
  correlationId,
  suggestions,
  retryLabel = "Pokusaj ponovo",
  onRetry,
  helpHref,
  helpLabel,
}: AnalyticsErrorStateProps) {
  const resolvedSuggestions = suggestions && suggestions.length > 0 ? suggestions : DEFAULT_SUGGESTIONS;
  const displayMessage = message || "Ne prikazujemo nule jer nije potvrdjeno da je period stvarno prazan.";

  return (
    <section className="analytics-error-state" role="alert" aria-live="assertive">
      <h2>{title}</h2>
      <p>{displayMessage}</p>
      {errorCode ? <p className="aes-code">Sifra greske: {errorCode}</p> : null}
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
        {helpHref ? <Link to={helpHref}>{helpLabel || "Otvori data quality"}</Link> : null}
      </div>
    </section>
  );
}

export type { AnalyticsErrorStateProps };
