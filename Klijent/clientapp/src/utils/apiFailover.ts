type ApiHostRole = "primary" | "fallback";

type PersistedFailoverState = {
  activeHost: ApiHostRole;
  lastPrimaryFailureAt: number | null;
  lastPrimaryProbeAt: number | null;
  updatedAt: number;
};

type RuntimeFailoverState = {
  activeHost: ApiHostRole;
  lastPrimaryFailureAt: number | null;
  lastPrimaryProbeAt: number | null;
};

const STORAGE_KEY = "trendplus:api-failover-state:v1";
const HOST_CHANGED_EVENT = "trendplus:api-host-changed";

const primaryBaseUrl = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL);
const fallbackBaseUrl = normalizeBaseUrl(import.meta.env.VITE_API_FALLBACK_URL);

const failoverConfigured =
  primaryBaseUrl.length > 0 &&
  fallbackBaseUrl.length > 0 &&
  primaryBaseUrl !== fallbackBaseUrl;

const primaryOrigin = toOrigin(primaryBaseUrl);
const fallbackOrigin = toOrigin(fallbackBaseUrl);

const requestTimeoutMs = readMsFromEnv("VITE_API_REQUEST_TIMEOUT_MS", import.meta.env.DEV ? 15_000 : 25_000);
const probeTimeoutMs = readMsFromEnv("VITE_API_FALLBACK_PROBE_TIMEOUT_MS", import.meta.env.DEV ? 3_000 : 5_000);
const primaryRetryCooldownMs = readMsFromEnv("VITE_API_PRIMARY_RETRY_COOLDOWN_MS", import.meta.env.DEV ? 30_000 : 180_000);
const stateTtlMs = readMsFromEnv("VITE_API_FAILOVER_STATE_TTL_MS", import.meta.env.DEV ? 15 * 60_000 : 30 * 60_000);
const healthProbePath = normalizePath(import.meta.env.VITE_API_HEALTH_PATH || "/health");

const apiPathPrefixes = ["/api", "/health", "/artikli", "/scrapers", "/admin"];
const debugEnabled = import.meta.env.DEV || import.meta.env.VITE_API_FALLBACK_DEBUG === "1";
const primaryMissing = primaryBaseUrl.length === 0;
const fallbackMatchesPrimary =
  fallbackBaseUrl.length > 0 &&
  primaryBaseUrl.length > 0 &&
  fallbackBaseUrl === primaryBaseUrl;

let runtimeState: RuntimeFailoverState = loadInitialState();
let primaryProbeInFlight: Promise<boolean> | null = null;
let misconfigurationWarned = false;

function normalizeBaseUrl(raw: string | undefined): string {
  return (raw ?? "").trim().replace(/\/+$/, "");
}

function normalizePath(raw: string): string {
  return raw.startsWith("/") ? raw : `/${raw}`;
}

