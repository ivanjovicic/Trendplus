import { useCallback, useEffect, useMemo, useState } from "react";
import { disableWorkers, enableWorkers, getWorkersHealth, type WorkerHealthWithControl } from "../services/workersApi";
import { usePingControl } from "../context/PingControlContext";

const POLL_MS = import.meta.env.DEV ? 15000 : 45000;

export default function WorkerControlFlag() {
  const { apiPingEnabled } = usePingControl();
  const [health, setHealth] = useState<WorkerHealthWithControl | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (force = false) => {
    if (!apiPingEnabled && !force) {
      return;
    }

    try {
      const next = await getWorkersHealth();
      setHealth(next);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Greska pri citanju worker statusa.");
    } finally {
      setLoading(false);
    }
  }, [apiPingEnabled]);

  useEffect(() => {
    void load(true);
  }, [load]);

  useEffect(() => {
    if (!apiPingEnabled) {
      return;
    }

    void load();
    const id = window.setInterval(() => {
      void load();
    }, POLL_MS);
    return () => window.clearInterval(id);
  }, [load, apiPingEnabled]);

  const statusTone = useMemo(() => {
    if (error) return { bg: "#7f1d1d", border: "#f87171", text: "#fee2e2" };
    if (!health) return { bg: "#1f2937", border: "#6b7280", text: "#e5e7eb" };
    if (!health.workersEnabled) return { bg: "#1f2937", border: "#9ca3af", text: "#e5e7eb" };
    if (health.errorWorkers > 0) return { bg: "#7f1d1d", border: "#f87171", text: "#fee2e2" };
    if (health.staleWorkers > 0) return { bg: "#78350f", border: "#fbbf24", text: "#fef3c7" };
    return { bg: "#064e3b", border: "#34d399", text: "#d1fae5" };
  }, [error, health]);

  const statusText = useMemo(() => {
    if (loading) return "DB ping: ucitavanje...";
    if (error) return "DB ping: status nedostupan";
    if (!health) return "DB ping: nema podataka";
    if (!health.workersEnabled) return "DB ping: iskljucen";
    return `DB ping: ukljucen (${health.runningWorkers}/${health.totalWorkers})`;
  }, [error, health, loading]);

  const onToggle = useCallback(async () => {
    if (!health || busy) return;
    try {
      setBusy(true);
      setError(null);
      if (health.workersEnabled) {
        await disableWorkers();
      } else {
        await enableWorkers();
      }
      await load(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Neuspesna promena worker statusa.");
    } finally {
      setBusy(false);
    }
  }, [busy, health, load]);

  const buttonLabel = health?.workersEnabled ? "Stop DB" : "Start DB";

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
      <span
        style={{
          padding: "4px 8px",
          borderRadius: 999,
          border: `1px solid ${statusTone.border}`,
          background: statusTone.bg,
          color: statusTone.text,
          fontSize: 12,
          fontWeight: 700,
          whiteSpace: "nowrap",
        }}
        title={health ? `Env: ${health.environment ?? "n/a"} | Last switch: ${health.lastSwitchAtUtc ?? "n/a"}` : "Worker status"}
      >
        {statusText}
      </span>
      <button
        type="button"
        onClick={() => void onToggle()}
        disabled={busy || loading || !!error}
        style={{
          padding: "5px 10px",
          borderRadius: 8,
          border: "1px solid #4b5563",
          background: busy ? "#374151" : "#1f2937",
          color: "white",
          fontSize: 12,
          fontWeight: 600,
          cursor: busy || loading || !!error ? "not-allowed" : "pointer",
          opacity: busy || loading || !!error ? 0.6 : 1,
        }}
      >
        {busy ? "..." : buttonLabel}
      </button>
      <button
        type="button"
        onClick={() => void load(true)}
        disabled={busy}
        style={{
          padding: "5px 8px",
          borderRadius: 8,
          border: "1px solid #4b5563",
          background: "#1f2937",
          color: "white",
          fontSize: 11,
          cursor: busy ? "not-allowed" : "pointer",
          opacity: busy ? 0.6 : 1,
        }}
        title="Osvezi worker status"
      >
        Osvezi
      </button>
    </div>
  );
}
