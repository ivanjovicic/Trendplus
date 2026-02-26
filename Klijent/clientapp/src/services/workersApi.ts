const API = import.meta.env.VITE_API_BASE_URL || "";

export interface WorkerStatusItem {
  workerName: string;
  status: string;
  lastHeartbeat: string;
  message?: string;
  lastError?: string;
  lastErrorTime?: string;
  errorCount: number;
  isStale: boolean;
}

export interface WorkerHealthWithControl {
  totalWorkers: number;
  healthyWorkers: number;
  runningWorkers: number;
  errorWorkers: number;
  stoppedWorkers: number;
  staleWorkers: number;
  hasCriticalIssues: boolean;
  workers: WorkerStatusItem[];
  workersEnabled: boolean;
  environment?: string;
  lastSwitchAtUtc?: string;
  lastSwitchBy?: string;
}

export interface WorkerControlState {
  enabled: boolean;
  environment: string;
  lastChangedUtc: string;
  lastChangedBy: string;
}

export async function getWorkersHealth(): Promise<WorkerHealthWithControl> {
  const res = await fetch(`${API}/api/workers/health`);
  if (!res.ok) throw new Error("Neuspesno citanje worker health statusa.");
  return res.json();
}

export async function getWorkersControl(): Promise<WorkerControlState> {
  const res = await fetch(`${API}/api/workers/control`);
  if (!res.ok) throw new Error("Neuspesno citanje worker control statusa.");
  return res.json();
}

export async function enableWorkers(): Promise<void> {
  const res = await fetch(`${API}/api/workers/control/enable`, { method: "POST" });
  if (!res.ok) throw new Error("Neuspesno ukljucivanje workera.");
}

export async function disableWorkers(): Promise<void> {
  const res = await fetch(`${API}/api/workers/control/disable`, { method: "POST" });
  if (!res.ok) throw new Error("Neuspesno iskljucivanje workera.");
}

