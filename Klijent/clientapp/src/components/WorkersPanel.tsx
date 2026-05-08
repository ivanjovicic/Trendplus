import React, { useEffect, useMemo, useState } from "react";
import { workerApi, type WorkerConfigurationItem } from "../services/workerApi";
import { AlertCircle, CheckCircle2, Clock, Pause, Play, RefreshCw, RotateCcw } from "lucide-react";

interface WorkersPanelProps {
  refreshInterval?: number;
}

type WorkerAction = "start" | "stop" | "restart" | "enableSchedule" | "disableSchedule";

export const WorkersPanel: React.FC<WorkersPanelProps> = ({ refreshInterval = 5000 }) => {
  const [workers, setWorkers] = useState<WorkerConfigurationItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const fetchWorkers = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await workerApi.getWorkersConfiguration();
      setWorkers(response.workers);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Greška pri učitavanju radnika.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchWorkers();
    const interval = window.setInterval(() => {
      void fetchWorkers();
    }, refreshInterval);
    return () => window.clearInterval(interval);
  }, [refreshInterval]);

  const runAction = async (worker: WorkerConfigurationItem, action: WorkerAction) => {
    try {
      setActionInProgress(`${worker.workerName}:${action}`);
      setError(null);

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

      setSuccessMessage(responseMessage);
      window.setTimeout(() => setSuccessMessage(null), 3000);
      await fetchWorkers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Akcija nije uspela.");
    } finally {
      setActionInProgress(null);
    }
  };

  const statusClass = (status: string) => {
    const normalized = status.toLowerCase();
    if (normalized.includes("error")) return "bg-red-100 text-red-800";
    if (normalized.includes("running") || normalized.includes("healthy")) return "bg-green-100 text-green-800";
    if (normalized.includes("stopped") || normalized.includes("disabled")) return "bg-yellow-100 text-yellow-900";
    if (normalized.includes("configuredbutnotrunning")) return "bg-gray-100 text-gray-700";
    return "bg-gray-100 text-gray-800";
  };

  const statusIcon = (status: string) => {
    const normalized = status.toLowerCase();
    if (normalized.includes("error")) return <AlertCircle className="w-3 h-3" />;
    if (normalized.includes("running") || normalized.includes("healthy")) return <CheckCircle2 className="w-3 h-3" />;
    return <Clock className="w-3 h-3" />;
  };

  const formatDate = (raw?: string | null) => {
    if (!raw) return "-";
    const date = new Date(raw);
    if (Number.isNaN(date.getTime())) return raw;
    return date.toLocaleString("sr-RS");
  };

  const orderedWorkers = useMemo(
    () => [...workers].sort((a, b) => a.displayName.localeCompare(b.displayName)),
    [workers]
  );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button
          onClick={() => void fetchWorkers()}
          disabled={loading}
          className="px-3 py-1 text-sm bg-blue-500 text-white rounded hover:bg-blue-600 disabled:opacity-50 inline-flex items-center gap-2"
        >
          <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
          Osveži
        </button>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded p-3 flex items-center gap-2 text-red-700 text-sm">
          <AlertCircle className="w-4 h-4" />
          {error}
        </div>
      )}

      {successMessage && (
        <div className="bg-green-50 border border-green-200 rounded p-3 flex items-center gap-2 text-green-700 text-sm">
          <CheckCircle2 className="w-4 h-4" />
          {successMessage}
        </div>
      )}

      {loading && orderedWorkers.length === 0 ? (
        <div className="text-center py-8 text-gray-500">Učitavanje radnika...</div>
      ) : orderedWorkers.length === 0 ? (
        <div className="text-center py-8 text-gray-500">Nema registrovanih radnika.</div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse border border-gray-300">
            <thead className="bg-gray-100">
              <tr>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Naziv</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Status</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Raspored</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Poslednji heartbeat</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Poslednje pokretanje</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Sledeće pokretanje</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Poslednja greška</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Akcije</th>
              </tr>
            </thead>
            <tbody>
              {orderedWorkers.map((worker) => {
                const actionKey = (action: WorkerAction) => `${worker.workerName}:${action}`;
                const runtimeDisabledReason = !worker.isRuntimeControllable
                  ? worker.runtimeControlReason ?? "Akcija nije podržana za ovaj worker."
                  : undefined;
                const scheduleDisabledReason = !worker.isScheduleControllable
                  ? worker.scheduleControlReason ?? "Akcija nije podržana za ovaj worker."
                  : undefined;

                return (
                  <tr key={worker.workerName} className="hover:bg-gray-50 align-top">
                    <td className="border border-gray-300 px-3 py-2 text-sm">
                      <div className="font-semibold">{worker.displayName}</div>
                      <div className="text-xs text-gray-500">{worker.workerName}</div>
                      <div className="text-xs text-gray-500 mt-1">{worker.description}</div>
                      {worker.isConfiguredButNotRunning && (
                        <div className="text-xs text-amber-700 mt-1">Konfigurisano, ali ne radi u ovom procesu.</div>
                      )}
                    </td>
                    <td className="border border-gray-300 px-3 py-2 text-sm">
                      <span className={`px-2 py-1 rounded-full text-xs font-medium inline-flex items-center gap-1 ${statusClass(worker.status)}`}>
                        {statusIcon(worker.status)}
                        {worker.status}
                      </span>
                    </td>
                    <td className="border border-gray-300 px-3 py-2 text-sm">
                      {worker.scheduleEnabled ? (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-800">Omogućen</span>
                      ) : (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800">Onemogućen</span>
                      )}
                    </td>
                    <td className="border border-gray-300 px-3 py-2 text-sm text-gray-600">{formatDate(worker.lastHeartbeat)}</td>
                    <td className="border border-gray-300 px-3 py-2 text-sm text-gray-600">{formatDate(worker.lastRunAt)}</td>
                    <td className="border border-gray-300 px-3 py-2 text-sm text-gray-600">{formatDate(worker.nextRunAt)}</td>
                    <td className="border border-gray-300 px-3 py-2 text-sm text-gray-700">
                      {worker.lastError ? (
                        <span className="text-red-700">{worker.lastError}</span>
                      ) : (
                        "-"
                      )}
                    </td>
                    <td className="border border-gray-300 px-3 py-2 text-sm">
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => void runAction(worker, "start")}
                          disabled={!!runtimeDisabledReason || actionInProgress === actionKey("start")}
                          title={runtimeDisabledReason}
                          className="px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50 inline-flex items-center gap-1"
                        >
                          <Play className="w-3 h-3" />
                          Pokreni odmah
                        </button>
                        <button
                          type="button"
                          onClick={() => void runAction(worker, "stop")}
                          disabled={!!runtimeDisabledReason || actionInProgress === actionKey("stop")}
                          title={runtimeDisabledReason}
                          className="px-2 py-1 text-xs bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50 inline-flex items-center gap-1"
                        >
                          <Pause className="w-3 h-3" />
                          Zaustavi
                        </button>
                        <button
                          type="button"
                          onClick={() => void runAction(worker, "restart")}
                          disabled={!!runtimeDisabledReason || actionInProgress === actionKey("restart")}
                          title={runtimeDisabledReason}
                          className="px-2 py-1 text-xs bg-orange-600 text-white rounded hover:bg-orange-700 disabled:opacity-50 inline-flex items-center gap-1"
                        >
                          <RotateCcw className="w-3 h-3" />
                          Restartuj
                        </button>
                        {worker.scheduleEnabled ? (
                          <button
                            type="button"
                            onClick={() => void runAction(worker, "disableSchedule")}
                            disabled={!!scheduleDisabledReason || actionInProgress === actionKey("disableSchedule")}
                            title={scheduleDisabledReason}
                            className="px-2 py-1 text-xs bg-yellow-600 text-white rounded hover:bg-yellow-700 disabled:opacity-50"
                          >
                            Onemogući raspored
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => void runAction(worker, "enableSchedule")}
                            disabled={!!scheduleDisabledReason || actionInProgress === actionKey("enableSchedule")}
                            title={scheduleDisabledReason}
                            className="px-2 py-1 text-xs bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50"
                          >
                            Omogući raspored
                          </button>
                        )}
                      </div>
                      {runtimeDisabledReason && (
                        <div className="mt-2 text-xs text-gray-500">{runtimeDisabledReason}</div>
                      )}
                      {!runtimeDisabledReason && scheduleDisabledReason && (
                        <div className="mt-2 text-xs text-gray-500">{scheduleDisabledReason}</div>
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
        <div className="text-xs text-gray-500 mt-2">
          Prikazano {orderedWorkers.length} radnik(a). Automatsko osvežavanje na {refreshInterval / 1000}s.
        </div>
      )}
    </div>
  );
};

export default WorkersPanel;
