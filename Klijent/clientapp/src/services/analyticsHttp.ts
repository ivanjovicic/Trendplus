import { makeUrl } from "./analyticsApi";
import { FetchTimeoutError, fetchWithTimeout } from "../utils/fetchWithTimeout";

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

const DEFAULT_TIMEOUT_MS = 60_000;
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
      const response = await fetchWithTimeout(url, { signal: options?.signal }, timeoutMs);
      if (!response.ok) {
        throw new ApiHttpError(response.status, await parseApiError(response, fallbackMessage));
      }

      return (await response.json()) as T;
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
