/**
 * API service for worker configuration management.
 */

import { apiUrl } from "../utils/apiUrl";

export interface WorkerConfigurationItem {
  workerName: string;
  displayName: string;
  description: string;
  workerType: string;
  isRuntimeControllable: boolean;
  isScheduleControllable: boolean;
  runtimeControlReason?: string | null;
  scheduleControlReason?: string | null;
  status: string;
  scheduleEnabled: boolean;
  isManuallyStopped: boolean;
  isRegisteredInCurrentProcess: boolean;
  isConfiguredButNotRunning: boolean;
  lastHeartbeat?: string | null;
  lastRunAt?: string | null;
  nextRunAt?: string | null;
  lastSuccessAt?: string | null;
  lastFailureAt?: string | null;
  lastError?: string | null;
}

export interface WorkerConfigurationResponse {
  processType: string;
  workersEnabledGlobally: boolean;
  runtimeToggleAllowed: boolean;
  total: number;
  workers: WorkerConfigurationItem[];
}

export interface WorkerActionResponse {
  success: boolean;
  message: string;
}

async function handleActionResponse(response: Response): Promise<WorkerActionResponse> {
  if (response.ok) {
    return response.json() as Promise<WorkerActionResponse>;
  }
  // Try to extract a meaningful error from the JSON body
  let detail = response.statusText;
  try {
    const body = await response.json() as { error?: string; message?: string };
    detail = body.error ?? body.message ?? detail;
  } catch {
    // ignore parse errors
  }
  throw new Error(detail || `HTTP ${response.status}`);
}

class WorkerApi {
  async getWorkersConfiguration(): Promise<WorkerConfigurationResponse> {
    const response = await fetch(apiUrl("/api/workers/configuration"));
    if (!response.ok) {
      // Try to extract a meaningful error from the JSON body
      let detail = response.statusText;
      try {
        const body = await response.json() as { error?: string; message?: string };
        detail = body.error ?? body.message ?? detail;
      } catch {
        // ignore parse errors
      }
      throw new Error(detail || `Učitavanje radnika nije uspelo (HTTP ${response.status})`);
    }
    return response.json();
  }

  async startWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/start`), {
      method: "POST",
    });
    return handleActionResponse(response);
  }

  async stopWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/stop`), {
      method: "POST",
    });
    return handleActionResponse(response);
  }

  async restartWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/restart`), {
      method: "POST",
    });
    return handleActionResponse(response);
  }

  async enableSchedule(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/schedule/enable`), {
      method: "POST",
    });
    return handleActionResponse(response);
  }

  async disableSchedule(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/schedule/disable`), {
      method: "POST",
    });
    return handleActionResponse(response);
  }
}

export const workerApi = new WorkerApi();

