import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { workerApi, type WorkerConfigurationItem } from "../services/workerApi";
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  CalendarCheck,
  CalendarOff,
} from "lucide-react";
import "./WorkersPanel.css";

interface WorkersPanelProps {
  refreshInterval?: number;
}

type WorkerAction = "start" | "stop" | "restart" | "enableSchedule" | "disableSchedule";

type ActionMessages = Record<string, { type: "success" | "error"; text: string }>;

export const WorkersPanel: React.FC<WorkersPanelProps> = ({ refreshInterval = 5000 }) => {
  const [workers, setWorkers] = useState<WorkerConfigurationItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [actionMessages, setActionMessages] = useState<ActionMessages>({});
  const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);
  const actionTimersRef = useRef<Record<string, ReturnType<typeof setTimeout>>>({});

  const setActionMessage = useCallback(
    (workerName: string, type: "success" | "error", text: string) => {
      setActionMessages((prev) => ({ ...prev, [workerName]: { type, text } }));
      if (actionTimersRef.current[workerName]) clearTimeout(actionTimersRef.current[workerName]);
      actionTimersRef.current[workerName] = setTimeout(
        () => {
          setActionMessages((prev) => {
            const next = { ...prev };
            delete next[workerName];
            return next;
          });
        },
        type === "success" ? 5000 : 8000,
      );
    },
    [],
  );

  const fetchWorkers = useCallback(async (isManual = false) => {
    try {
      if (isManual) {
        setLoading(true);
        setLoadError(null);
      }
      const response = await workerApi.getWorkersConfiguration();
      setWorkers(response.workers);
      setLastRefreshedAt(new Date());
    } catch (err) {
      if (isManual) setLoadError(err instanceof Error ? err.message : "Greška pri učitavanju radnika.");
    } finally {
      if (isManual) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchWorkers(true);
    const interval = window.setInterval(() => {
      void fetchWorkers(false);
    }, refreshInterval);
    return () => {
      window.clearInterval(interval);
      Object.values(actionTimersRef.current).forEach(clearTimeout);
    };
  }, [fetchWorkers, refreshInterval]);

  const runAction = useCallback(
    async (worker: WorkerConfigurationItem, action: WorkerAction) => {
      const key = `${worker.workerName}:${action}`;
      try {
        setActionInProgress(key);
        setActionMessages((prev) => {
          const next = { ...prev };
          delete next[worker.workerName];
          return next;
        });
        let responseMessage = "";
        switch (action) {
          case "start":
            responseMessage = (await workerApi.startWorker(worker.workerName)).message;
            break;
          case "stop":
            responseMessage = (await workerApi.stopWorker(worker.workerName)).message;
            break;
          case "restart":
            responseMessage = (await workerApi.restartWorker(worker.workerName)).message;
            break;
          case "enableSchedule":
            responseMessage = (await workerApi.enableSchedule(worker.workerName)).message;
            break;
          case "disableSchedule":
            responseMessage = (await workerApi.disableSchedule(worker.workerName)).message;
            break;
        }
        setActionMessage(worker.workerName, "success", responseMessage || "Uspešno.");
        await fetchWorkers(false);
      } catch (err) {
        setActionMessage(
          worker.workerName,
          "error",
          err instanceof Error ? err.message : "Akcija nije uspela.",
        );
      } finally {
        setActionInProgress(null);
      }
    },
    [fetchWorkers, setActionMessage],
  );

  const statusClass = (status: string) => {
    const n = status.toLowerCase();
    if (n.includes("error")) return "wp-badge wp-badge--error";
    if (n.includes("running") || n.includes("healthy")) return "wp-badge wp-badge--healthy";
    if (n.includes("stopped") || n.includes("disabled") || n.includes("paused"))
      return "wp-badge wp-badge--stopped";
    return "wp-badge wp-badge--muted";
  };

  const statusIcon = (status: string) => {
    const n = status.toLowerCase();
    if (n.includes("error")) return <AlertCircle className="wp-icon-xs" />;
    if (n.includes("running") || n.includes("healthy")) return <CheckCircle2 className="wp-icon-xs" />;
    return <Clock className="wp-icon-xs" />;
  };

  const formatDate = (raw?: string | null) => {
    if (!raw) return "-";
    const d = new Date(raw);
    if (Number.isNaN(d.getTime())) return raw;
    return d.toLocaleString("sr-RS");
  };

  const orderedWorkers = useMemo(
    () => [...workers].sort((a, b) => a.displayName.localeCompare(b.displayName)),
    [workers],
  );

  return (
    <div className="workers-panel">
      <div className="wp-toolbar">
        <div className="wp-toolbar-left">
          <span className="wp-count">
            {orderedWorkers.length > 0 ? `${orderedWorkers.length} radnik(a)` : ""}
          </span>
          {lastRefreshedAt && (
            <span className="wp-refreshed-at">
              Osveženo: {lastRefreshedAt.toLocaleTimeString("sr-RS")}
            </span>
          )}
        </div>
        <button
          type="button"
          onClick={() => void fetchWorkers(true)}
          disabled={loading}
          className="wp-btn wp-btn--primary"
        >
          <RefreshCw className={`wp-icon-sm ${loading ? "wp-spin" : ""}`} />
          Osveži
        </button>
      </div>

      {loadError && (
        <div className="wp-alert wp-alert--error">
          <AlertCircle className="wp-icon-sm" />
          {loadError}
        </div>
      )}

      {loading && orderedWorkers.length === 0 ? (
        <div className="wp-empty">Učitavanje radnika...</div>
      ) : orderedWorkers.length === 0 ? (
        <div className="wp-empty">Nema registrovanih radnika.</div>
      ) : (
        <div className="wp-table-scroll">
          <table className="wp-table">
            <thead>
              <tr>
                <th className="wp-th wp-col-name">Naziv</th>
                <th className="wp-th wp-col-status">Status</th>
                <th className="wp-th wp-col-schedule">Raspored</th>
                <th className="wp-th wp-col-heartbeat">Heartbeat</th>
                <th className="wp-th wp-col-lastrun">Poslednje pokretanje</th>
                <th className="wp-th wp-col-nextrun">Sledeće pokretanje</th>
                <th className="wp-th wp-col-success">Uspeh</th>
                <th className="wp-th wp-col-failure">Neuspeh</th>
                <th className="wp-th wp-col-error">Greška</th>
                <th className="wp-th wp-col-actions wp-col-sticky">Akcije</th>
              </tr>
            </thead>
            <tbody>
              {orderedWorkers.map((worker) => {
                const actionKey = (a: WorkerAction) => `${worker.workerName}:${a}`;
                const isRunning = (a: WorkerAction) => actionInProgress === actionKey(a);
                const anyRunning = (
                  ["start", "stop", "restart", "enableSchedule", "disableSchedule"] as WorkerAction[]
                ).some((a) => isRunning(a));
                const runtimeDisabled = !worker.isRuntimeControllable
                  ? (worker.runtimeControlReason ?? "Akcija nije podržana.")
                  : undefined;
                const scheduleDisabled = !worker.isScheduleControllable
                  ? (worker.scheduleControlReason ?? "Akcija nije podržana.")
                  : undefined;
                const msg = actionMessages[worker.workerName];
                return (
                  <tr key={worker.workerName} className="wp-row">
                    <td className="wp-td wp-col-name">
                      <div className="wp-worker-name">{worker.displayName}</div>
                      <div className="wp-worker-key">{worker.workerName}</div>
                      <div className="wp-worker-desc">{worker.description}</div>
                      {worker.isConfiguredButNotRunning && (
                        <div className="wp-worker-note wp-worker-note--warn">
                          Konfigurisano, ali ne radi u ovom procesu.
                        </div>
                      )}
                      {worker.isManuallyStopped && (
                        <div className="wp-worker-note wp-worker-note--stopped">Ručno zaustavljen.</div>
                      )}
                    </td>
                    <td className="wp-td wp-col-status">
                      <span className={statusClass(worker.status)}>
                        {statusIcon(worker.status)}
                        {worker.status}
                      </span>
                    </td>
                    <td className="wp-td wp-col-schedule">
                      {worker.scheduleEnabled ? (
                        <span className="wp-badge wp-badge--schedule-on">Omogućen</span>
                      ) : (
                        <span className="wp-badge wp-badge--schedule-off">Onemogućen</span>
                      )}
                    </td>
                    <td className="wp-td wp-col-heartbeat wp-td--date">{formatDate(worker.lastHeartbeat)}</td>
                    <td className="wp-td wp-col-lastrun wp-td--date">{formatDate(worker.lastRunAt)}</td>
                    <td className="wp-td wp-col-nextrun wp-td--date">{formatDate(worker.nextRunAt)}</td>
                    <td className="wp-td wp-col-success wp-td--date">{formatDate(worker.lastSuccessAt)}</td>
                    <td className="wp-td wp-col-failure wp-td--date">{formatDate(worker.lastFailureAt)}</td>
                    <td className="wp-td wp-col-error">
                      {worker.lastError ? (
                        <span className="wp-error-text" title={worker.lastError}>
                          {worker.lastError.length > 80
                            ? `${worker.lastError.slice(0, 80)}…`
                            : worker.lastError}
                        </span>
                      ) : (
                        <span className="wp-muted">-</span>
                      )}
                    </td>
                    <td className="wp-td wp-col-actions wp-col-sticky">
                      {msg && (
                        <div className={`wp-action-msg wp-action-msg--${msg.type}`}>
                          {msg.type === "success" ? (
                            <CheckCircle2 className="wp-icon-xs" />
                          ) : (
                            <AlertCircle className="wp-icon-xs" />
                          )}
                          {msg.text}
                        </div>
                      )}
                      <div className="wp-actions">
                        <button
                          type="button"
                          onClick={() => void runAction(worker, "start")}
                          disabled={!!runtimeDisabled || anyRunning}
                          title={runtimeDisabled ?? "Pokreni odmah"}
                          className="wp-btn wp-btn--run"
                        >
                          {isRunning("start") ? (
                            <RefreshCw className="wp-icon-xs wp-spin" />
                          ) : (
                            <Play className="wp-icon-xs" />
                          )}
                          {isRunning("start") ? "Pokretanje..." : "Pokreni odmah"}
                        </button>
                        <button
                          type="button"
                          onClick={() => void runAction(worker, "stop")}
                          disabled={!!runtimeDisabled || anyRunning}
                          title={runtimeDisabled ?? "Zaustavi"}
                          className="wp-btn wp-btn--stop"
                        >
                          {isRunning("stop") ? (
                            <RefreshCw className="wp-icon-xs wp-spin" />
                          ) : (
                            <Pause className="wp-icon-xs" />
                          )}
                          {isRunning("stop") ? "Zaustavljanje..." : "Zaustavi"}
                        </button>
                        <button
                          type="button"
                          onClick={() => void runAction(worker, "restart")}
                          disabled={!!runtimeDisabled || anyRunning}
                          title={runtimeDisabled ?? "Restartuj"}
                          className="wp-btn wp-btn--restart"
                        >
                          {isRunning("restart") ? (
                            <RefreshCw className="wp-icon-xs wp-spin" />
                          ) : (
                            <RotateCcw className="wp-icon-xs" />
                          )}
                          {isRunning("restart") ? "Restartovanje..." : "Restartuj"}
                        </button>
                        {worker.scheduleEnabled ? (
                          <button
                            type="button"
                            onClick={() => void runAction(worker, "disableSchedule")}
                            disabled={!!scheduleDisabled || anyRunning}
                            title={scheduleDisabled ?? "Onemogući raspored"}
                            className="wp-btn wp-btn--sched-off"
                          >
                            {isRunning("disableSchedule") ? (
                              <RefreshCw className="wp-icon-xs wp-spin" />
                            ) : (
                              <CalendarOff className="wp-icon-xs" />
                            )}
                            {isRunning("disableSchedule") ? "..." : "Onemogući raspored"}
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => void runAction(worker, "enableSchedule")}
                            disabled={!!scheduleDisabled || anyRunning}
                            title={scheduleDisabled ?? "Omogući raspored"}
                            className="wp-btn wp-btn--sched-on"
                          >
                            {isRunning("enableSchedule") ? (
                              <RefreshCw className="wp-icon-xs wp-spin" />
                            ) : (
                              <CalendarCheck className="wp-icon-xs" />
                            )}
                            {isRunning("enableSchedule") ? "..." : "Omogući raspored"}
                          </button>
                        )}
                      </div>
                      {(runtimeDisabled ?? scheduleDisabled) && (
                        <div className="wp-disabled-reason">{runtimeDisabled ?? scheduleDisabled}</div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {orderedWorkers.length > 0 && (
        <div className="wp-footer">Auto-osvežavanje svaki {refreshInterval / 1000}s.</div>
      )}
    </div>
  );
};

export default WorkersPanel;