function readMsFromEnv(name: string, fallback: number): number {
  const parsed = Number(import.meta.env[name]);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function toOrigin(baseUrl: string): string | null {
  if (!baseUrl) return null;
  try {
    return new URL(baseUrl).origin;
  } catch {
    return null;
  }
}

function nowMs(): number {
  return Date.now();
}

function toAbsoluteUrl(value: string): URL | null {
  if (typeof window === "undefined") return null;
  try {
    return new URL(value, window.location.origin);
  } catch {
    return null;
  }
}

function toRawUrl(input: RequestInfo | URL): string {
  if (typeof input === "string") return input;
  if (input instanceof URL) return input.toString();
  return input.url;
}

function isLikelyRelative(rawUrl: string): boolean {
  return !/^https?:\/\//i.test(rawUrl);
}

function isApiPath(pathname: string): boolean {
  return apiPathPrefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

function shouldManageRequest(input: RequestInfo | URL): boolean {
  const rawUrl = toRawUrl(input);
  const parsed = toAbsoluteUrl(rawUrl);
  if (!parsed) return false;

  if (primaryOrigin && parsed.origin === primaryOrigin) return true;
  if (fallbackOrigin && parsed.origin === fallbackOrigin) return true;

  if (isLikelyRelative(rawUrl) && isApiPath(parsed.pathname.toLowerCase())) {
    return true;
  }

  return false;
}

function resolveTargetBaseUrl(): string {
  if (!failoverConfigured) return primaryBaseUrl;
  return runtimeState.activeHost === "fallback" ? fallbackBaseUrl : primaryBaseUrl;
}

function rewriteUrlForTarget(input: RequestInfo | URL, targetBaseUrl: string): string {
  const rawUrl = toRawUrl(input);
  const parsed = toAbsoluteUrl(rawUrl);
  if (!parsed) return rawUrl;

  if (isLikelyRelative(rawUrl)) {
    return `${targetBaseUrl}${parsed.pathname}${parsed.search}${parsed.hash}`;
  }

  if ((primaryOrigin && parsed.origin === primaryOrigin) || (fallbackOrigin && parsed.origin === fallbackOrigin)) {
    return `${targetBaseUrl}${parsed.pathname}${parsed.search}${parsed.hash}`;
  }

  return rawUrl;
}

function buildRewrittenInput(input: RequestInfo | URL, rewrittenUrl: string): RequestInfo | URL {
  if (input instanceof Request) {
    return new Request(rewrittenUrl, input);
  }

  if (input instanceof URL) {
    return new URL(rewrittenUrl);
  }

  return rewrittenUrl;
}

function saveState(): void {
  if (!failoverConfigured) return;
  if (typeof window === "undefined") return;
  try {
    const payload: PersistedFailoverState = {
      activeHost: runtimeState.activeHost,
      lastPrimaryFailureAt: runtimeState.lastPrimaryFailureAt,
      lastPrimaryProbeAt: runtimeState.lastPrimaryProbeAt,
      updatedAt: nowMs(),
    };
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  } catch {
    // Storage may be blocked; keep in-memory fallback behavior.
  }
}

function loadInitialState(): RuntimeFailoverState {
  if (typeof window === "undefined") {
    return {
      activeHost: "primary",
      lastPrimaryFailureAt: null,
      lastPrimaryProbeAt: null,
    };
  }

  if (!failoverConfigured) {
    return {
      activeHost: "primary",
      lastPrimaryFailureAt: null,
      lastPrimaryProbeAt: null,
    };
  }

  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return {
        activeHost: "primary",
        lastPrimaryFailureAt: null,
        lastPrimaryProbeAt: null,
      };
    }

    const parsed = JSON.parse(raw) as PersistedFailoverState;
    const expired = nowMs() - (parsed.updatedAt ?? 0) > stateTtlMs;
    if (expired) {
      window.sessionStorage.removeItem(STORAGE_KEY);
      return {
        activeHost: "primary",
        lastPrimaryFailureAt: null,
        lastPrimaryProbeAt: null,
      };
    }

    return {
      activeHost: parsed.activeHost === "fallback" ? "fallback" : "primary",
      lastPrimaryFailureAt: parsed.lastPrimaryFailureAt ?? null,
      lastPrimaryProbeAt: parsed.lastPrimaryProbeAt ?? null,
    };
  } catch {
    return {
      activeHost: "primary",
      lastPrimaryFailureAt: null,
      lastPrimaryProbeAt: null,
    };
  }
}

function emitHostChanged(nextHost: ApiHostRole): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent(HOST_CHANGED_EVENT, {
      detail: {
        activeHost: nextHost,
        primaryBaseUrl,
        fallbackBaseUrl,
      },
    })
  );
}

function debugLog(message: string, extra?: Record<string, unknown>): void {
  if (!debugEnabled) return;
  if (extra) {
    console.info(`[api-failover] ${message}`, extra);
    return;
  }
  console.info(`[api-failover] ${message}`);
}

