import { AlertTriangle } from "lucide-react";
import { useBackendStatus } from "../context/useBackendStatus";
import UltraSpinner from "./ui/UltraSpinner";

function buildWaitHint(seconds: number): string {
  if (seconds >= 90) {
    const minutes = Math.max(1, Math.round(seconds / 60));
    return `about ${minutes} minutes`;
  }
  return "about a minute";
}

export default function BackendWakeupNotice() {
  const { online, checking } = useBackendStatus();
  const rawWakeupSeconds = Number(import.meta.env.VITE_BACKEND_WAKEUP_SECONDS ?? 60);
  const wakeupSeconds = Number.isFinite(rawWakeupSeconds) && rawWakeupSeconds > 0 ? rawWakeupSeconds : 60;

  if (online || checking) {
    return null;
  }

  return (
    <div className="backend-wakeup-overlay" role="alert" aria-live="assertive">
      <section className="backend-wakeup-overlay__panel">
        <div className="backend-wakeup-overlay__icon-row">
          <UltraSpinner size="md" label="Waiting for backend to wake up" />
          <span className="backend-wakeup-overlay__badge">
            <AlertTriangle size={14} />
            Backend offline
          </span>
        </div>
        <h2>Backend is currently unavailable</h2>
        <p>
          The server may be waking up from sleep mode. Please wait {buildWaitHint(wakeupSeconds)} and keep this tab
          open.
        </p>
        <p>We will reconnect automatically as soon as the backend is online.</p>
      </section>
    </div>
  );
}
