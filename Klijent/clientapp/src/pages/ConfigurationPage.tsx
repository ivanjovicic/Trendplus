import { useEffect, useState, useCallback } from "react";
import {
  Activity,
  AlertCircle,
  CheckCircle,
  Clock,
  Cpu,
  Database,
  Gauge,
  HardDrive,
  LogOut,
  Moon,
  RefreshCw,
  Server,
  Settings,
  Sun,
  Terminal,
  Zap,
} from "lucide-react";
import { useTheme } from "../context/ThemeContext";
import { useToast } from "../components/Toast";
import { usePingControl } from "../context/PingControlContext";
import { WorkersPanel } from "../components/WorkersPanel";
import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS } from "../utils/apiTimeouts";
import {
  getBackendRoutingPreference as getBackendRoutingPreferenceApi,
  updateBackendRoutingPreference,
  pingBackendProvider,
  type BackendProvider,
  type BackendProviderHealth,
  type BackendRoutingPreference,
} from "../services/backendRoutingApi";
import {
  getBackendRoutingPreference as getLocalBackendRoutingPreference,
  saveBackendRoutingPreference as saveLocalBackendRoutingPreference,
} from "../utils/apiFailover";
import "./ConfigurationPage.css";

interface PendingBatch {
  id: number;
  sourceFileName: string;
  status: string;
  queuedAtUtc: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  lastHeartbeatUtc: string | null;
  currentStep: string | null;
  currentTable: string | null;
  elapsedSeconds: number;
  rowsRead: number;
  rowsWritten: number;
  progressPercent: number;
  errorMessage: string | null;
  hasSourceFile: boolean;
  hasStorageKey: boolean;
  cancellationRequested: boolean;
  retryCount: number;
}

interface PendingBatchesData {
  total: number;
  batches: PendingBatch[];
}

interface HealthCheck {
  timestamp: string;
  workerGlobalEnabled: boolean;
  databaseConnected: boolean;
  databaseMessage: string;
}

interface RedisStatus {
  enabled: boolean;
  available: boolean;
}

type Panel =
  | "backend"
  | "workers"
  | "import"
  | "health"
  | "cache"
  | "toggles"
  | "diagnostics"
  | "themes"
  | "logs";