function warnIfFailoverIsMisconfigured(): void {
  if (misconfigurationWarned) return;
  misconfigurationWarned = true;

  if (primaryMissing) {
    console.warn("[api-failover] VITE_API_BASE_URL is empty; requests cannot target the intended primary API.");
    return;
  }

  if (fallbackMatchesPrimary) {
    console.warn(
      "[api-failover] VITE_API_BASE_URL and VITE_API_FALLBACK_URL point to the same host; failover is effectively disabled.",
      {
        primaryBaseUrl,
        fallbackBaseUrl,
      }
    );
  }
}

function markPrimaryFailure(reason: string): void {
  if (!failoverConfigured) return;
  runtimeState = {
    ...runtimeState,
    activeHost: "fallback",
    lastPrimaryFailureAt: nowMs(),
  };
  saveState();
  emitHostChanged("fallback");
  debugLog("Primary API marked unavailable; fallback activated", { reason });
}

function markPrimaryProbeAttempt(): void {
  runtimeState = {
    ...runtimeState,
    lastPrimaryProbeAt: nowMs(),
  };
  saveState();
}

function markPrimaryRecovered(): void {
  runtimeState = {
    activeHost: "primary",
    lastPrimaryFailureAt: null,
    lastPrimaryProbeAt: nowMs(),
  };
  saveState();
  emitHostChanged("primary");
  debugLog("Primary API recovered; returning traffic to primary");
}

function keepFallbackActiveAfterPrimaryProbeFailure(reason: string): void {
  runtimeState = {
    ...runtimeState,
    activeHost: "fallback",
    lastPrimaryFailureAt: nowMs(),
  };
  saveState();
  debugLog("Primary API probe failed; keeping fallback active", { reason });
}

function shouldAttemptPrimaryRecovery(): boolean {
  if (!failoverConfigured) return false;
  if (runtimeState.activeHost !== "fallback") return false;

  const now = nowMs();
  const lastFailure = runtimeState.lastPrimaryFailureAt ?? 0;
  if (now - lastFailure < primaryRetryCooldownMs) return false;

  const lastProbe = runtimeState.lastPrimaryProbeAt ?? 0;
  if (now - lastProbe < primaryRetryCooldownMs) return false;

  return true;
}

function isAvailabilityHttpStatus(status: number): boolean {
  return status === 502 || status === 503 || status === 504;
}

function isAvailabilityError(error: unknown): boolean {
  if (error instanceof ApiFailoverTimeoutError) return true;
  if (error instanceof TypeError) return true;
  if (error instanceof DOMException && error.name === "AbortError") return true;

  if (error instanceof Error) {
    const text = `${error.name} ${error.message}`.toLowerCase();
    return (
      text.includes("networkerror") ||
      text.includes("failed to fetch") ||
      text.includes("fetch failed") ||
      text.includes("connection") ||
      text.includes("timeout") ||
      text.includes("econnrefused") ||
      text.includes("dns")
    );
  }

  return false;
}

class ApiFailoverTimeoutError extends Error {
  readonly timeoutMs: number;

  constructor(timeoutMs: number) {
    super(`API request timed out after ${timeoutMs}ms`);
    this.name = "ApiFailoverTimeoutError";
    this.timeoutMs = timeoutMs;
  }
}

