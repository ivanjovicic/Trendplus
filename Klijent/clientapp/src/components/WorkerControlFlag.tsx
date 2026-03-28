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
    if (!apiPingEnabled) {
      setLoading(false);
      return;
    }

    void load(true);

    const id = window.setInterval(() => {
      void load();
    }, POLL_MS);

    return () => window.clearInterval(id);
  }, [load, apiPingEnabled]);

  const statusTone = useMemo(() => {
    if (error) return { bg: "var(--error, #ef4444)", border: "var(--error, #ef4444)", text: "var(--text-primary, #0f172a)" };
    if (!health) return { bg: "var(--surface-default, #f4f7fb)", border: "var(--border-default, #d3dce9)", text: "var(--text-primary, #0f172a)" };
    if (!health.workersEnabled) return { bg: "var(--surface-default, #f4f7fb)", border: "var(--border-hover, #a7b9d3)", text: "var(--text-primary, #0f172a)" };
    if (health.errorWorkers > 0) return { bg: "var(--error, #ef4444)", border: "var(--error, #ef4444)", text: "var(--text-primary, #0f172a)" };
    if (health.staleWorkers > 0) return { bg: "var(--warning, #f59e0b)", border: "var(--warning, #f59e0b)", text: "var(--text-primary, #0f172a)" };
    return { bg: "var(--success, #10b981)", border: "var(--success, #10b981)", text: "var(--text-primary, #0f172a)" };
  }, [error, health]);

  const statusText = useMemo(() => {
    if (loading) return "Workeri: ucitavanje...";
    if (error) return "Workeri: status nedostupan";
    if (!health) return "Workeri: nema podataka";
    if (!health.workersEnabled) return "Workeri: iskljuceni";
    return `Workeri: ukljuceni (${health.runningWorkers}/${health.totalWorkers})`;
  }, [error, health, loading]);

  const onToggle = useCallback(async () => {
    if (!health || busy) return;
    if (!health.runtimeToggleAllowed && !health.workersEnabled) return;
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

  const buttonLabel = health?.workersEnabled ? "Stop workers" : "Start workers";
  const toggleDisabled = busy || loading || !!error || !health || (!health.runtimeToggleAllowed && !health.workersEnabled);
  const toggleTitle = !health
    ? "Worker control"
    : !health.runtimeToggleAllowed && !health.workersEnabled
      ? "U ovoj okolini ukljucivanje workera je zakljucano."
      : "Promeni worker status";

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
        disabled={toggleDisabled}
        style={{
          padding: "5px 10px",
          borderRadius: 8,
          border: "1px solid var(--border-default, #d3dce9)",
          background: busy ? "var(--surface-elevated, #ffffff)" : "var(--surface-default, #f4f7fb)",
          color: "var(--text-on-surface, #0f172a)",
          fontSize: 12,
          fontWeight: 600,
          cursor: toggleDisabled ? "not-allowed" : "pointer",
          opacity: toggleDisabled ? 0.6 : 1,
        }}
        title={toggleTitle}
      >
        {busy ? "..." : (!health?.runtimeToggleAllowed && !health?.workersEnabled ? "Locked" : buttonLabel)}
      </button>
      <button
        type="button"
        onClick={() => void load(true)}
        disabled={busy}
        style={{
          padding: "5px 8px",
          borderRadius: 8,
          border: "1px solid var(--border-default, #d3dce9)",
          background: "var(--surface-default, #f4f7fb)",
          color: "var(--text-on-surface, #0f172a)",
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
