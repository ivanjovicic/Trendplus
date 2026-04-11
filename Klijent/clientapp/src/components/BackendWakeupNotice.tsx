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
  const { online, checking, lastCheckedAt } = useBackendStatus();
  const rawWakeupSeconds = Number(import.meta.env.VITE_BACKEND_WAKEUP_SECONDS ?? 60);
  const wakeupSeconds = Number.isFinite(rawWakeupSeconds) && rawWakeupSeconds > 0 ? rawWakeupSeconds : 60;

  if (online || (checking && lastCheckedAt === null)) {
    return null;
  }

  return (
    <div className="backend-wakeup-overlay" role="status" aria-live="polite">
      <section className="backend-wakeup-overlay__panel">
        <div className="backend-wakeup-overlay__icon-row">
          <UltraSpinner size="md" label="Waiting for backend to wake up" />
          <span className="backend-wakeup-overlay__badge">
            {checking ? (
              <>&#x21bb; Proverava se...</>
            ) : (
              <><AlertTriangle size={14} /> Backend nije dostupan</>
            )}
          </span>
        </div>

        <h2>{checking ? "Proveravamo konekciju..." : "Backend trenutno nije dostupan"}</h2>
        <p>Server se mozda budi iz rezima spavanja. Sacekajte {buildWaitHint(wakeupSeconds)} i ostavite tab otvoren.</p>
        <p>Notice je informativan i ne blokira rad. Sakrice se cim backend ponovo postane dostupan.</p>
      </section>
    </div>
  );
}
