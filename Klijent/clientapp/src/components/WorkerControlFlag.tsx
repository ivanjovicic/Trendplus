import { useCallback, useEffect, useMemo, useState } from "react";
import { Bot, RefreshCw } from "lucide-react";
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
      setError(e instanceof Error ? e.message : "Greška pri čitanju worker statusa.");
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

  const toneClass = useMemo(() => {
    if (error) return "border-[var(--error)]/50 bg-error-soft text-[var(--error)]";
    if (!health) return "border-muted bg-[var(--surface-darker)] text-muted";
    if (!health.workersEnabled) return "border-muted bg-[var(--surface-darker)] text-muted";
    if (health.errorWorkers > 0) return "border-[var(--error)]/50 bg-error-soft text-[var(--error)]";
    if (health.staleWorkers > 0) return "border-[var(--warning)]/50 bg-warning-soft text-[var(--warning)]";
    return "border-[var(--success)]/50 bg-success-soft text-[var(--success)]";
  }, [error, health]);

  const statusText = useMemo(() => {
    if (loading) return "Workeri: učitavanje";
    if (error) return "Workeri: nedostupno";
    if (!health) return "Workeri: nema podataka";
    if (!health.workersEnabled) return "Workeri: isključeni";
    return `Workeri: ${health.runningWorkers}/${health.totalWorkers}`;
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
      setError(e instanceof Error ? e.message : "Neuspešna promena worker statusa.");
    } finally {
      setBusy(false);
    }
  }, [busy, health, load]);

  const buttonLabel = health?.workersEnabled ? "Stop" : "Start";
  const toggleDisabled = busy || loading || !!error || !health || (!health.runtimeToggleAllowed && !health.workersEnabled);
  const toggleTitle = !health
    ? "Worker control"
    : !health.runtimeToggleAllowed && !health.workersEnabled
      ? "U ovoj okolini uključivanje workera je zaključano."
      : "Promeni worker status";

  return (
    <div className="inline-flex shrink-0 items-center gap-1 rounded-2xl border border-muted bg-[var(--surface-light)] px-1.5 py-1">
      <span
        className={`inline-flex items-center gap-1.5 rounded-xl border px-2 py-1 text-[11px] font-bold tracking-wide ${toneClass}`}
        title={health ? `Env: ${health.environment ?? "n/a"} | Last switch: ${health.lastSwitchAtUtc ?? "n/a"}` : "Worker status"}
      >
        <Bot size={12} />
        {statusText}
      </span>
      <button
        type="button"
        onClick={() => void onToggle()}
        disabled={toggleDisabled}
        className="rounded-xl border border-muted bg-[var(--surface-elevated)] px-2 py-1 text-[11px] font-semibold text-contrast transition hover:border-[var(--info)] hover:bg-[var(--surface-darker)] disabled:cursor-not-allowed disabled:opacity-60"
        title={toggleTitle}
      >
        {busy ? "..." : (!health?.runtimeToggleAllowed && !health?.workersEnabled ? "Locked" : buttonLabel)}
      </button>
      <button
        type="button"
        onClick={() => void load(true)}
        disabled={busy}
        className="rounded-xl border border-muted bg-[var(--surface-elevated)] px-2 py-1 text-[11px] font-semibold text-secondary transition hover:border-[var(--info)] hover:text-contrast disabled:cursor-not-allowed disabled:opacity-60"
        title="Osveži worker status"
      >
        <RefreshCw size={12} className={busy ? "animate-spin" : ""} />
      </button>
    </div>
  );
}
