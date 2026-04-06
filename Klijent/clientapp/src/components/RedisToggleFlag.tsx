import { useCallback, useEffect, useState } from "react";
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
      const res = await fetchWithTimeout(apiUrl("/api/redis/status"), undefined, 10_000);
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
      setError(e instanceof Error ? e.message : "Greska");
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
      const res = await fetchWithTimeout(apiUrl("/api/redis/toggle"), { method: "POST" }, 10_000);
      if (res.status === 404) {
        setEndpointMissing(true);
        return;
      }
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = (await res.json()) as RedisStatus;
      setStatus(data);
      setEndpointMissing(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Greska pri toglovanju Redis-a.");
    } finally {
      setBusy(false);
    }
  }, [busy]);

  const tone = error
    ? { bg: "var(--error, var(--theme-color-7f1d1d, #7f1d1d))", border: "var(--error, var(--theme-color-f87171, var(--theme-color-f87171, #f87171)))", text: "var(--text-on-error, var(--theme-color-fee2e2, var(--theme-color-fee2e2, #fee2e2)))" }
    : !status
    ? { bg: "var(--surface-elevated, var(--theme-color-1f2937, var(--theme-color-1f2937, #1f2937)))", border: "var(--border-default, var(--theme-color-6b7280, var(--theme-color-6b7280, #6b7280)))", text: "var(--text-muted, var(--theme-color-e5e7eb, #e5e7eb))" }
    : status.enabled && status.available
    ? { bg: "var(--success, var(--theme-color-064e3b, #064e3b))", border: "var(--success, var(--theme-color-34d399, var(--theme-color-34d399, #34d399)))", text: "var(--text-on-success, var(--theme-color-d1fae5, #d1fae5))" }
    : status.enabled && !status.available
    ? { bg: "var(--warning, var(--theme-color-78350f, var(--theme-color-78350f, #78350f)))", border: "var(--warning, var(--theme-color-fbbf24, var(--theme-color-fbbf24, #fbbf24)))", text: "var(--text-muted, var(--theme-color-fef3c7, var(--theme-color-fef3c7, #fef3c7)))" }
    : { bg: "var(--surface-elevated, var(--theme-color-1f2937, var(--theme-color-1f2937, #1f2937)))", border: "var(--border-hover, var(--theme-color-9ca3af, var(--theme-color-9ca3af, #9ca3af)))", text: "var(--text-muted, var(--theme-color-e5e7eb, var(--theme-color-e5e7eb, #e5e7eb)))" };

  const label = error
    ? "Redis: greska"
    : endpointMissing
    ? "Redis: endpoint nije aktivan"
    : loading && !status
    ? "Redis: ucitavanje..."
    : !status
    ? "Redis: status nedostupan"
    : status.enabled
    ? status.available
      ? "Redis: ukljucen"
      : "Redis: ukljucen (nedostupan)"
    : "Redis: iskljucen";

  const buttonLabel = status?.enabled ? "Stop Redis" : "Start Redis";
  const title = status
    ? `Enabled: ${status.enabled} | Available: ${status.available}`
    : "Redis cache status";

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
      <span
        style={{
          padding: "4px 8px",
          borderRadius: 999,
          border: `1px solid ${tone.border}`,
          background: tone.bg,
          color: tone.text,
          fontSize: 12,
          fontWeight: 700,
          whiteSpace: "nowrap",
        }}
        title={title}
      >
        {label}
      </span>
      <button
        type="button"
        onClick={() => void onToggle()}
        disabled={busy || !status || endpointMissing}
        style={{
          padding: "5px 10px",
          borderRadius: 8,
          border: `1px solid var(--border-default, var(--theme-color-4b5563, var(--theme-color-4b5563, #4b5563)))`,
          background: busy ? "var(--surface-elevated, var(--theme-color-374151, var(--theme-color-374151, #374151)))" : "var(--surface-default, var(--theme-color-1f2937, var(--theme-color-1f2937, #1f2937)))",
          color: "var(--text-primary, var(--theme-color-ffffff, var(--theme-color-ffffff, #ffffff)))",
          fontSize: 12,
          fontWeight: 600,
          cursor: busy || !status || endpointMissing ? "not-allowed" : "pointer",
          opacity: busy || !status || endpointMissing ? 0.6 : 1,
        }}
      >
        {busy ? "..." : buttonLabel}
      </button>
    </div>
  );
}