async function fetchWithTimeoutVia(
  nativeFetch: typeof window.fetch,
  input: RequestInfo | URL,
  init: RequestInit | undefined,
  timeoutMs: number
): Promise<Response> {
  const controller = new AbortController();
  const externalSignal = init?.signal;
  let didTimeout = false;
  let externallyAborted = false;

  const timeoutId = window.setTimeout(() => {
    didTimeout = true;
    controller.abort(new DOMException("Request timed out", "TimeoutError"));
  }, timeoutMs);

  const onExternalAbort = () => {
    externallyAborted = true;
    controller.abort(externalSignal?.reason);
  };

  if (externalSignal) {
    if (externalSignal.aborted) {
      externallyAborted = true;
      controller.abort(externalSignal.reason);
    } else {
      externalSignal.addEventListener("abort", onExternalAbort, { once: true });
    }
  }

  try {
    return await nativeFetch(input, {
      ...init,
      signal: controller.signal,
    });
  } catch (error) {
    if (didTimeout) {
      throw new ApiFailoverTimeoutError(timeoutMs);
    }

    if (externallyAborted && error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    throw error;
  } finally {
    window.clearTimeout(timeoutId);
    externalSignal?.removeEventListener("abort", onExternalAbort);
  }
}

async function executeApiRequest(
  nativeFetch: typeof window.fetch,
  input: RequestInfo | URL,
  init: RequestInit | undefined
): Promise<Response> {
  const requestSource: RequestInfo | URL = input instanceof Request ? input.clone() : input;
  const targetBase = resolveTargetBaseUrl();
  const targetUrl = rewriteUrlForTarget(requestSource, targetBase);
  const targetInput = buildRewrittenInput(requestSource, targetUrl);
  const initialHost = runtimeState.activeHost;

  try {
    const response = await fetchWithTimeoutVia(nativeFetch, targetInput, init, requestTimeoutMs);

    if (
      failoverConfigured &&
      initialHost === "primary" &&
      isAvailabilityHttpStatus(response.status)
    ) {
      markPrimaryFailure(`status_${response.status}`);
      const retrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
      const fallbackUrl = rewriteUrlForTarget(retrySource, fallbackBaseUrl);
      const fallbackInput = buildRewrittenInput(retrySource, fallbackUrl);
      return await fetchWithTimeoutVia(nativeFetch, fallbackInput, init, requestTimeoutMs);
    }

    if (
      failoverConfigured &&
      initialHost === "fallback" &&
      isAvailabilityHttpStatus(response.status)
    ) {
      const rescued = await tryPrimaryRescueAfterFallbackFailure(
        nativeFetch,
        requestSource,
        init,
        `status_${response.status}`
      );
      if (rescued) {
        return rescued;
      }
    }

    return response;
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError" && init?.signal?.aborted) {
      const abortReason = init.signal.reason;
      const timeoutAbort =
        abortReason instanceof DOMException &&
        abortReason.name === "TimeoutError";

      if (!timeoutAbort) {
        throw error;
      }
    }

    if (
      failoverConfigured &&
      initialHost === "primary" &&
      isAvailabilityError(error)
    ) {
      markPrimaryFailure(error instanceof Error ? error.message : "transport_failure");
      const retrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
      const fallbackUrl = rewriteUrlForTarget(retrySource, fallbackBaseUrl);
      const fallbackInput = buildRewrittenInput(retrySource, fallbackUrl);
      return await fetchWithTimeoutVia(nativeFetch, fallbackInput, init, requestTimeoutMs);
    }

    if (
      failoverConfigured &&
      initialHost === "fallback" &&
      isAvailabilityError(error)
    ) {
      const rescued = await tryPrimaryRescueAfterFallbackFailure(
        nativeFetch,
        requestSource,
        init,
        error instanceof Error ? error.message : "fallback_transport_failure"
      );
      if (rescued) {
        return rescued;
      }
    }

    throw error;
  }
}

async function tryPrimaryRescueAfterFallbackFailure(
  nativeFetch: typeof window.fetch,
  requestSource: RequestInfo | URL,
  init: RequestInit | undefined,
  reason: string
): Promise<Response | null> {
  debugLog("Fallback API failed while active; probing primary immediately", { reason });
  markPrimaryProbeAttempt();

  const primaryHealthy = await probePrimaryAvailability(nativeFetch);
  if (!primaryHealthy) {
    keepFallbackActiveAfterPrimaryProbeFailure(reason);
    return null;
  }

  const retrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
  const primaryUrl = rewriteUrlForTarget(retrySource, primaryBaseUrl);
  const primaryInput = buildRewrittenInput(retrySource, primaryUrl);

  try {
    const response = await fetchWithTimeoutVia(nativeFetch, primaryInput, init, requestTimeoutMs);
    if (isAvailabilityHttpStatus(response.status)) {
      keepFallbackActiveAfterPrimaryProbeFailure(`rescue_status_${response.status}`);
      return null;
    }

    markPrimaryRecovered();
    return response;
  } catch (error) {
    if (isAvailabilityError(error)) {
      keepFallbackActiveAfterPrimaryProbeFailure(
        error instanceof Error ? error.message : "primary_rescue_transport_failure"
      );
      return null;
    }

    markPrimaryRecovered();
    throw error;
  }
}

