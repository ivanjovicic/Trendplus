import { Link } from "react-router-dom";
import "./AnalyticsErrorState.css";

type AnalyticsErrorStateProps = {
  title: string;
  message: string;
  errorCode?: string | null;
  suggestions?: string[];
  retryLabel?: string;
  onRetry?: () => void;
  helpHref?: string;
  helpLabel?: string;
};

export default function AnalyticsErrorState({
  title,
  message,
  errorCode,
  suggestions,
  retryLabel = "Pokusaj ponovo",
  onRetry,
  helpHref,
  helpLabel,
}: AnalyticsErrorStateProps) {
  return (
    <section className="analytics-error-state" role="alert" aria-live="assertive">
      <h2>{title}</h2>
      <p>{message || "Podaci trenutno nisu dostupni."}</p>
      {errorCode ? <p className="aes-code">Sifra greske: {errorCode}</p> : null}
      {suggestions && suggestions.length > 0 ? (
        <ul className="aes-suggestions">
          {suggestions.map((item) => (
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
