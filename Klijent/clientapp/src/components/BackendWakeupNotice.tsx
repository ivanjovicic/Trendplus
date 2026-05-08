import { AlertTriangle } from "lucide-react";
import { useBackendStatus } from "../context/useBackendStatus";
import UltraSpinner from "./ui/UltraSpinner";

function buildWaitHint(seconds: number): string {
  if (seconds >= 90) {
    const minutes = Math.max(1, Math.round(seconds / 60));
    return `oko ${minutes} minuta`;
  }

  return "oko minut";
}

export default function BackendWakeupNotice() {
  const { status, checking, lastError, recoveryNoticeVisible, providerState } = useBackendStatus();
  const rawWakeupSeconds = Number(import.meta.env.VITE_BACKEND_WAKEUP_SECONDS ?? 60);
  const wakeupSeconds = Number.isFinite(rawWakeupSeconds) && rawWakeupSeconds > 0 ? rawWakeupSeconds : 60;

  const isUnavailable = status === "down" || status === "recovering";
  const isReconnecting = isUnavailable && checking;
  const usingFallback = providerState.activeHost === "fallback";

  if (!isUnavailable) {
    if (recoveryNoticeVisible) {
      return (
        <div className="backend-wakeup-overlay backend-wakeup-overlay--success" role="status" aria-live="polite">
          <section className="backend-wakeup-overlay__panel">
            <div className="backend-wakeup-overlay__icon-row">
              <span className="backend-wakeup-overlay__badge">&#x2713; Backend je online</span>
            </div>
            <h2>Veza je ponovo uspostavljena</h2>
            <p>Podaci se sada ucitavaju iz aktivnog backend servisa.</p>
          </section>
        </div>
      );
    }

    return null;
  }

  const title = isReconnecting ? "Backend se budi..." : "Backend trenutno nije dostupan";
  const description = isReconnecting
    ? `Proveravamo da li se server vratio online. Sacekajte ${buildWaitHint(wakeupSeconds)} i ostavite tab otvoren.`
    : `Server je i dalje nedostupan. Sacekajte ${buildWaitHint(wakeupSeconds)} i ostavite tab otvoren dok se ne vrati online.`;

  return (
    <div className="backend-wakeup-overlay" role="status" aria-live="polite">
      <section className="backend-wakeup-overlay__panel">
        <div className="backend-wakeup-overlay__icon-row">
          <UltraSpinner size="md" label="Waiting for backend to wake up" />
          <span className="backend-wakeup-overlay__badge">
            {isReconnecting ? (
              <>&#x21bb; Povezivanje...</>
            ) : (
              <><AlertTriangle size={14} /> Backend nije dostupan</>
            )}
          </span>
        </div>

        <h2>{title}</h2>
        <p>{description}</p>
        {usingFallback ? <p className="backend-wakeup-overlay__meta">Aktivan je fallback backend.</p> : null}
        {lastError ? <p className="backend-wakeup-overlay__meta">Poslednji signal: {lastError}</p> : null}
        <p>Notice je informativan i ne blokira rad. Sakrice se cim backend ponovo postane dostupan.</p>
      </section>
    </div>
  );
}
