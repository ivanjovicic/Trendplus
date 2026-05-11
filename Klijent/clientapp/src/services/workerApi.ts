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

class WorkerApi {
  async getWorkersConfiguration(): Promise<WorkerConfigurationResponse> {
    const response = await fetch(apiUrl("/api/workers/configuration"));
    if (!response.ok) {
      throw new Error(`Failed to fetch workers configuration: ${response.statusText}`);
    }
    return response.json();
  }

  async startWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/start`), {
      method: "POST",
    });
    if (!response.ok) {
      throw new Error(`Failed to start worker: ${response.statusText}`);
    }
    return response.json();
  }

  async stopWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/stop`), {
      method: "POST",
    });
    if (!response.ok) {
      throw new Error(`Failed to stop worker: ${response.statusText}`);
    }
    return response.json();
  }

  async restartWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/restart`), {
      method: "POST",
    });
    if (!response.ok) {
      throw new Error(`Failed to restart worker: ${response.statusText}`);
    }
    return response.json();
  }

  async enableSchedule(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/schedule/enable`), {
      method: "POST",
    });
    if (!response.ok) {
      throw new Error(`Failed to enable schedule: ${response.statusText}`);
    }
    return response.json();
  }

  async disableSchedule(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(apiUrl(`/api/workers/${encodeURIComponent(workerName)}/schedule/disable`), {
      method: "POST",
    });
    if (!response.ok) {
      throw new Error(`Failed to disable schedule: ${response.statusText}`);
    }
    return response.json();
  }
}

export const workerApi = new WorkerApi();
