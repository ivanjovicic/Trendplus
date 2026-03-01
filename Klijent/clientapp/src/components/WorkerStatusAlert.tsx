import { useEffect, useState, useCallback } from "react";
import { usePingControl } from "../context/PingControlContext";
import { apiUrl } from "../utils/apiUrl";

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
      const res = await fetch(apiUrl("/api/workers/health"));
      if (!res.ok) {
        if (res.status === 404) {
          setError("Worker health endpoint nije dostupan.");
        } else {
          setError(`Neuspešno čitanje worker statusa (HTTP ${res.status})`);
        }
        return;
      }

      const data = (await res.json()) as WorkerHealthSummary;
      setHealth(data);
      setError(null);
    } catch {
      setError("Nije moguće povezivanje sa backend-om.");
    }
  }, []);

  useEffect(() => {
    if (!apiPingEnabled) {
      return;
    }

    void fetchHealth();
    const interval = setInterval(fetchHealth, WORKER_HEALTH_POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [fetchHealth, apiPingEnabled]);

  if (!apiPingEnabled) return null;
  if (dismissed) return null;
  if (!health && !error) return null;
  if (health && !health.hasCriticalIssues && health.errorWorkers === 0 && health.staleWorkers === 0 && !error) {
    return null;
  }

  const style = (() => {
    if (error) return { background: "#fef2f2", borderColor: "#dc2626", color: "#991b1b" };
    if ((health?.errorWorkers ?? 0) > 0) return { background: "#fef2f2", borderColor: "#dc2626", color: "#991b1b" };
    if ((health?.staleWorkers ?? 0) > 0) return { background: "#fffbeb", borderColor: "#f59e0b", color: "#92400e" };
    return { background: "#f0fdf4", borderColor: "#059669", color: "#065f46" };
  })();

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleTimeString("sr-RS", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
  };

  const getStatusIcon = (status: string, isStale: boolean) => {
    if (isStale) return "⚠️";
    switch (status) {
      case "Healthy":
        return "✅";
      case "Running":
        return "🔄";
      case "Error":
        return "❌";
      case "Stopped":
        return "⏹️";
      default:
        return "❔";
    }
  };

  return (
    <div
      style={{
        position: "fixed",
        bottom: 20,
        right: 20,
        maxWidth: 420,
        zIndex: 9999,
        background: style.background,
        border: `2px solid ${style.borderColor}`,
        borderRadius: 12,
        padding: 16,
        boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
        fontFamily: "system-ui, -apple-system, sans-serif",
      }}
    >
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: expanded ? 12 : 0,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <span style={{ fontSize: 20 }}>
            {error ? "🔴" : (health?.errorWorkers ?? 0) > 0 ? "❌" : (health?.staleWorkers ?? 0) > 0 ? "⚠️" : "✅"}
          </span>
          <div>
            <div style={{ fontWeight: 700, fontSize: 14, color: style.color }}>
              {error
                ? "Monitoring workera nedostupan"
                : (health?.errorWorkers ?? 0) > 0
                ? `${health?.errorWorkers} worker ima grešku`
                : (health?.staleWorkers ?? 0) > 0
                ? `${health?.staleWorkers} worker ne šalje heartbeat`
                : "Svi workeri su zdravi"}
            </div>
            {!expanded && health && !error && (
              <div style={{ fontSize: 12, opacity: 0.8, marginTop: 2 }}>
                {health.totalWorkers} workera • Klikni za detalje
              </div>
            )}
          </div>
        </div>

        <div style={{ display: "flex", gap: 8 }}>
          <button
            type="button"
            onClick={() => setExpanded(!expanded)}
            style={{ background: "transparent", border: "none", cursor: "pointer", fontSize: 16, padding: 4 }}
            title={expanded ? "Smanji" : "Proširi"}
          >
            {expanded ? "🔼" : "🔽"}
          </button>
          <button
            type="button"
            onClick={() => setDismissed(true)}
            style={{ background: "transparent", border: "none", cursor: "pointer", fontSize: 16, padding: 4 }}
            title="Zatvori"
          >
            ✕
          </button>
        </div>
      </div>

      {expanded && health && (
        <div style={{ marginTop: 8 }}>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(3, 1fr)",
              gap: 8,
              marginBottom: 12,
              padding: 8,
              background: "rgba(255,255,255,0.5)",
              borderRadius: 8,
            }}
          >
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: 20, fontWeight: 700 }}>{health.healthyWorkers}</div>
              <div style={{ fontSize: 11, opacity: 0.7 }}>Zdravi</div>
            </div>
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: 20, fontWeight: 700, color: health.errorWorkers > 0 ? "#dc2626" : undefined }}>
                {health.errorWorkers}
              </div>
              <div style={{ fontSize: 11, opacity: 0.7 }}>Greške</div>
            </div>
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: 20, fontWeight: 700, color: health.staleWorkers > 0 ? "#f59e0b" : undefined }}>
                {health.staleWorkers}
              </div>
              <div style={{ fontSize: 11, opacity: 0.7 }}>Stale</div>
            </div>
          </div>

          <div style={{ maxHeight: 220, overflowY: "auto" }}>
            {health.workers.map((worker, idx) => (
              <div
                key={idx}
                style={{
                  padding: 8,
                  marginBottom: 4,
                  background: worker.status === "Error" || worker.isStale ? "rgba(220,38,38,0.1)" : "rgba(255,255,255,0.5)",
                  borderRadius: 6,
                  fontSize: 12,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span>{getStatusIcon(worker.status, worker.isStale)}</span>
                    <span style={{ fontWeight: 600 }}>{worker.workerName}</span>
                  </div>
                  <span style={{ opacity: 0.7 }}>{formatTime(worker.lastHeartbeat)}</span>
                </div>

                {worker.message && <div style={{ marginTop: 4, opacity: 0.8, fontSize: 11 }}>{worker.message}</div>}

                {worker.lastError && (
                  <div style={{ marginTop: 4, color: "#dc2626", fontSize: 11, background: "rgba(220,38,38,0.1)", padding: 4, borderRadius: 4 }}>
                    ❌ {worker.lastError}
                    {worker.errorCount > 1 && ` (${worker.errorCount}x)`}
                  </div>
                )}

                {worker.isStale && (
                  <div style={{ marginTop: 4, color: "#f59e0b", fontSize: 11, fontWeight: 600 }}>
                    ⚠️ Worker nije poslao heartbeat duže od 10 minuta
                  </div>
                )}
              </div>
            ))}
          </div>

          <button
            type="button"
            onClick={() => void fetchHealth()}
            style={{
              marginTop: 8,
              width: "100%",
              padding: "8px 12px",
              background: style.borderColor,
              color: "white",
              border: "none",
              borderRadius: 6,
              cursor: "pointer",
              fontSize: 13,
              fontWeight: 600,
            }}
          >
            🔄 Osveži status
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
              background: "#dc2626",
              color: "white",
              border: "none",
              borderRadius: 6,
              cursor: "pointer",
              fontSize: 13,
              fontWeight: 600,
            }}
          >
            🔄 Pokušaj ponovo
          </button>
        </div>
      )}
    </div>
  );
}
