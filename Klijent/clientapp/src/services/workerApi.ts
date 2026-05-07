/**
 * API service for worker management and configuration
 */

export interface WorkerStatus {
  workerName: string;
  runtimeStatus: string;
  lastHeartbeat?: string;
  lastError?: string;
  lastErrorTime?: string;
  errorCount: number;
  isScheduleEnabled: boolean;
  isManuallyStopped: boolean;
  updatedAtUtc: string;
  updatedBy?: string;
}

export interface WorkersListResponse {
  workers: WorkerStatus[];
  total: number;
}

export interface WorkerActionResponse {
  success: boolean;
  message: string;
}

class WorkerApi {
  private baseUrl = '/api/admin';

  async getWorkersList(): Promise<WorkersListResponse> {
    const response = await fetch(`${this.baseUrl}/workers/list`);
    if (!response.ok) {
      throw new Error(`Failed to fetch workers list: ${response.statusText}`);
    }
    return response.json();
  }

  async getWorkerDetails(workerName: string): Promise<WorkerStatus> {
    const response = await fetch(`${this.baseUrl}/workers/${encodeURIComponent(workerName)}`);
    if (!response.ok) {
      throw new Error(`Failed to fetch worker details: ${response.statusText}`);
    }
    return response.json();
  }

  async stopWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(
      `${this.baseUrl}/workers/${encodeURIComponent(workerName)}/stop`,
      { method: 'POST' }
    );
    if (!response.ok) {
      throw new Error(`Failed to stop worker: ${response.statusText}`);
    }
    return response.json();
  }

  async resumeWorker(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(
      `${this.baseUrl}/workers/${encodeURIComponent(workerName)}/resume`,
      { method: 'POST' }
    );
    if (!response.ok) {
      throw new Error(`Failed to resume worker: ${response.statusText}`);
    }
    return response.json();
  }

  async enableSchedule(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(
      `${this.baseUrl}/workers/${encodeURIComponent(workerName)}/schedule/enable`,
      { method: 'POST' }
    );
    if (!response.ok) {
      throw new Error(`Failed to enable schedule: ${response.statusText}`);
    }
    return response.json();
  }

  async disableSchedule(workerName: string): Promise<WorkerActionResponse> {
    const response = await fetch(
      `${this.baseUrl}/workers/${encodeURIComponent(workerName)}/schedule/disable`,
      { method: 'POST' }
    );
    if (!response.ok) {
      throw new Error(`Failed to disable schedule: ${response.statusText}`);
    }
    return response.json();
  }
}

export const workerApi = new WorkerApi();
