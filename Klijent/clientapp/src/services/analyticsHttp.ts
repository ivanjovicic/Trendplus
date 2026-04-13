import { makeUrl } from "./analyticsApi";
import { FetchTimeoutError, fetchWithTimeout } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS, getRetryTimeouts } from "../utils/apiTimeouts";

type FetchAnalyticsJsonOptions = {
  signal?: AbortSignal;
  timeoutMs?: number;
  dedupe?: boolean;
};

export class ApiHttpError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiHttpError";
    this.status = status;
  }
}

const DEFAULT_TIMEOUT_MS = API_COLD_START_TIMEOUT_MS;
const inFlightGetRequests = new Map<string, Promise<unknown>>();

async function parseApiError(res: Response, fallbackMessage?: string): Promise<string> {
  const contentType = res.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    const payload = (await res.json().catch(() => null)) as
      | { detail?: string; title?: string; message?: string }
      | null;

    const detail = payload?.detail ?? payload?.message ?? payload?.title;
    if (detail && fallbackMessage) {
      return detail.startsWith(fallbackMessage) ? detail : `${fallbackMessage}: ${detail}`;
    }
    if (detail) return detail;
  }

  const text = (await res.text()).trim();
  if (text && fallbackMessage) {
    return text.startsWith(fallbackMessage) ? text : `${fallbackMessage}: ${text}`;
  }
  if (text) return text;

  return fallbackMessage ?? `HTTP ${res.status}`;
}

/**
 * Sends a request with automatic retry if backend appears to be cold-starting.
 * First attempt has a short timeout for quick failure detection.
 * If it fails, retries with longer timeout.
 */
async function fetchWithRetry<T>(
  url: string,
  signal: AbortSignal | undefined,
  timeoutMs: number,
  fallbackMessage?: string
): Promise<T> {
  const { firstAttemptTimeoutMs, totalTimeoutMs } = getRetryTimeouts(timeoutMs);
  
  try {
    const response = await fetchWithTimeout(url, { signal }, firstAttemptTimeoutMs);
    if (!response.ok) {
      throw new ApiHttpError(response.status, await parseApiError(response, fallbackMessage));
    }
    return (await response.json()) as T;
  } catch (error) {
    // Don't retry on abort or non-timeout errors
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }
    
    if (!(error instanceof FetchTimeoutError)) {
      throw error;
    }

    // First attempt timed out - retry with longer timeout (for cold-start backends)
  const response = await fetchWithTimeout(url, { signal }, totalTimeoutMs);
    if (!response.ok) {
      throw new ApiHttpError(response.status, await parseApiError(response, fallbackMessage));
    }
    return (await response.json()) as T;
  }
}

export async function fetchAnalyticsJson<T>(
  path: string,
  params?: URLSearchParams,
  fallbackMessage?: string,
  options?: FetchAnalyticsJsonOptions
): Promise<T> {
  const url = makeUrl(path, params);
  const timeoutMs = options?.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const dedupeEnabled = options?.dedupe ?? true;
  const canDedupe = dedupeEnabled && !options?.signal;

  if (canDedupe) {
    const existing = inFlightGetRequests.get(url);
    if (existing) {
      return existing as Promise<T>;
    }
  }

  const request = (async () => {
    try {
      return await fetchWithRetry<T>(url, options?.signal, timeoutMs, fallbackMessage);
    } catch (error) {
      if (error instanceof FetchTimeoutError) {
        throw new Error(fallbackMessage ? `${fallbackMessage}: zahtev je istekao.` : error.message);
      }

      if (error instanceof ApiHttpError) {
        if (!fallbackMessage || error.message.startsWith(fallbackMessage)) {
          throw error;
        }

        throw new ApiHttpError(error.status, `${fallbackMessage}: ${error.message}`);
      }

      if (error instanceof DOMException && error.name === "AbortError") {
        throw error;
      }

      if (error instanceof Error) {
        throw new Error(fallbackMessage ? `${fallbackMessage}: ${error.message}` : error.message);
      }

      throw new Error(fallbackMessage ?? "Nepoznata greska pri ucitavanju podataka.");
    }
  })();

  if (canDedupe) {
    inFlightGetRequests.set(url, request as Promise<unknown>);
  }

  try {
    return await request;
  } finally {
    if (canDedupe) {
      inFlightGetRequests.delete(url);
    }
  }
}
