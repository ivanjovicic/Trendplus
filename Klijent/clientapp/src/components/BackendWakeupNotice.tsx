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
  const { online, checking } = useBackendStatus();
  const rawWakeupSeconds = Number(import.meta.env.VITE_BACKEND_WAKEUP_SECONDS ?? 60);
  const wakeupSeconds = Number.isFinite(rawWakeupSeconds) && rawWakeupSeconds > 0 ? rawWakeupSeconds : 60;

  // Hide only when confirmed online — don't hide while checking (avoids flicker)
  if (online) {
    return null;
  }

  return (
    <div className="backend-wakeup-overlay" role="alert" aria-live="assertive">
      <section className="backend-wakeup-overlay__panel">
        <div className="backend-wakeup-overlay__icon-row">
          <UltraSpinner size="md" label="Waiting for backend to wake up" />
          <span className="backend-wakeup-overlay__badge">
            {checking ? (
              <>&#x21bb; Proverava se&hellip;</>
            ) : (
              <><AlertTriangle size={14} /> Backend nije dostupan</>
            )}
          </span>
        </div>
        <h2>{checking ? "Provera konekcije…" : "Backend trenutno nedostupan"}</h2>
        <p>
          Server se možda budi iz režima spavanja. Sačekajte {buildWaitHint(wakeupSeconds)} i ostavite tab otvoren.
        </p>
        <p>Automatski ćemo se ponovo priključiti čim backend postane dostupan.</p>
      </section>
    </div>
  );
}
