import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS } from "../utils/apiTimeouts";

export type BackendProvider = "render" | "fly";

export interface BackendRoutingPreference {
  primaryProvider: BackendProvider;
  fallbackEnabled: boolean;
  fallbackProvider: BackendProvider;
  updatedAtUtc?: string;
  updatedBy?: string;
}

export interface BackendProviderHealth {
  provider: BackendProvider;
  success: boolean;
  statusCode: number | null;
  latencyMs: number;
  checkedAtUtc: string;
  message: string;
}

async function ensureOk(res: Response, message: string): Promise<void> {
  if (res.ok) return;

  let detail = "";
  try {
    const body = await res.json();
    detail = body?.detail || body?.message || "";
  } catch {
    // ignore
  }

  throw new Error(detail ? `${message}: ${detail}` : `${message} (HTTP ${res.status})`);
}

export async function getBackendRoutingPreference(): Promise<BackendRoutingPreference> {
  const res = await fetchWithTimeout(apiUrl("/api/admin/backend-routing"), undefined, API_COLD_START_TIMEOUT_MS);
  await ensureOk(res, "Neuspesno citanje backend konfiguracije");
  return res.json();
}

export async function updateBackendRoutingPreference(
  payload: BackendRoutingPreference
): Promise<BackendRoutingPreference> {
  const res = await fetchWithTimeout(
    apiUrl("/api/admin/backend-routing"),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    },
    API_COLD_START_TIMEOUT_MS
  );
  await ensureOk(res, "Neuspesno cuvanje backend konfiguracije");
  return res.json();
}

export async function pingBackendProvider(provider: BackendProvider): Promise<BackendProviderHealth> {
  const res = await fetchWithTimeout(
    apiUrl(`/api/admin/backend-routing/ping/${provider}`),
    undefined,
    API_COLD_START_TIMEOUT_MS
  );
  await ensureOk(res, "Neuspesna provera backend providera");
  return res.json();
}
