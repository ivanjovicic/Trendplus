import { useCallback, useEffect, useState } from "react";
import { DatabaseZap } from "lucide-react";
import { usePingControl } from "../context/PingControlContext";
import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

const POLL_MS = import.meta.env.DEV ? 20000 : 60000;

interface RedisStatus {
  enabled: boolean;
  available: boolean;
}

export default function RedisToggleFlag() {
  const { apiPingEnabled } = usePingControl();
  const [status, setStatus] = useState<RedisStatus | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [endpointMissing, setEndpointMissing] = useState(false);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async (force = false) => {
    if (!apiPingEnabled && !force) return;
    try {
      setLoading(true);
      const res = await fetchWithTimeout(apiUrl("/api/redis/status"), undefined, 60_000);
      if (res.status === 404) {
        setEndpointMissing(true);
        setStatus(null);
        setError(null);
        return;
      }
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = (await res.json()) as RedisStatus;
      setStatus(data);
      setError(null);
      setEndpointMissing(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Greška");
    } finally {
      setLoading(false);
    }
  }, [apiPingEnabled]);

  useEffect(() => {
    void load(true);
  }, [load]);

  useEffect(() => {
    if (!apiPingEnabled) return;
    const id = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(id);
  }, [load, apiPingEnabled]);

  const onToggle = useCallback(async () => {
    if (busy) return;
    try {
      setBusy(true);
      setError(null);
      const res = await fetchWithTimeout(apiUrl("/api/redis/toggle"), { method: "POST" }, 60_000);
      if (res.status === 404) {
        setEndpointMissing(true);
        return;
      }
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = (await res.json()) as RedisStatus;
      setStatus(data);
      setEndpointMissing(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Greška pri togglovanju Redis-a.");
    } finally {
      setBusy(false);
    }
  }, [busy]);

  const toneClass = error
    ? "border-[var(--error)]/50 bg-error-soft text-[var(--error)]"
    : !status
    ? "border-muted bg-[var(--surface-darker)] text-muted"
    : status.enabled && status.available
    ? "border-[var(--success)]/50 bg-success-soft text-[var(--success)]"
    : status.enabled && !status.available
    ? "border-[var(--warning)]/50 bg-warning-soft text-[var(--warning)]"
    : "border-muted bg-[var(--surface-darker)] text-muted";

  const label = error
    ? "Redis: greška"
    : endpointMissing
    ? "Redis: nema endpoint"
    : loading && !status
    ? "Redis: učitavanje"
    : !status
    ? "Redis: nedostupno"
    : status.enabled
    ? status.available
      ? "Redis: uključen"
      : "Redis: nedostupan"
    : "Redis: isključen";

  const buttonLabel = status?.enabled ? "Stop" : "Start";
  const title = status
    ? `Enabled: ${status.enabled} | Available: ${status.available}`
    : "Redis cache status";

  return (
    <div className="inline-flex shrink-0 items-center gap-1 rounded-2xl border border-muted bg-[var(--surface-light)] px-1.5 py-1">
      <span
        className={`inline-flex items-center gap-1.5 rounded-xl border px-2 py-1 text-[11px] font-bold tracking-wide ${toneClass}`}
        title={title}
      >
        <DatabaseZap size={12} />
        {label}
      </span>
      <button
        type="button"
        onClick={() => void onToggle()}
        disabled={busy || !status || endpointMissing}
        className="rounded-xl border border-muted bg-[var(--surface-elevated)] px-2 py-1 text-[11px] font-semibold text-contrast transition hover:border-[var(--info)] hover:bg-[var(--surface-darker)] disabled:cursor-not-allowed disabled:opacity-60"
      >
        {busy ? "..." : buttonLabel}
      </button>
    </div>
  );
}
