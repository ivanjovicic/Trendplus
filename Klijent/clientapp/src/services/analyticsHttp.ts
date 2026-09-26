import { makeUrl } from "./analyticsApi";
import {
  API_FAILOVER_TIMEOUT_MS_OPTION,
  type ApiFailoverRequestInit,
} from "../utils/apiFailover";
import { FetchTimeoutError, fetchWithTimeout } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS, getRetryTimeouts } from "../utils/apiTimeouts";
import type { ZodType } from "zod";
import {
  AnalyticsResponseValidationError,
  validateAnalyticsResponse,
} from "../validation/analyticsResponseValidation";
import { assertAnalyticsMetaSuccess } from "../utils/analyticsResponseMeta";
import {
  ANALYTICS_ERROR_FALLBACK_MESSAGE,
  getSafeAnalyticsErrorMessage,
} from "../utils/analyticsErrorMessages";

type FetchAnalyticsJsonOptions = {
  signal?: AbortSignal;
  timeoutMs?: number;
  dedupe?: boolean;
  schema?: ZodType<unknown>;
};

export class ApiHttpError extends Error {
  readonly status: number;
  readonly errorCode: string | null;
  readonly correlationId: string | null;

  constructor(status: number, message: string, errorCode?: string | null, correlationId?: string | null) {
    super(message);
    this.name = "ApiHttpError";
    this.status = status;
    this.errorCode = errorCode ?? null;
    this.correlationId = correlationId ?? null;
  }
}

const DEFAULT_TIMEOUT_MS = API_COLD_START_TIMEOUT_MS;
const inFlightGetRequests = new Map<string, Promise<unknown>>();

function validateFetchPayload<T>(
  payload: T,
  schema: ZodType<unknown> | undefined,
  fallbackMessage?: string,
): T {
  const checked = assertAnalyticsMetaSuccess(
    payload,
    (candidate) => {
      if (!candidate || typeof candidate !== "object") return null;
      const meta = (candidate as { meta?: unknown }).meta;
      return meta && typeof meta === "object" ? meta as import("../types/analytics").AnalyticsResponseMeta : null;
    },
    fallbackMessage ?? "Podaci trenutno nisu dostupni.",
  );

  return schema
    ? validateAnalyticsResponse<T>(checked, schema, fallbackMessage ?? "Analytics")
    : checked;
}

type FailoverAwareWindow = Window & {
  __trendplusFailoverInstalled?: boolean;
};

type ApiErrorDetails = {
  message: string;
  errorCode: string | null;
  correlationId: string | null;
};

async function parseApiError(res: Response, fallbackMessage?: string): Promise<ApiErrorDetails> {
  const contentType = res.headers.get("content-type") ?? "";
  let candidate: string | null = null;
  let errorCode: string | null = null;
  let correlationId: string | null = null;

  const isJson = contentType.includes("json");
  if (isJson) {
    const payload = (await res.json().catch(() => null)) as
      | { detail?: string; title?: string; message?: string; errorCode?: string; correlationId?: string }
      | null;

    candidate = payload?.detail ?? payload?.message ?? payload?.title ?? null;
    errorCode = typeof payload?.errorCode === "string" ? payload.errorCode : null;
    correlationId = typeof payload?.correlationId === "string" ? payload.correlationId : null;
  }

  if (!candidate && !isJson) {
    candidate = (await res.text()).trim() || null;
  }

  return {
    message: getSafeAnalyticsErrorMessage(
      candidate,
      errorCode,
      fallbackMessage ?? ANALYTICS_ERROR_FALLBACK_MESSAGE,
    ),
    errorCode,
    correlationId,
  };
}

function isApiFailoverLayerActive(): boolean {
  if (typeof window === "undefined") return false;
  return Boolean((window as FailoverAwareWindow).__trendplusFailoverInstalled);
}

async function fetchAnalyticsResponse(
  url: string,
  signal: AbortSignal | undefined,
  timeoutMs: number
): Promise<Response> {
  if (isApiFailoverLayerActive()) {
    // The global failover fetch layer already applies host failover and per-host
    // timeouts. Wrapping it in an additional timeout causes the first timeout to
    // abort the fallback request before it can complete.
    const init: ApiFailoverRequestInit = {
      signal,
      [API_FAILOVER_TIMEOUT_MS_OPTION]: timeoutMs,
    };
    return fetch(url, init);
  }

  return fetchWithTimeout(url, { signal }, timeoutMs);
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
  fallbackMessage?: string,
  schema?: ZodType<unknown>,
): Promise<T> {
  if (isApiFailoverLayerActive()) {
    const response = await fetchAnalyticsResponse(url, signal, timeoutMs);
    if (!response.ok) {
      const error = await parseApiError(response, fallbackMessage);
      throw new ApiHttpError(response.status, error.message, error.errorCode, error.correlationId);
    }
    const payload = (await response.json()) as T;
    return validateFetchPayload(payload, schema, fallbackMessage);
  }

  const { firstAttemptTimeoutMs, totalTimeoutMs } = getRetryTimeouts(timeoutMs);
  
  try {
    const response = await fetchAnalyticsResponse(url, signal, firstAttemptTimeoutMs);
    if (!response.ok) {
      const error = await parseApiError(response, fallbackMessage);
      throw new ApiHttpError(response.status, error.message, error.errorCode, error.correlationId);
    }
    const payload = (await response.json()) as T;
    return validateFetchPayload(payload, schema, fallbackMessage);
  } catch (error) {
    // Don't retry on abort or non-timeout errors
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }
    
    if (!(error instanceof FetchTimeoutError)) {
      throw error;
    }

    // First attempt timed out - retry with longer timeout (for cold-start backends)
    const response = await fetchAnalyticsResponse(url, signal, totalTimeoutMs);
    if (!response.ok) {
      const error = await parseApiError(response, fallbackMessage);
      throw new ApiHttpError(response.status, error.message, error.errorCode, error.correlationId);
    }
    const payload = (await response.json()) as T;
    return validateFetchPayload(payload, schema, fallbackMessage);
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
      return await fetchWithRetry<T>(url, options?.signal, timeoutMs, fallbackMessage, options?.schema);
    } catch (error) {
      if (error instanceof FetchTimeoutError) {
        throw new Error(fallbackMessage ? `${fallbackMessage}: zahtev je istekao.` : error.message);
      }

      if (error instanceof ApiHttpError) {
        const safeMessage = getSafeAnalyticsErrorMessage(
          error.message,
          error.errorCode,
          fallbackMessage ?? ANALYTICS_ERROR_FALLBACK_MESSAGE,
        );
        const correlationSuffix = error.correlationId && !error.message.includes(error.correlationId)
          ? ` (referentni ID: ${error.correlationId})`
          : "";
        throw new ApiHttpError(error.status, `${safeMessage}${correlationSuffix}`, error.errorCode, error.correlationId);
      }

      if (error instanceof DOMException && error.name === "AbortError") {
        throw error;
      }

      if (error instanceof AnalyticsResponseValidationError) {
        throw error;
      }

      if (error instanceof Error) {
        const safeMessage = getSafeAnalyticsErrorMessage(
          error.message,
          null,
          fallbackMessage ?? ANALYTICS_ERROR_FALLBACK_MESSAGE,
        );
        throw new Error(safeMessage);
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
