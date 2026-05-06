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
  Power,
  RefreshCw,
  Settings,
  Sun,
  Terminal,
  Zap,
} from "lucide-react";
import { useTheme } from "../context/ThemeContext";
import { useToast } from "../components/Toast";
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

interface WorkerControl {
  isEnabled: boolean;
  workersEnabled: string;
  workersEnabledSource: string;
}

interface HealthCheck {
  timestamp: string;
  workerGlobalEnabled: boolean;
  databaseConnected: boolean;
  databaseMessage: string;
}

type Panel = "workers" | "import" | "health" | "toggles" | "diagnostics" | "themes" | "logs";

export default function ConfigurationPage() {
  const [activePanel, setActivePanel] = useState<Panel>("workers");
  const [batches, setBatches] = useState<PendingBatch[]>([]);
  const [workerControl, setWorkerControl] = useState<WorkerControl | null>(null);
  const [health, setHealth] = useState<HealthCheck | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { currentTheme, setTheme } = useTheme();
  const { showToast } = useToast();

  const apiBase = import.meta.env.VITE_API_BASE || "http://localhost:8080";

  const loadWorkerControl = useCallback(async () => {
    try {
      const res = await fetch(`${apiBase}/api/workers/control`);
      if (res.ok) {
        const data = await res.json();
        setWorkerControl(data);
      }
    } catch (e) {
      console.error("Failed to load worker control:", e);
    }
  }, [apiBase]);

  const loadPendingBatches = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${apiBase}/api/admin/pending-batches?take=50`);
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
  }, [apiBase]);

  const loadHealth = useCallback(async () => {
    try {
      const res = await fetch(`${apiBase}/api/admin/health-check`);
      if (res.ok) {
        const data = await res.json();
        setHealth(data);
      }
    } catch (e) {
      console.error("Failed to load health check:", e);
    }
  }, [apiBase]);

  const toggleWorkers = async (enable: boolean) => {
    try {
      const url = `${apiBase}/api/workers/control/${enable ? "enable" : "disable"}`;
      const res = await fetch(url, { method: "POST" });
      if (res.ok) {
        showToast(`Workers ${enable ? "enabled" : "disabled"}`, "success");
        await loadWorkerControl();
      } else {
        showToast("Failed to toggle workers", "error");
      }
    } catch (e) {
      showToast("Error toggling workers", "error");
      console.error(e);
    }
  };

  const requeueBatch = async (batchId: number) => {
    if (!window.confirm(`Requeue batch ${batchId}?`)) return;

    try {
      const res = await fetch(`${apiBase}/api/admin/requeue-batch/${batchId}`, { method: "POST" });
      const result = (await res.json()) as any;
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
      const res = await fetch(`${apiBase}/api/admin/run-stale-recovery`, { method: "POST" });
      const result = (await res.json()) as any;
      showToast(result.message, result.success ? "success" : "error");
      if (result.success) {
        await loadPendingBatches();
      }
    } catch (e) {
      showToast("Error running stale recovery", "error");
      console.error(e);
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

  useEffect(() => {
    loadWorkerControl();
    loadHealth();
  }, [loadWorkerControl, loadHealth]);

  useEffect(() => {
    if (activePanel === "import") {
      loadPendingBatches();
    }
  }, [activePanel, loadPendingBatches]);

  return (
    <div className="configuration-page">
      <div className="config-header">
        <h1 className="config-title">Konfiguracija i nadzor</h1>
        <p className="config-subtitle">Upravljanje radnicima, import zadacima, zdravljem sistema i temama</p>
      </div>

      <div className="config-layout">
        {/* Left panel: navigation */}
        <div className="config-sidebar">
          <div className="config-menu">
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

        {/* Center panel: content */}
        <div className="config-content">
          {/* Workers Panel */}
          {activePanel === "workers" && (
            <div className="config-panel">
              <h2 className="panel-title">Radnici</h2>
              {workerControl && (
                <div className="panel-card">
                  <div className="card-header">
                    <Activity size={20} />
                    <span className="card-title">Globalno stanje radnika</span>
                    <span className={`status-badge ${workerControl.isEnabled ? "enabled" : "disabled"}`}>
                      {workerControl.isEnabled ? "Uključeni" : "Isključeni"}
                    </span>
                  </div>
                  <div className="card-content">
                    <p>
                      <strong>Izvor:</strong> {workerControl.workersEnabledSource}
                    </p>
                    <div className="action-group">
                      <button
                        className="btn btn-primary"
                        onClick={() => toggleWorkers(true)}
                        disabled={workerControl.isEnabled}
                      >
                        <Power size={16} /> Uključi radnike
                      </button>
                      <button
                        className="btn btn-secondary"
                        onClick={() => toggleWorkers(false)}
                        disabled={!workerControl.isEnabled}
                      >
                        <Power size={16} /> Isključi radnike
                      </button>
                      <button className="btn btn-ghost" onClick={loadWorkerControl}>
                        <RefreshCw size={16} /> Osveži
                      </button>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Import Panel */}
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
                                <button className="btn-small" onClick={() => requeueBatch(batch.id)} title="Requeue">
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

          {/* Health Panel */}
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
            </div>
          )}

          {/* Toggles Panel */}
          {activePanel === "toggles" && (
            <div className="config-panel">
              <h2 className="panel-title">Runtime opcije</h2>
              <div className="panel-card">
                <div className="card-content">
                  <p className="text-muted">
                    Većina runtime opcija se kontroliše kroz /api/workers/control endpoint. Sve vrednosti koje se ovde prikazuju su informativne i zahtevaju
                    restart za promenu.
                  </p>
                  <ul className="info-list">
                    <li>WorkerEnabled — Uključivanje/isključivanje background worker-a</li>
                    <li>AccessImportOptions.PollingIntervalSeconds — Kako često worker proverava pending batches</li>
                    <li>MaxConcurrentJobs — Broj simultanih import zadataka</li>
                    <li>EnableAutoRetryForTransientFailures — Automatski retry na privremene greške</li>
                    <li>PreventConcurrentRuns — Zabrana simultanih import sesija</li>
                  </ul>
                </div>
              </div>
            </div>
          )}

          {/* Diagnostics Panel */}
          {activePanel === "diagnostics" && (
            <div className="config-panel">
              <h2 className="panel-title">Dijagnostika i održavanje</h2>
              <div className="panel-card">
                <div className="card-content">
                  <div className="action-group-vertical">
                    <button className="btn btn-warning" onClick={runStaleRecovery}>
                      <Zap size={16} /> Pokreni stale batch recovery
                    </button>
                    <p className="text-small text-muted">
                      Pronađi abandoned import sesije (long-running bez heartbeat) i označi ih kao failed
                    </p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Themes Panel */}
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

          {/* Logs Panel */}
          {activePanel === "logs" && (
            <div className="config-panel">
              <h2 className="panel-title">Audit logovi</h2>
              <div className="panel-card">
                <div className="card-content">
                  <p className="text-muted">Audit logovi su dostupni kroz /api/admin/audit-log endpoint</p>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Right side: quick metrics */}
        <div className="config-metrics">
          <div className="metric-card">
            <Clock size={20} />
            <span>Poslednja osvežavanja: {new Date().toLocaleTimeString()}</span>
          </div>
        </div>
      </div>
    </div>
  );
}
