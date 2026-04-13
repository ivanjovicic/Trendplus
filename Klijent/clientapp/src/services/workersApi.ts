import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS } from "../utils/apiTimeouts";

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
  runtimeToggleAllowed: boolean;
  environment?: string;
  lastSwitchAtUtc?: string;
  lastSwitchBy?: string;
}

export interface WorkerControlState {
  enabled: boolean;
  environment: string;
  runtimeToggleAllowed: boolean;
  lastChangedUtc: string;
  lastChangedBy: string;
}

async function ensureOk(res: Response, message: string): Promise<void> {
  if (res.ok) return;

  let detail = "";
  try {
    const body = await res.json();
    detail = body?.detail || body?.message || "";
  } catch {
    // no-op
  }

  const suffix = detail ? `: ${detail}` : "";
  throw new Error(`${message} (HTTP ${res.status}${suffix})`);
}

export async function getWorkersHealth(): Promise<WorkerHealthWithControl> {
  const res = await fetchWithTimeout(apiUrl("/api/workers/health"), undefined, API_COLD_START_TIMEOUT_MS);
  await ensureOk(res, "Neuspesno citanje worker health statusa");
  return res.json();
}

export async function getWorkersControl(): Promise<WorkerControlState> {
  const res = await fetchWithTimeout(apiUrl("/api/workers/control"), undefined, API_COLD_START_TIMEOUT_MS);
  await ensureOk(res, "Neuspesno citanje worker control statusa");
  return res.json();
}

export async function enableWorkers(): Promise<void> {
  const res = await fetchWithTimeout(apiUrl("/api/workers/control/enable"), { method: "POST" }, API_COLD_START_TIMEOUT_MS);
  await ensureOk(res, "Neuspesno ukljucivanje workera");
}

export async function disableWorkers(): Promise<void> {
  const res = await fetchWithTimeout(apiUrl("/api/workers/control/disable"), { method: "POST" }, API_COLD_START_TIMEOUT_MS);
  await ensureOk(res, "Neuspesno iskljucivanje workera");
}
