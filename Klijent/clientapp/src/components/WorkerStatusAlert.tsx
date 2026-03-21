import { useEffect, useState, useCallback } from "react";
import { usePingControl } from "../context/PingControlContext";
import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

interface WorkerStatus {
  workerName: string;
  status: string;
  lastHeartbeat: string;
  message?: string;
  lastError?: string;
  lastErrorTime?: string;
  errorCount: number;
  isStale: boolean;
}

interface WorkerHealthSummary {
  totalWorkers: number;
  healthyWorkers: number;
  runningWorkers: number;
  errorWorkers: number;
  stoppedWorkers: number;
  staleWorkers: number;
  hasCriticalIssues: boolean;
  workers: WorkerStatus[];
}

const WORKER_HEALTH_POLL_INTERVAL_MS = import.meta.env.DEV ? 30000 : 120000;

export default function WorkerStatusAlert() {
  const { apiPingEnabled } = usePingControl();
  const [health, setHealth] = useState<WorkerHealthSummary | null>(null);
  const [dismissed, setDismissed] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchHealth = useCallback(async () => {
    try {
      const res = await fetchWithTimeout(apiUrl("/api/workers/health"), undefined, 10_000);
      if (!res.ok) {
        if (res.status === 404) {
          setError("Worker health endpoint nije dostupan.");
        } else {
          setError(`Neuspesno citanje worker statusa (HTTP ${res.status})`);
        }
        return;
      }

      const data = (await res.json()) as WorkerHealthSummary;
      setHealth(data);
      setError(null);
    } catch {
      setError("Nije moguce povezivanje sa backend-om.");
    }
  }, []);

  useEffect(() => {
    if (!apiPingEnabled) {
      return;
    }

    void fetchHealth();
    const interval = window.setInterval(() => {
      void fetchHealth();
    }, WORKER_HEALTH_POLL_INTERVAL_MS);

    return () => window.clearInterval(interval);
  }, [fetchHealth, apiPingEnabled]);

  if (!apiPingEnabled) return null;
  if (dismissed) return null;
  if (!health && !error) return null;
  if (health && !health.hasCriticalIssues && health.errorWorkers === 0 && health.staleWorkers === 0 && !error) {
    return null;
  }

  const style = (() => {
    if (error) return { background: "var(--surface-elevated)", borderColor: "var(--error)", color: "var(--error)" };
    if ((health?.errorWorkers ?? 0) > 0) return { background: "var(--surface-elevated)", borderColor: "var(--error)", color: "var(--error)" };
    if ((health?.staleWorkers ?? 0) > 0) return { background: "var(--surface-elevated)", borderColor: "var(--warning)", color: "var(--warning)" };
    return { background: "var(--surface-elevated)", borderColor: "var(--success)", color: "var(--success)" };
  })();

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleTimeString("sr-RS", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
  };

  const getStatusGlyph = (status: string, isStale: boolean) => {
    if (isStale) return "!";
    switch (status) {
      case "Healthy":
        return "OK";
      case "Running":
        return "...";
      case "Error":
        return "ERR";
      case "Stopped":
        return "STOP";
      default:
        return "?";
    }
  };

  return (
    <div className="fixed bottom-5 right-5 z-[9999] rounded-xl p-4 shadow-lg min-w-[280px] max-w-[420px] border-2" style={{ background: style.background, borderColor: style.borderColor, fontFamily: "system-ui, -apple-system, sans-serif" }}>
      <div className="flex justify-between items-center" style={{ marginBottom: expanded ? 12 : 0 }}>
        <div className="flex items-center gap-2">
          <span className="font-extrabold text-[13px]">{error ? "ERR" : (health?.errorWorkers ?? 0) > 0 ? "ERR" : (health?.staleWorkers ?? 0) > 0 ? "WARN" : "OK"}</span>
          <div>
            <div className="font-bold text-sm" style={{ color: style.color }}>
              {error
                ? "Monitoring workera nedostupan"
                : (health?.errorWorkers ?? 0) > 0
                ? `${health?.errorWorkers} worker ima gresku`
                : (health?.staleWorkers ?? 0) > 0
                ? `${health?.staleWorkers} worker ne salje heartbeat`
                : "Svi workeri su zdravi"}
            </div>
            {!expanded && health && !error && (
              <div className="text-[12px] opacity-80 mt-1">
                {health.totalWorkers} workera | Klikni za detalje
              </div>
            )}
          </div>
        </div>

        <div className="flex gap-2">
          <button type="button" onClick={() => setExpanded(!expanded)} className="p-1 text-lg" title={expanded ? "Smanji" : "Prosiri"}>
            {expanded ? "^" : "v"}
          </button>
          <button type="button" onClick={() => setDismissed(true)} className="p-1 text-lg" title="Zatvori">
            x
          </button>
        </div>
      </div>

      {expanded && health && (
        <div style={{ marginTop: 8 }}>
          <div className="grid grid-cols-3 gap-2 mb-3 p-2 rounded" style={{ background: 'rgba(255,255,255,0.5)' }}>
            <div className="text-center">
              <div className="text-2xl font-bold">{health.healthyWorkers}</div>
              <div className="text-xs opacity-70">Zdravi</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold" style={{ color: health.errorWorkers > 0 ? 'var(--error)' : undefined }}>{health.errorWorkers}</div>
              <div className="text-xs opacity-70">Greske</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold" style={{ color: health.staleWorkers > 0 ? 'var(--warning)' : undefined }}>{health.staleWorkers}</div>
              <div className="text-xs opacity-70">Stale</div>
            </div>
          </div>

          <div className="max-h-[220px] overflow-y-auto">
            {health.workers.map((worker, idx) => (
              <div key={idx} className="p-2 mb-1 rounded text-sm" style={{ background: worker.status === "Error" || worker.isStale ? 'rgba(220,38,38,0.1)' : 'rgba(255,255,255,0.5)' }}>
                <div className="flex justify-between items-center">
                  <div className="flex items-center gap-1">
                    <span>{getStatusGlyph(worker.status, worker.isStale)}</span>
                    <span className="font-semibold">{worker.workerName}</span>
                  </div>
                  <span className="opacity-70">{formatTime(worker.lastHeartbeat)}</span>
                </div>

                {worker.message && <div className="mt-1 opacity-80 text-[11px]">{worker.message}</div>}

                {worker.lastError && (
                  <div className="mt-1 p-1 rounded text-[11px]" style={{ color: 'var(--error)', background: 'rgba(220,38,38,0.1)' }}>
                    ERR {worker.lastError}
                    {worker.errorCount > 1 && ` (${worker.errorCount}x)`}
                  </div>
                )}

                {worker.isStale && (
                  <div className="mt-1 text-[11px] font-semibold" style={{ color: 'var(--warning)' }}>
                    WARN Worker nije poslao heartbeat duze od 10 minuta
                  </div>
                )}
              </div>
            ))}
          </div>

          <button type="button" onClick={() => void fetchHealth()} className="mt-2 w-full py-2 rounded-md text-white font-semibold" style={{ background: style.borderColor }}>
            Osvezi status
          </button>
        </div>
      )}

      {error && (
        <div style={{ marginTop: 8, fontSize: 12 }}>
          <div style={{ opacity: 0.8, marginBottom: 8 }}>{error}</div>
          <button
            type="button"
            onClick={() => void fetchHealth()}
            style={{
              width: "100%",
              padding: "8px 12px",
              background: "var(--error)",
              color: "white",
              border: "none",
              borderRadius: 6,
              cursor: "pointer",
              fontSize: 13,
              fontWeight: 600,
            }}
          >
            Pokusaj ponovo
          </button>
        </div>
      )}
    </div>
  );
}
