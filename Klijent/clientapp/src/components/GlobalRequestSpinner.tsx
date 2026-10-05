import { useEffect, useState } from "react";
import { useRequestActivity } from "../context/RequestActivityContext";
import UltraSpinner from "./ui/UltraSpinner";

const SHOW_DELAY_MS = 180;
const LOADING_LABEL = "Učitavanje podataka";

/** Serbian plural for "zahtev" (prompt-simplified: 1 zahtev, else zahteva). */
export function formatActiveRequestsLabel(count: number): string {
  const n = Math.max(0, Math.floor(Number.isFinite(count) ? count : 0));
  const noun = n === 1 ? "zahtev" : "zahteva";
  return `${n} ${noun} u toku`;
}

export default function GlobalRequestSpinner() {
  const { activeRequests, hasActiveRequests } = useRequestActivity();
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (!hasActiveRequests) {
      setVisible(false);
      return;
    }

    const timeoutId = window.setTimeout(() => setVisible(true), SHOW_DELAY_MS);
    return () => window.clearTimeout(timeoutId);
  }, [hasActiveRequests]);

  if (!visible) return null;

  return (
    <div className="global-request-spinner" aria-live="polite" data-testid="global-request-spinner">
      <div className="global-request-spinner__card">
        <UltraSpinner size="sm" label={LOADING_LABEL} />
        <div className="global-request-spinner__content">
          <strong>{LOADING_LABEL}</strong>
          <span>{formatActiveRequestsLabel(activeRequests)}</span>
          <span className="global-request-spinner__bar" aria-hidden="true" />
        </div>
      </div>
    </div>
  );
}
