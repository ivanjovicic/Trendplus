import "./AnalyticsEmptyState.css";

type AnalyticsEmptyStateProps = {
  title: string;
  message?: string;
  reasons?: string[];
  actions?: string[];
};

export default function AnalyticsEmptyState({
  title,
  message,
  reasons,
  actions,
}: AnalyticsEmptyStateProps) {
  return (
    <section className="analytics-empty-state" role="status" aria-live="polite">
      <h2>{title}</h2>
      {message ? <p>{message}</p> : null}

      {reasons && reasons.length > 0 ? (
        <div>
          <h3>Moguci razlozi</h3>
          <ul>
            {reasons.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {actions && actions.length > 0 ? (
        <div>
          <h3>Predlog akcija</h3>
          <ul>
            {actions.map((action) => (
              <li key={action}>{action}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}

export type { AnalyticsEmptyStateProps };