async function probePrimaryAvailability(nativeFetch: typeof window.fetch): Promise<boolean> {
  const probeUrl = `${primaryBaseUrl}${healthProbePath}`;

  try {
    const response = await fetchWithTimeoutVia(nativeFetch, probeUrl, { method: "GET", cache: "no-store" }, probeTimeoutMs);
    if (isAvailabilityHttpStatus(response.status)) {
      return false;
    }

    return response.status < 500;
  } catch {
    return false;
  }
}

async function maybeRecoverPrimary(
  nativeFetch: typeof window.fetch,
  forceProbe = false
): Promise<void> {
  if (!forceProbe && !shouldAttemptPrimaryRecovery()) {
    return;
  }

  if (primaryProbeInFlight) {
    await primaryProbeInFlight;
    return;
  }

  markPrimaryProbeAttempt();

  primaryProbeInFlight = (async () => {
    debugLog("Attempting primary API recovery probe", { probePath: healthProbePath });
    const healthy = await probePrimaryAvailability(nativeFetch);
    if (healthy) {
      markPrimaryRecovered();
    } else {
      keepFallbackActiveAfterPrimaryProbeFailure("scheduled_recovery_probe_failed");
    }
    return healthy;
  })();

  try {
    await primaryProbeInFlight;
  } finally {
    primaryProbeInFlight = null;
  }
}

function installGlobalFlag(nativeFetch: typeof window.fetch): boolean {
  const existing = (window as Window & { __trendplusFailoverInstalled?: boolean }).__trendplusFailoverInstalled;
  if (existing) {
    return false;
  }

  (window as Window & { __trendplusFailoverInstalled?: boolean }).__trendplusFailoverInstalled = true;
  (window as Window & { __trendplusNativeFetch?: typeof window.fetch }).__trendplusNativeFetch = nativeFetch;
  return true;
}

export function installApiFailoverFetchLayer(): void {
  if (typeof window === "undefined") return;
  warnIfFailoverIsMisconfigured();
  if (!failoverConfigured) return;

  const nativeFetch = window.fetch.bind(window);
  if (!installGlobalFlag(nativeFetch)) {
    return;
  }

  debugLog("API failover layer enabled", {
    primaryBaseUrl,
    fallbackBaseUrl,
    requestTimeoutMs,
    probeTimeoutMs,
    primaryRetryCooldownMs,
  });

  // If fallback state was persisted from a previous session, run a single
  // startup probe so we can quickly return to primary when it is healthy again.
  if (runtimeState.activeHost === "fallback") {
    void maybeRecoverPrimary(nativeFetch, true);
  }

  window.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    if (!shouldManageRequest(input)) {
      return nativeFetch(input, init);
    }

    if (runtimeState.activeHost === "fallback") {
      await maybeRecoverPrimary(nativeFetch);
    }

    return executeApiRequest(nativeFetch, input, init);
  }) as typeof window.fetch;
}

export function getActiveApiBaseUrl(): string {
  return resolveTargetBaseUrl();
}

export function getConfiguredPrimaryApiBaseUrl(): string {
  return primaryBaseUrl;
}

export function getConfiguredFallbackApiBaseUrl(): string {
  return fallbackBaseUrl;
}

export function getApiFailoverHostChangeEventName(): string {
  return HOST_CHANGED_EVENT;
}