export default function ConfigurationPage() {
  const [activePanel, setActivePanel] = useState<Panel>("backend");
  const [batches, setBatches] = useState<PendingBatch[]>([]);
  const [health, setHealth] = useState<HealthCheck | null>(null);
  const [redisStatus, setRedisStatus] = useState<RedisStatus | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [backendPreference, setBackendPreference] = useState<BackendRoutingPreference | null>(null);
  const [providerHealth, setProviderHealth] = useState<Partial<Record<BackendProvider, BackendProviderHealth>>>({});
  const [savingBackendPreference, setSavingBackendPreference] = useState(false);
  const [refreshHintVisible, setRefreshHintVisible] = useState(false);
  const { currentTheme, setTheme } = useTheme();
  const { apiPingEnabled, setApiPingEnabled } = usePingControl();
  const { showToast } = useToast();

  const loadPendingBatches = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetchWithTimeout(apiUrl("/api/admin/pending-batches?take=50"), undefined, API_COLD_START_TIMEOUT_MS);
      if (res.ok) {
        const data = (await res.json()) as PendingBatchesData;
        setBatches(data.batches || []);
      } else {
        setError("Failed to load pending batches");
      }
    } catch (e) {
      setError("Error loading pending batches");
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadHealth = useCallback(async () => {
    try {
      const res = await fetchWithTimeout(apiUrl("/api/admin/health-check"), undefined, API_COLD_START_TIMEOUT_MS);
      if (res.ok) {
        const data = await res.json();
        setHealth(data);
      }
    } catch (e) {
      console.error("Failed to load health check:", e);
    }
  }, []);

  const loadRedisStatus = useCallback(async () => {
    try {
      const res = await fetchWithTimeout(apiUrl("/api/redis/status"), undefined, API_COLD_START_TIMEOUT_MS);
      if (res.ok) {
        const data = (await res.json()) as RedisStatus;
        setRedisStatus(data);
      }
    } catch (e) {
      console.error("Failed to load redis status:", e);
      setRedisStatus(null);
    }
  }, []);

  const toggleRedis = async () => {
    try {
      const res = await fetchWithTimeout(apiUrl("/api/redis/toggle"), { method: "POST" }, API_COLD_START_TIMEOUT_MS);
      if (!res.ok) {
        showToast("Redis toggle nije uspeo", "error");
        return;
      }
      const data = (await res.json()) as RedisStatus;
      setRedisStatus(data);
      showToast(`Redis ${data.enabled ? "ukljucen" : "iskljucen"}`, "success");
    } catch (e) {
      showToast("Greska pri promeni Redis stanja", "error");
      console.error(e);
    }
  };

  const loadBackendPreference = useCallback(async () => {
    try {
      const serverPreference = await getBackendRoutingPreferenceApi();
      setBackendPreference(serverPreference);
      saveLocalBackendRoutingPreference({
        primaryProvider: serverPreference.primaryProvider,
        fallbackEnabled: serverPreference.fallbackEnabled,
        fallbackProvider: serverPreference.fallbackProvider,
      });
    } catch (e) {
      const local = getLocalBackendRoutingPreference();
      setBackendPreference({
        primaryProvider: local.primaryProvider,
        fallbackEnabled: local.fallbackEnabled,
        fallbackProvider: local.fallbackProvider,
      });
      console.error("Failed to load backend preference from API, using local preference.", e);
    }
  }, []);

  const runProviderPing = useCallback(async (provider: BackendProvider) => {
    try {
      const result = await pingBackendProvider(provider);
      setProviderHealth((prev) => ({ ...prev, [provider]: result }));
      return result;
    } catch (e) {
      const failResult: BackendProviderHealth = {
        provider,
        success: false,
        statusCode: null,
        latencyMs: 0,
        checkedAtUtc: new Date().toISOString(),
        message: e instanceof Error ? e.message : "Ping failed",
      };
      setProviderHealth((prev) => ({ ...prev, [provider]: failResult }));
      return failResult;
    }
  }, []);

  const pingConfiguredProviders = useCallback(async () => {
    if (!backendPreference) return;
    await runProviderPing(backendPreference.primaryProvider);
    if (backendPreference.fallbackEnabled) {
      await runProviderPing(backendPreference.fallbackProvider);
    }
  }, [backendPreference, runProviderPing]);

  const requeueBatch = async (batchId: number) => {
    if (!window.confirm(`Requeue batch ${batchId}?`)) return;

    try {
      const res = await fetchWithTimeout(apiUrl(`/api/admin/requeue-batch/${batchId}`), { method: "POST" }, API_COLD_START_TIMEOUT_MS);
      const result = (await res.json()) as { success: boolean; message: string };
      if (result.success) {
        showToast("Batch requeued", "success");
        await loadPendingBatches();
      } else {
        showToast(`Requeue failed: ${result.message}`, "error");
      }
    } catch (e) {
      showToast("Error requeuing batch", "error");
      console.error(e);
    }
  };

  const runStaleRecovery = async () => {
    if (!window.confirm("Run stale batch recovery? This may mark old pending batches as failed.")) return;

    try {
      const res = await fetchWithTimeout(apiUrl("/api/admin/run-stale-recovery"), { method: "POST" }, API_COLD_START_TIMEOUT_MS);
      const result = (await res.json()) as { success: boolean; message: string };
      showToast(result.message, result.success ? "success" : "error");
      if (result.success) {
        await loadPendingBatches();
      }
    } catch (e) {
      showToast("Error running stale recovery", "error");
      console.error(e);
    }
  };

  const updateBackendPreferenceField = <K extends keyof BackendRoutingPreference>(
    key: K,
    value: BackendRoutingPreference[K]
  ) => {
    setBackendPreference((prev) => {
      const base: BackendRoutingPreference = prev ?? {
        primaryProvider: "render",
        fallbackEnabled: true,
        fallbackProvider: "fly",
      };
      const next = { ...base, [key]: value };
      if (next.fallbackEnabled && next.fallbackProvider === next.primaryProvider) {
        next.fallbackProvider = next.primaryProvider === "render" ? "fly" : "render";
      }
      return next;
    });
  };

  const saveBackendPreference = async () => {
    if (!backendPreference) return;
    if (
      backendPreference.fallbackEnabled &&
      backendPreference.fallbackProvider === backendPreference.primaryProvider
    ) {
      showToast("Fallback provider mora da se razlikuje od primary providera.", "error");
      return;
    }

    setSavingBackendPreference(true);
    try {
      const saved = await updateBackendRoutingPreference(backendPreference);
      setBackendPreference(saved);
      saveLocalBackendRoutingPreference({
        primaryProvider: saved.primaryProvider,
        fallbackEnabled: saved.fallbackEnabled,
        fallbackProvider: saved.fallbackProvider,
      });
      setRefreshHintVisible(true);
      showToast("Backend konfiguracija sacuvana.", "success");
      await pingConfiguredProviders();
    } catch (e) {
      showToast(e instanceof Error ? e.message : "Greska pri cuvanju backend konfiguracije", "error");
    } finally {
      setSavingBackendPreference(false);
    }
  };

  const formatDate = (d: string | null | undefined) => {
    if (!d) return "-";
    try {
      const date = new Date(d);
      return date.toLocaleString("sr-RS", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
      });
    } catch {
      return d;
    }
  };

  const providerLabel = (provider: BackendProvider) =>
    provider === "render" ? "Render" : "Fly.io";

  useEffect(() => {
    loadHealth();
    loadRedisStatus();
    loadBackendPreference();
  }, [loadHealth, loadRedisStatus, loadBackendPreference]);

  useEffect(() => {
    if (activePanel === "import") {
      loadPendingBatches();
    }
  }, [activePanel, loadPendingBatches]);

  useEffect(() => {
    if (activePanel === "backend" && backendPreference) {
      void pingConfiguredProviders();
    }
  }, [activePanel, backendPreference, pingConfiguredProviders]);

  return (
    <div className="configuration-page">
      <div className="config-header">
        <h1 className="config-title">Konfiguracija i nadzor</h1>
        <p className="config-subtitle">
          Jedinstveno mesto za backend rutu, fallback, radnike, import, API zdravlje, cache i teme.
        </p>
      </div>

      <div className="config-layout">
        <div className="config-sidebar">
          <div className="config-menu">
            <button
              className={`config-menu-item ${activePanel === "backend" ? "active" : ""}`}
              onClick={() => setActivePanel("backend")}
            >
              <Server size={18} />
              <span>Backend</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "workers" ? "active" : ""}`}
              onClick={() => setActivePanel("workers")}
            >
              <Cpu size={18} />
              <span>Radnici</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "import" ? "active" : ""}`}
              onClick={() => setActivePanel("import")}
            >
              <HardDrive size={18} />
              <span>Import</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "health" ? "active" : ""}`}
              onClick={() => setActivePanel("health")}
            >
              <Gauge size={18} />
              <span>Zdravlje API-ja</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "cache" ? "active" : ""}`}
              onClick={() => setActivePanel("cache")}
            >
              <Database size={18} />
              <span>Cache / Redis</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "toggles" ? "active" : ""}`}
              onClick={() => setActivePanel("toggles")}
            >
              <Zap size={18} />
              <span>Runtime opcije</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "diagnostics" ? "active" : ""}`}
              onClick={() => setActivePanel("diagnostics")}
            >
              <Terminal size={18} />
              <span>Dijagnostika</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "themes" ? "active" : ""}`}
              onClick={() => setActivePanel("themes")}
            >
              <Sun size={18} />
              <span>Teme</span>
            </button>
            <button
              className={`config-menu-item ${activePanel === "logs" ? "active" : ""}`}
              onClick={() => setActivePanel("logs")}
            >
              <LogOut size={18} />
              <span>Audit logovi</span>
            </button>
          </div>
        </div>

        <div className="config-content">
          {activePanel === "backend" && (
            <div className="config-panel">
              <h2 className="panel-title">Backend provider i failover</h2>
              <div className="panel-card">
                <div className="card-header">
                  <Server size={20} />
                  <span className="card-title">Aktivna backend strategija</span>
                </div>
                {backendPreference ? (
                  <div className="card-content">
                    <div className="health-item">
                      <span className="health-label">Primary provider</span>
                      <select
                        value={backendPreference.primaryProvider}
                        onChange={(e) =>
                          updateBackendPreferenceField(
                            "primaryProvider",
                            e.target.value as BackendProvider
                          )
                        }
                        className="dark-select control-muted rounded-lg px-2.5 py-2 text-sm"
                      >
                        <option value="render">Render</option>
                        <option value="fly">Fly.io</option>
                      </select>
                    </div>

                    <div className="health-item">
                      <span className="health-label">Fallback backend</span>
                      <label className="inline-flex items-center gap-2 text-sm">
                        <input
                          type="checkbox"
                          checked={backendPreference.fallbackEnabled}
                          onChange={(e) =>
                            updateBackendPreferenceField("fallbackEnabled", e.target.checked)
                          }
                        />
                        Uključen fallback
                      </label>
                    </div>

                    {backendPreference.fallbackEnabled && (
                      <div className="health-item">
                        <span className="health-label">Fallback provider</span>
                        <select
                          value={backendPreference.fallbackProvider}
                          onChange={(e) =>
                            updateBackendPreferenceField(
                              "fallbackProvider",
                              e.target.value as BackendProvider
                            )
                          }
                          className="dark-select control-muted rounded-lg px-2.5 py-2 text-sm"
                        >
                          <option
                            value="render"
                            disabled={backendPreference.primaryProvider === "render"}
                          >
                            Render
                          </option>
                          <option
                            value="fly"
                            disabled={backendPreference.primaryProvider === "fly"}
                          >
                            Fly.io
                          </option>
                        </select>
                      </div>
                    )}

                    <p className="text-small text-muted">
                      Fallback je aktivan samo kada primary backend ne odgovori na API zahtev ili health/ping proveru.
                    </p>

                    <div className="action-group">
                      <button
                        className="btn btn-primary"
                        onClick={() => void saveBackendPreference()}
                        disabled={savingBackendPreference}
                      >
                        <Settings size={16} />
                        {savingBackendPreference ? "Čuvanje..." : "Sačuvaj konfiguraciju"}
                      </button>
                      <button
                        className="btn btn-ghost"
                        onClick={() => void pingConfiguredProviders()}
                      >
                        <RefreshCw size={16} /> Proveri API ping
                      </button>
                    </div>

                    {refreshHintVisible && (
                      <div className="alert alert-error" style={{ borderColor: "var(--color-border)" }}>
                        <AlertCircle size={18} />
                        Promena provider redosleda važi nakon osvežavanja stranice.
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="loading">Učitavanje backend konfiguracije...</div>
                )}
              </div>

              <div className="panel-card">
                <div className="card-header">
                  <Activity size={20} />
                  <span className="card-title">Provider ping status</span>
                </div>
                <div className="card-content">
                  {(["render", "fly"] as BackendProvider[]).map((provider) => {
                    const ping = providerHealth[provider];
                    return (
                      <div className="health-item" key={provider}>
                        <div>
                          <div className="health-label">{providerLabel(provider)}</div>
                          <div className="text-small text-muted">
                            {ping
                              ? `Poslednja provera: ${formatDate(ping.checkedAtUtc)}`
                              : "Još nije provereno"}
                          </div>
                        </div>
                        <div className="flex items-center gap-2">
                          {ping ? (
                            <span className={`health-status ${ping.success ? "ok" : "error"}`}>
                              {ping.success ? (
                                <>
                                  <CheckCircle size={16} /> OK ({ping.latencyMs}ms)
                                </>
                              ) : (
                                <>
                                  <AlertCircle size={16} /> {ping.message}
                                </>
                              )}
                            </span>
                          ) : (
                            <span className="health-status off">N/A</span>
                          )}
                          <button
                            className="btn-small"
                            onClick={() => void runProviderPing(provider)}
                            title="Ponovo proveri"
                          >
                            <RefreshCw size={14} />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
          )}

          {activePanel === "workers" && (
            <div className="config-panel">
              <h2 className="panel-title">Radnici</h2>
              <div className="panel-card">
                <WorkersPanel refreshInterval={5000} />
              </div>
            </div>
          )}

          {activePanel === "import" && (
            <div className="config-panel">
              <h2 className="panel-title">Import iz Accessa</h2>
              {error && (
                <div className="alert alert-error">
                  <AlertCircle size={18} />
                  {error}
                </div>
              )}
              {loading ? (
                <div className="loading">Učitavanje...</div>
              ) : (
                <div className="panel-card">
                  <div className="card-header">
                    <HardDrive size={20} />
                    <span className="card-title">Pending/Failed batches ({batches.length})</span>
                  </div>
                  <div className="table-wrapper">
                    <table className="config-table">
                      <thead>
                        <tr>
                          <th>ID</th>
                          <th>Fajl</th>
                          <th>Status</th>
                          <th>Queued</th>
                          <th>Elapsed</th>
                          <th>Progress</th>
                          <th>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {batches.length === 0 ? (
                          <tr>
                            <td colSpan={7} className="text-center text-muted">
                              Nema pending batch-eva
                            </td>
                          </tr>
                        ) : (
                          batches.map((batch) => (
                            <tr key={batch.id}>
                              <td className="mono">{batch.id}</td>
                              <td>{batch.sourceFileName}</td>
                              <td>
                                <span className={`status-badge ${batch.status}`}>{batch.status}</span>
                              </td>
                              <td className="text-small">{formatDate(batch.queuedAtUtc)}</td>
                              <td className="text-small">{batch.elapsedSeconds}s</td>
                              <td>
                                <div className="progress-bar">
                                  <div style={{ width: `${batch.progressPercent}%` }} className="progress-fill" />
                                </div>
                              </td>
                              <td className="actions">
                                <button className="btn-small" onClick={() => void requeueBatch(batch.id)} title="Requeue">
                                  <RefreshCw size={14} />
                                </button>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}

          {activePanel === "health" && (
            <div className="config-panel">
              <h2 className="panel-title">Zdravlje API-ja</h2>
              {health && (
                <div className="panel-card">
                  <div className="card-header">
                    <Gauge size={20} />
                    <span className="card-title">Status</span>
                  </div>
                  <div className="card-content">
                    <div className="health-item">
                      <span className="health-label">Baza podataka:</span>
                      <span className={`health-status ${health.databaseConnected ? "ok" : "error"}`}>
                        {health.databaseConnected ? (
                          <>
                            <CheckCircle size={16} /> OK
                          </>
                        ) : (
                          <>
                            <AlertCircle size={16} /> Error
                          </>
                        )}
                      </span>
                    </div>
                    <div className="health-item">
                      <span className="health-label">Radnici uključeni:</span>
                      <span className={`health-status ${health.workerGlobalEnabled ? "ok" : "off"}`}>
                        {health.workerGlobalEnabled ? "DA" : "NE"}
                      </span>
                    </div>
                  </div>
                </div>
              )}

              <div className="panel-card">
                <div className="card-header">
                  <Activity size={20} />
                  <span className="card-title">Frontend API ping kontrola</span>
                </div>
                <div className="card-content">
                  <p className="text-muted">
                    Periodično pingovanje backend-a iz frontenda možete uključiti ili isključiti ovde.
                  </p>
                  <div className="action-group">
                    <button
                      className="btn btn-primary"
                      onClick={() => setApiPingEnabled(true)}
                      disabled={apiPingEnabled}
                    >
                      Uključi API ping
                    </button>
                    <button
                      className="btn btn-secondary"
                      onClick={() => setApiPingEnabled(false)}
                      disabled={!apiPingEnabled}
                    >
                      Isključi API ping
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activePanel === "cache" && (
            <div className="config-panel">
              <h2 className="panel-title">Cache / Redis</h2>
              <div className="panel-card">
                <div className="card-header">
                  <Database size={20} />
                  <span className="card-title">Redis status</span>
                </div>
                <div className="card-content">
                  <div className="health-item">
                    <span className="health-label">Redis state</span>
                    <span className={`health-status ${redisStatus?.enabled ? "ok" : "off"}`}>
                      {redisStatus
                        ? redisStatus.enabled
                          ? redisStatus.available
                            ? "Uključen"
                            : "Uključen (nedostupan)"
                          : "Isključen"
                        : "N/A"}
                    </span>
                  </div>
                  <div className="action-group">
                    <button className="btn btn-primary" onClick={() => void toggleRedis()}>
                      Promeni Redis stanje
                    </button>
                    <button className="btn btn-ghost" onClick={() => void loadRedisStatus()}>
                      <RefreshCw size={16} /> Osveži
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activePanel === "toggles" && (
            <div className="config-panel">
              <h2 className="panel-title">Runtime opcije</h2>
              <div className="panel-card">
                <div className="card-content">
                  <p className="text-muted">
                    Većina runtime opcija se kontroliše kroz /api/workers/control endpoint. Vrednosti su
                    informativne, a pojedine izmene zahtevaju restart.
                  </p>
                  <ul className="info-list">
                    <li>WorkerEnabled — uključivanje/isključivanje background worker-a</li>
                    <li>AccessImportOptions.PollingIntervalSeconds — učestalost provere pending batch-eva</li>
                    <li>MaxConcurrentJobs — broj simultanih import zadataka</li>
                    <li>EnableAutoRetryForTransientFailures — automatski retry na privremene greške</li>
                    <li>PreventConcurrentRuns — zabrana simultanih import sesija</li>
                  </ul>
                </div>
              </div>
            </div>
          )}

          {activePanel === "diagnostics" && (
            <div className="config-panel">
              <h2 className="panel-title">Dijagnostika i održavanje</h2>
              <div className="panel-card">
                <div className="card-content">
                  <div className="action-group-vertical">
                    <button className="btn btn-warning" onClick={() => void runStaleRecovery()}>
                      <Zap size={16} /> Pokreni stale batch recovery
                    </button>
                    <p className="text-small text-muted">
                      Pronađi abandoned import sesije (dugo bez heartbeat-a) i označi ih kao failed.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activePanel === "themes" && (
            <div className="config-panel">
              <h2 className="panel-title">Teme</h2>
              <div className="panel-card">
                <div className="card-content">
                  <div className="themes-grid">
                    {(["neon-light", "neon-dark", "soft-gray"] as const).map((t) => (
                      <button
                        key={t}
                        className={`theme-btn ${currentTheme === t ? "selected" : ""}`}
                        onClick={() => {
                          setTheme(t);
                          localStorage.setItem("trendplus-theme", t);
                          showToast(`Tema promenjena na ${t}`, "success");
                        }}
                      >
                        {t === "neon-light" && <Sun size={24} />}
                        {t === "neon-dark" && <Moon size={24} />}
                        {t === "soft-gray" && <Settings size={24} />}
                        <span>{t === "neon-light" ? "Svetla" : t === "neon-dark" ? "Tamna" : "Soft Gray"}</span>
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          )}

          {activePanel === "logs" && (
            <div className="config-panel">
              <h2 className="panel-title">Audit logovi</h2>
              <div className="panel-card">
                <div className="card-content">
                  <p className="text-muted">Audit logovi su dostupni kroz /api/admin/audit-log endpoint.</p>
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="config-metrics">
          <div className="metric-card">
            <Clock size={20} />
            <span>Poslednje osvežavanje: {new Date().toLocaleTimeString()}</span>
          </div>
          {backendPreference && (
            <div className="metric-card">
              <Server size={20} />
              <span>
                Primary: {providerLabel(backendPreference.primaryProvider)}
                {backendPreference.fallbackEnabled
                  ? ` | Fallback: ${providerLabel(backendPreference.fallbackProvider)}`
                  : " | Fallback: isključen"}
              </span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
