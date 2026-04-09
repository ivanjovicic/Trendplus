import { useEffect, useState } from "react";
import { useRequestActivity } from "../context/RequestActivityContext";
import UltraSpinner from "./ui/UltraSpinner";

const SHOW_DELAY_MS = 180;

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
    <div className="global-request-spinner" aria-live="polite">
      <div className="global-request-spinner__card">
        <UltraSpinner size="sm" label="Loading data" />
        <div className="global-request-spinner__content">
          <strong>Loading data</strong>
          <span>{activeRequests} request{activeRequests === 1 ? "" : "s"} in progress</span>
          <span className="global-request-spinner__bar" aria-hidden="true" />
        </div>
      </div>
    </div>
  );
}
