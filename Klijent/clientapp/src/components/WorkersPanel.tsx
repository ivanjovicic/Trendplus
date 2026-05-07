import React, { useEffect, useState } from 'react';
import { workerApi, WorkerStatus } from '../services/workerApi';
import { AlertCircle, CheckCircle2, XCircle, Clock, Play, Pause, RotateCcw } from 'lucide-react';

interface WorkersPanelProps {
  refreshInterval?: number;
}

export const WorkersPanel: React.FC<WorkersPanelProps> = ({ refreshInterval = 5000 }) => {
  const [workers, setWorkers] = useState<WorkerStatus[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const fetchWorkers = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await workerApi.getWorkersList();
      setWorkers(response.workers);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load workers');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWorkers();
    const interval = setInterval(fetchWorkers, refreshInterval);
    return () => clearInterval(interval);
  }, [refreshInterval]);

  const handleStopWorker = async (workerName: string) => {
    if (!window.confirm(`Are you sure you want to stop "${workerName}"?`)) {
      return;
    }
    
    try {
      setActionInProgress(workerName);
      await workerApi.stopWorker(workerName);
      setSuccessMessage(`Worker "${workerName}" stopped successfully`);
      setTimeout(() => setSuccessMessage(null), 3000);
      await fetchWorkers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to stop worker');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleResumeWorker = async (workerName: string) => {
    try {
      setActionInProgress(workerName);
      await workerApi.resumeWorker(workerName);
      setSuccessMessage(`Worker "${workerName}" resumed successfully`);
      setTimeout(() => setSuccessMessage(null), 3000);
      await fetchWorkers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to resume worker');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleEnableSchedule = async (workerName: string) => {
    try {
      setActionInProgress(workerName);
      await workerApi.enableSchedule(workerName);
      setSuccessMessage(`Schedule enabled for "${workerName}"`);
      setTimeout(() => setSuccessMessage(null), 3000);
      await fetchWorkers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to enable schedule');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleDisableSchedule = async (workerName: string) => {
    if (!window.confirm(`Are you sure you want to disable scheduling for "${workerName}"? Manual start will still be allowed.`)) {
      return;
    }
    
    try {
      setActionInProgress(workerName);
      await workerApi.disableSchedule(workerName);
      setSuccessMessage(`Schedule disabled for "${workerName}"`);
      setTimeout(() => setSuccessMessage(null), 3000);
      await fetchWorkers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to disable schedule');
    } finally {
      setActionInProgress(null);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status.toLowerCase()) {
      case 'running':
        return <CheckCircle2 className="w-4 h-4 text-green-600" />;
      case 'stopped':
        return <XCircle className="w-4 h-4 text-red-600" />;
      case 'error':
        return <AlertCircle className="w-4 h-4 text-orange-600" />;
      default:
        return <Clock className="w-4 h-4 text-gray-400" />;
    }
  };

  const getStatusBadge = (status: string) => {
    const baseClasses = 'px-2 py-1 rounded-full text-xs font-medium flex items-center gap-1';
    switch (status.toLowerCase()) {
      case 'running':
        return <span className={`${baseClasses} bg-green-100 text-green-800`}>{getStatusIcon(status)} Running</span>;
      case 'stopped':
        return <span className={`${baseClasses} bg-red-100 text-red-800`}>{getStatusIcon(status)} Stopped</span>;
      case 'error':
        return <span className={`${baseClasses} bg-orange-100 text-orange-800`}>{getStatusIcon(status)} Error</span>;
      default:
        return <span className={`${baseClasses} bg-gray-100 text-gray-800`}>{getStatusIcon(status)} Unknown</span>;
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h3 className="text-lg font-semibold">Worker Management</h3>
        <button
          onClick={fetchWorkers}
          disabled={loading}
          className="px-3 py-1 text-sm bg-blue-500 text-white rounded hover:bg-blue-600 disabled:opacity-50"
        >
          {loading ? 'Refreshing...' : 'Refresh'}
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

      {loading && workers.length === 0 ? (
        <div className="text-center py-8 text-gray-500">Loading workers...</div>
      ) : workers.length === 0 ? (
        <div className="text-center py-8 text-gray-500">No workers found</div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse border border-gray-300">
            <thead className="bg-gray-100">
              <tr>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Worker Name</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Status</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Schedule</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Last Heartbeat</th>
                <th className="border border-gray-300 px-3 py-2 text-left text-sm font-semibold">Actions</th>
              </tr>
            </thead>
            <tbody>
              {workers.map((worker) => (
                <tr key={worker.workerName} className="hover:bg-gray-50">
                  <td className="border border-gray-300 px-3 py-2 text-sm font-mono">{worker.workerName}</td>
                  <td className="border border-gray-300 px-3 py-2 text-sm">
                    {getStatusBadge(worker.runtimeStatus)}
                  </td>
                  <td className="border border-gray-300 px-3 py-2 text-sm">
                    {worker.isScheduleEnabled ? (
                      <span className="px-2 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                        Enabled
                      </span>
                    ) : (
                      <span className="px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                        Disabled
                      </span>
                    )}
                  </td>
                  <td className="border border-gray-300 px-3 py-2 text-sm text-gray-600">
                    {worker.lastHeartbeat ? new Date(worker.lastHeartbeat).toLocaleString() : 'Never'}
                  </td>
                  <td className="border border-gray-300 px-3 py-2 text-sm">
                    <div className="flex gap-2">
                      {worker.isManuallyStopped ? (
                        <button
                          onClick={() => handleResumeWorker(worker.workerName)}
                          disabled={actionInProgress === worker.workerName}
                          className="px-2 py-1 text-xs bg-green-500 text-white rounded hover:bg-green-600 disabled:opacity-50 flex items-center gap-1"
                          title="Resume worker (allow running)"
                        >
                          <Play className="w-3 h-3" />
                          Resume
                        </button>
                      ) : (
                        <button
                          onClick={() => handleStopWorker(worker.workerName)}
                          disabled={actionInProgress === worker.workerName}
                          className="px-2 py-1 text-xs bg-red-500 text-white rounded hover:bg-red-600 disabled:opacity-50 flex items-center gap-1"
                          title="Stop worker (prevent running)"
                        >
                          <Pause className="w-3 h-3" />
                          Stop
                        </button>
                      )}

                      {worker.isScheduleEnabled ? (
                        <button
                          onClick={() => handleDisableSchedule(worker.workerName)}
                          disabled={actionInProgress === worker.workerName}
                          className="px-2 py-1 text-xs bg-yellow-500 text-white rounded hover:bg-yellow-600 disabled:opacity-50 flex items-center gap-1"
                          title="Disable scheduled execution"
                        >
                          <Clock className="w-3 h-3" />
                          Disable Schedule
                        </button>
                      ) : (
                        <button
                          onClick={() => handleEnableSchedule(worker.workerName)}
                          disabled={actionInProgress === worker.workerName}
                          className="px-2 py-1 text-xs bg-purple-500 text-white rounded hover:bg-purple-600 disabled:opacity-50 flex items-center gap-1"
                          title="Enable scheduled execution"
                        >
                          <Clock className="w-3 h-3" />
                          Enable Schedule
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {workers.length > 0 && (
        <div className="text-xs text-gray-500 mt-2">
          Showing {workers.length} worker(s). Auto-refreshing every {refreshInterval / 1000}s.
        </div>
      )}
    </div>
  );
};

export default WorkersPanel;
