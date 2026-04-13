import { AlertTriangle } from "lucide-react";
import { useEffect, useState } from "react";
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
  const { online, checking, lastCheckedAt } = useBackendStatus();
  const rawWakeupSeconds = Number(import.meta.env.VITE_BACKEND_WAKEUP_SECONDS ?? 60);
  const rawNoticeDelayMs = Number(import.meta.env.VITE_BACKEND_WAKEUP_NOTICE_DELAY_MS ?? 1500);
  const wakeupSeconds = Number.isFinite(rawWakeupSeconds) && rawWakeupSeconds > 0 ? rawWakeupSeconds : 60;
  const noticeDelayMs = Number.isFinite(rawNoticeDelayMs) && rawNoticeDelayMs > 0 ? rawNoticeDelayMs : 1500;
  const [initialDelayElapsed, setInitialDelayElapsed] = useState(false);

  const isInitialProbe = checking && lastCheckedAt === null;
  const isRecovering = checking && !online;
  const isUnavailable = !checking && !online;

  useEffect(() => {
    if (!isInitialProbe) {
      setInitialDelayElapsed(false);
      return;
    }

    const timeoutId = window.setTimeout(() => {
      setInitialDelayElapsed(true);
    }, noticeDelayMs);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [isInitialProbe, noticeDelayMs]);

  if (!isRecovering && !isUnavailable && !(isInitialProbe && initialDelayElapsed)) {
    return null;
  }

  const title = checking ? "Budjenje servera, sacekajte..." : "Backend trenutno nije dostupan";
  const description = isInitialProbe || isRecovering
    ? `Prvi zahtev traje duze jer se backend budi iz rezima spavanja. Sacekajte ${buildWaitHint(wakeupSeconds)} i ostavite tab otvoren.`
    : `Server je i dalje nedostupan. Sacekajte ${buildWaitHint(wakeupSeconds)} i ostavite tab otvoren dok se ne vrati online.`;

  return (
    <div className="backend-wakeup-overlay" role="status" aria-live="polite">
      <section className="backend-wakeup-overlay__panel">
        <div className="backend-wakeup-overlay__icon-row">
          <UltraSpinner size="md" label="Waiting for backend to wake up" />
          <span className="backend-wakeup-overlay__badge">
            {checking ? (
              <>&#x21bb; Budjenje servera</>
            ) : (
              <><AlertTriangle size={14} /> Backend nije dostupan</>
            )}
          </span>
        </div>

        <h2>{title}</h2>
        <p>{description}</p>
        <p>Notice je informativan i ne blokira rad. Sakrice se cim backend ponovo postane dostupan.</p>
      </section>
    </div>
  );
}
