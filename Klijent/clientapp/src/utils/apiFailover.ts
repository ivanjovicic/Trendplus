type ApiHostRole = "primary" | "fallback";
type BackendProvider = "render" | "fly";

export type BackendRoutingPreference = {
  primaryProvider: BackendProvider;
  fallbackEnabled: boolean;
  fallbackProvider: BackendProvider;
};

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

export type BackendProviderStatePhase =
  | "unknown"
  | "checking_primary"
  | "primary_warming"
  | "primary_ready"
  | "primary_failed"
  | "checking_fallback"
  | "fallback_ready"
  | "fallback_failed"
  | "degraded";

export type BackendProviderStateSnapshot = {
  phase: BackendProviderStatePhase;
  activeHost: ApiHostRole;
  reason: string | null;
  updatedAt: number;
  retryAfterMs: number | null;
};

export const API_FAILOVER_TIMEOUT_MS_OPTION = "trendplusTimeoutMs" as const;

export type ApiFailoverRequestInit = RequestInit & {
  [API_FAILOVER_TIMEOUT_MS_OPTION]?: number;
};

const STORAGE_KEY = "trendplus:api-failover-state:v2";
const ROUTING_STORAGE_KEY = "trendplus:backend-routing-preference:v1";
const HOST_CHANGED_EVENT = "trendplus:api-host-changed";
const PROVIDER_STATE_EVENT = "trendplus:backend-provider-state";

const explicitRenderBaseUrl = normalizeBaseUrl(import.meta.env.VITE_API_RENDER_BASE_URL);
const explicitFlyBaseUrl = normalizeBaseUrl(import.meta.env.VITE_API_FLY_BASE_URL);
const legacyPrimaryBaseUrl = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL);
const legacyFallbackBaseUrl = normalizeBaseUrl(import.meta.env.VITE_API_FALLBACK_URL);

const inferredRenderBaseUrl = inferProviderBaseUrl(
  "render",
  legacyPrimaryBaseUrl,
  legacyFallbackBaseUrl
);
const inferredFlyBaseUrl = inferProviderBaseUrl(
  "fly",
  legacyPrimaryBaseUrl,
  legacyFallbackBaseUrl
);

const renderBaseUrl =
  explicitRenderBaseUrl ||
  inferredRenderBaseUrl ||
  legacyPrimaryBaseUrl;
const flyBaseUrl =
  explicitFlyBaseUrl ||
  inferredFlyBaseUrl;

const routingPreference = loadRoutingPreference();
const primaryBaseUrl = getBaseUrlForProvider(routingPreference.primaryProvider);
const fallbackBaseUrl = routingPreference.fallbackEnabled
  ? getBaseUrlForProvider(routingPreference.fallbackProvider)
  : "";

const failoverConfigured =
  primaryBaseUrl.length > 0 &&
  fallbackBaseUrl.length > 0 &&
  primaryBaseUrl !== fallbackBaseUrl;

const primaryOrigin = toOrigin(primaryBaseUrl);
const fallbackOrigin = toOrigin(fallbackBaseUrl);

const requestTimeoutMs = readMsFromEnv("VITE_API_REQUEST_TIMEOUT_MS", import.meta.env.DEV ? 15_000 : 25_000);
const primaryRequestTimeoutMs = Math.min(
  readMsFromEnv("VITE_API_PRIMARY_REQUEST_TIMEOUT_MS", import.meta.env.DEV ? 12_000 : 15_000),
  requestTimeoutMs
);
const fallbackRequestTimeoutMs = readMsFromEnv("VITE_API_FALLBACK_REQUEST_TIMEOUT_MS", requestTimeoutMs);
const probeTimeoutMs = readMsFromEnv("VITE_API_FALLBACK_PROBE_TIMEOUT_MS", import.meta.env.DEV ? 6_000 : 8_000);
const primaryRetryCooldownMs = readMsFromEnv("VITE_API_PRIMARY_RETRY_COOLDOWN_MS", import.meta.env.DEV ? 30_000 : 45_000);
const stateTtlMs = readMsFromEnv("VITE_API_FAILOVER_STATE_TTL_MS", import.meta.env.DEV ? 5 * 60_000 : 5 * 60_000);
const primaryWarmupWindowMs = readMsFromEnv(
  "VITE_API_PRIMARY_WARMUP_MAX_MS",
  import.meta.env.DEV ? 45_000 : 90_000
);
const primaryWarmupInitialBackoffMs = readMsFromEnv(
  "VITE_API_PRIMARY_WARMUP_INITIAL_BACKOFF_MS",
  import.meta.env.DEV ? 1_500 : 2_500
);
const primaryWarmupMaxBackoffMs = readMsFromEnv(
  "VITE_API_PRIMARY_WARMUP_MAX_BACKOFF_MS",
  import.meta.env.DEV ? 6_000 : 10_000
);
const primaryWarmupMinBackoffMs = readMsFromEnv(
  "VITE_API_PRIMARY_WARMUP_MIN_BACKOFF_MS",
  import.meta.env.DEV ? 100 : 500
);
const recoveryProbePath = normalizePath(
  import.meta.env.VITE_API_RECOVERY_PROBE_PATH ||
  import.meta.env.VITE_API_HEALTH_PATH ||
  "/ready"
);

const apiPathPrefixes = ["/api", "/health", "/ready", "/artikli", "/scrapers", "/admin"];
const debugEnabled = import.meta.env.DEV || import.meta.env.VITE_API_FALLBACK_DEBUG === "1";
const primaryMissing = primaryBaseUrl.length === 0;
const fallbackMatchesPrimary =
  fallbackBaseUrl.length > 0 &&
  primaryBaseUrl.length > 0 &&
  fallbackBaseUrl === primaryBaseUrl;

let runtimeState: RuntimeFailoverState = loadInitialState();
let primaryProbeInFlight: Promise<boolean> | null = null;
let primaryWarmupInFlight: Promise<boolean> | null = null;
let primaryWarmupStartedAt: number | null = null;
let misconfigurationWarned = false;
let providerState: BackendProviderStateSnapshot = {
  phase: "unknown",
  activeHost: runtimeState.activeHost,
  reason: null,
  updatedAt: nowMs(),
  retryAfterMs: null,
};

function normalizeBaseUrl(raw: string | undefined): string {
  return (raw ?? "").trim().replace(/\/+$/, "");
}

function inferProviderBaseUrl(
  provider: BackendProvider,
  legacyPrimary: string,
  legacyFallback: string
): string {
  const providerPattern =
    provider === "render"
      ? /onrender\.com/i
      : /(fly\.dev|fly\.io)/i;

  if (providerPattern.test(legacyPrimary)) return legacyPrimary;
  if (providerPattern.test(legacyFallback)) return legacyFallback;
  return "";
}

function getBaseUrlForProvider(provider: BackendProvider): string {
  return provider === "render" ? renderBaseUrl : flyBaseUrl;
}

function normalizeProvider(value: unknown): BackendProvider {
  return String(value).toLowerCase() === "fly" ? "fly" : "render";
}

function getDefaultRoutingPreference(): BackendRoutingPreference {
  const primaryProvider = normalizeProvider(import.meta.env.VITE_DEFAULT_BACKEND_PROVIDER);
  const configuredFallbackProvider = normalizeProvider(import.meta.env.VITE_FALLBACK_BACKEND_PROVIDER);
  const fallbackProvider =
    configuredFallbackProvider === primaryProvider
      ? primaryProvider === "render" ? "fly" : "render"
      : configuredFallbackProvider;

  return {
    primaryProvider,
    fallbackEnabled: readBooleanFromEnv("VITE_ENABLE_BACKEND_FALLBACK", false),
    fallbackProvider,
  };
}

function loadRoutingPreference(): BackendRoutingPreference {
  if (typeof window === "undefined") {
    return getDefaultRoutingPreference();
  }

  try {
    const raw = window.localStorage.getItem(ROUTING_STORAGE_KEY);
    if (!raw) return getDefaultRoutingPreference();
    const parsed = JSON.parse(raw) as Partial<BackendRoutingPreference>;

    const primaryProvider = normalizeProvider(parsed.primaryProvider);
    const fallbackProvider = normalizeProvider(parsed.fallbackProvider);
    const fallbackEnabled =
      typeof parsed.fallbackEnabled === "boolean"
        ? parsed.fallbackEnabled
        : getDefaultRoutingPreference().fallbackEnabled;

    if (fallbackEnabled && fallbackProvider === primaryProvider) {
      return {
        primaryProvider,
        fallbackEnabled,
        fallbackProvider: primaryProvider === "render" ? "fly" : "render",
      };
    }

    return {
      primaryProvider,
      fallbackEnabled,
      fallbackProvider,
    };
  } catch {
    return getDefaultRoutingPreference();
  }
}

export function getBackendRoutingPreference(): BackendRoutingPreference {
  return { ...routingPreference };
}

export function saveBackendRoutingPreference(next: BackendRoutingPreference): void {
  if (typeof window === "undefined") return;
  const sanitized: BackendRoutingPreference = {
    primaryProvider: normalizeProvider(next.primaryProvider),
    fallbackEnabled: Boolean(next.fallbackEnabled),
    fallbackProvider: normalizeProvider(next.fallbackProvider),
  };

  if (sanitized.fallbackEnabled && sanitized.primaryProvider === sanitized.fallbackProvider) {
    sanitized.fallbackProvider = sanitized.primaryProvider === "render" ? "fly" : "render";
  }

  window.localStorage.setItem(ROUTING_STORAGE_KEY, JSON.stringify(sanitized));
}

function normalizePath(raw: string): string {
  return raw.startsWith("/") ? raw : `/${raw}`;
}

function readMsFromEnv(name: string, fallback: number): number {
  const parsed = Number(import.meta.env[name]);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function readBooleanFromEnv(name: string, fallback: boolean): boolean {
  const raw = import.meta.env[name];
  if (raw == null || raw === "") return fallback;

  return ["1", "true", "yes", "on"].includes(String(raw).trim().toLowerCase());
}

function readPositiveMs(value: unknown): number | null {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function getRequestTimeoutMs(init: ApiFailoverRequestInit | undefined, host: ApiHostRole): number {
  const requestedTimeoutMs = readPositiveMs(init?.[API_FAILOVER_TIMEOUT_MS_OPTION]);
  if (host === "primary") {
    return Math.min(requestedTimeoutMs ?? primaryRequestTimeoutMs, primaryRequestTimeoutMs);
  }

  return requestedTimeoutMs ?? fallbackRequestTimeoutMs;
}

function stripFailoverOptions(init: ApiFailoverRequestInit | undefined): RequestInit | undefined {
  if (!init || !(API_FAILOVER_TIMEOUT_MS_OPTION in init)) return init;

  const { [API_FAILOVER_TIMEOUT_MS_OPTION]: _timeoutMs, ...nativeInit } = init;
  return nativeInit;
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

function isProviderHealthPath(pathname: string): boolean {
  return pathname === "/ready" || pathname === "/health" || pathname.startsWith("/health/");
}

function shouldManageRequest(input: RequestInfo | URL): boolean {
  const rawUrl = toRawUrl(input);
  const parsed = toAbsoluteUrl(rawUrl);
  if (!parsed) return false;

  if (isProviderHealthPath(parsed.pathname.toLowerCase())) {
    return false;
  }

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

function setProviderState(
  phase: BackendProviderStatePhase,
  reason: string | null,
  retryAfterMs: number | null = null
): void {
  providerState = {
    phase,
    activeHost: runtimeState.activeHost,
    reason,
    updatedAt: nowMs(),
    retryAfterMs,
  };

  if (typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(
    new CustomEvent(PROVIDER_STATE_EVENT, {
      detail: providerState,
    })
  );
}

function markActiveHostReady(reason: string): void {
  const readyPhase: BackendProviderStatePhase =
    runtimeState.activeHost === "fallback" ? "fallback_ready" : "primary_ready";

  if (
    providerState.phase === readyPhase &&
    providerState.activeHost === runtimeState.activeHost &&
    providerState.reason === reason
  ) {
    return;
  }

  setProviderState(readyPhase, reason);
}

function debugLog(message: string, extra?: Record<string, unknown>): void {
  if (!debugEnabled) return;
  if (extra) {
    console.info(`[api-failover] ${message}`, extra);
    return;
  }
  console.info(`[api-failover] ${message}`);
}

function describeUrlForTelemetry(rawUrl: string): string {
  const parsed = toAbsoluteUrl(rawUrl);
  if (!parsed) return "<unparseable>";
  return `${parsed.origin}${parsed.pathname}`;
}

function describeError(error: unknown): string {
  if (error instanceof ApiFailoverTimeoutError) {
    return `timeout_${error.timeoutMs}ms`;
  }

  if (error instanceof DOMException) {
    return `${error.name}:${error.message}`;
  }

  if (error instanceof Error) {
    return `${error.name}:${error.message}`;
  }

  return String(error);
}

function elapsedMsSince(startedAt: number): number {
  return Math.round(performance.now() - startedAt);
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(value, max));
}

function parseRetryAfterMs(value: string | null): number | null {
  if (!value) return null;

  const trimmed = value.trim();
  const seconds = Number(trimmed);
  if (Number.isFinite(seconds) && seconds >= 0) {
    return clamp(Math.round(seconds * 1_000), primaryWarmupMinBackoffMs, primaryWarmupMaxBackoffMs);
  }

  const parsedDate = Date.parse(trimmed);
  if (!Number.isFinite(parsedDate)) return null;

  const deltaMs = parsedDate - Date.now();
  if (deltaMs <= 0) return primaryWarmupMinBackoffMs;
  return clamp(Math.round(deltaMs), primaryWarmupMinBackoffMs, primaryWarmupMaxBackoffMs);
}

async function waitWithOptionalAbort(ms: number, signal?: AbortSignal): Promise<void> {
  if (ms <= 0) return;

  if (signal?.aborted) {
    throw new DOMException("Request aborted", "AbortError");
  }

  await new Promise<void>((resolve, reject) => {
    const id = window.setTimeout(() => {
      signal?.removeEventListener("abort", onAbort);
      resolve();
    }, ms);

    const onAbort = () => {
      window.clearTimeout(id);
      signal?.removeEventListener("abort", onAbort);
      reject(new DOMException("Request aborted", "AbortError"));
    };

    signal?.addEventListener("abort", onAbort, { once: true });
  });
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
  primaryWarmupStartedAt = null;
  primaryWarmupInFlight = null;
  runtimeState = {
    ...runtimeState,
    activeHost: "fallback",
    lastPrimaryFailureAt: nowMs(),
  };
  saveState();
  emitHostChanged("fallback");
  setProviderState("primary_failed", reason);
  debugLog("Primary API marked unavailable; fallback activated", {
    reason,
    hostChanged: "primary->fallback",
    activeHost: "fallback",
  });
}

function markPrimaryProbeAttempt(): void {
  runtimeState = {
    ...runtimeState,
    lastPrimaryProbeAt: nowMs(),
  };
  saveState();
}

function markPrimaryRecovered(): void {
  primaryWarmupStartedAt = null;
  primaryWarmupInFlight = null;
  runtimeState = {
    activeHost: "primary",
    lastPrimaryFailureAt: null,
    lastPrimaryProbeAt: nowMs(),
  };
  saveState();
  emitHostChanged("primary");
  setProviderState("primary_ready", "primary_recovered");
  debugLog("Primary API recovered; returning traffic to primary", {
    hostChanged: "fallback->primary",
    activeHost: "primary",
  });
}

function keepFallbackActiveAfterPrimaryProbeFailure(reason: string): void {
  runtimeState = {
    ...runtimeState,
    activeHost: "fallback",
    lastPrimaryFailureAt: nowMs(),
  };
  saveState();
  setProviderState("fallback_ready", reason);
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

function isWarmingUpHttpStatus(status: number): boolean {
  return status === 503 || status === 504;
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}

function isExternalAbortError(error: unknown, init: RequestInit | undefined): boolean {
  if (!isAbortError(error)) return false;
  return Boolean(init?.signal?.aborted);
}

function isAvailabilityError(error: unknown): boolean {
  if (error instanceof ApiFailoverTimeoutError) return true;
  if (error instanceof TypeError) return true;
  if (error instanceof DOMException && error.name === "TimeoutError") return true;

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

type ProviderProbeResult = {
  healthy: boolean;
  warmingUp: boolean;
  degraded: boolean;
  statusCode: number | null;
  retryAfterMs: number | null;
  latencyMs: number;
  failureReason: string | null;
};

async function probeProviderReadiness(
  nativeFetch: typeof window.fetch,
  baseUrl: string,
  signal: AbortSignal | undefined,
  timeoutMs: number
): Promise<ProviderProbeResult> {
  const probeUrl = `${baseUrl}${recoveryProbePath}`;
  const startedAt = performance.now();

  try {
    const response = await fetchWithTimeoutVia(
      nativeFetch,
      probeUrl,
      { method: "GET", cache: "no-store", signal },
      timeoutMs
    );

    const retryAfterMs = parseRetryAfterMs(response.headers.get("Retry-After"));
    const latencyMs = elapsedMsSince(startedAt);
    const healthy = response.ok;
    const warmingUp = response.status === 503;
    const degraded = response.status >= 500 && response.status !== 503;

    debugLog("Provider readiness probe completed", {
      recoveryProbeEndpoint: recoveryProbePath,
      probeTarget: describeUrlForTelemetry(probeUrl),
      status: response.status,
      latencyMs,
      healthy,
      warmingUp,
      retryAfterMs,
    });

    return {
      healthy,
      warmingUp,
      degraded,
      statusCode: response.status,
      retryAfterMs,
      latencyMs,
      failureReason: healthy ? null : `status_${response.status}`,
    };
  } catch (error) {
    const latencyMs = elapsedMsSince(startedAt);
    const isAbort = isAbortError(error);
    if (isAbort && signal?.aborted) {
      throw error;
    }

    const failureReason = describeError(error);
    debugLog("Provider readiness probe failed", {
      recoveryProbeEndpoint: recoveryProbePath,
      probeTarget: describeUrlForTelemetry(probeUrl),
      latencyMs,
      failureReason,
    });

    return {
      healthy: false,
      warmingUp: false,
      degraded: true,
      statusCode: null,
      retryAfterMs: null,
      latencyMs,
      failureReason,
    };
  }
}

async function waitForPrimaryWarmup(
  nativeFetch: typeof window.fetch,
  signal: AbortSignal | undefined,
  triggerReason: string
): Promise<boolean> {
  if (primaryWarmupInFlight) {
    return primaryWarmupInFlight;
  }

  primaryWarmupStartedAt ??= nowMs();

  primaryWarmupInFlight = (async () => {
    let attempt = 0;
    let backoffMs = primaryWarmupInitialBackoffMs;

    setProviderState("checking_primary", triggerReason);

    while (true) {
      attempt += 1;
      const elapsed = nowMs() - (primaryWarmupStartedAt ?? nowMs());

      if (elapsed >= primaryWarmupWindowMs) {
        setProviderState("primary_failed", "warmup_window_exhausted");
        return false;
      }

      const probe = await probeProviderReadiness(nativeFetch, primaryBaseUrl, signal, probeTimeoutMs);
      if (probe.healthy) {
        setProviderState("primary_ready", "primary_ready");
        markPrimaryRecovered();
        return true;
      }

      if (!probe.warmingUp && !probe.degraded) {
        setProviderState("primary_failed", probe.failureReason ?? "primary_probe_failed");
        return false;
      }

      const waitMs = clamp(
        probe.retryAfterMs ?? Math.round(backoffMs * (0.9 + Math.random() * 0.2)),
        primaryWarmupMinBackoffMs,
        primaryWarmupMaxBackoffMs
      );

      setProviderState("primary_warming", probe.failureReason ?? "primary_warming", waitMs);
      debugLog("Primary warmup retry scheduled", {
        attempt,
        waitMs,
        retryAfterMs: probe.retryAfterMs,
        elapsedMs: elapsed,
        maxWarmupWindowMs: primaryWarmupWindowMs,
      });

      await waitWithOptionalAbort(waitMs, signal);
      backoffMs = Math.min(Math.round(backoffMs * 1.8), primaryWarmupMaxBackoffMs);
    }
  })();

  try {
    return await primaryWarmupInFlight;
  } finally {
    primaryWarmupInFlight = null;
  }
}

async function verifyFallbackBeforeSwitch(
  nativeFetch: typeof window.fetch,
  reason: string,
  signal: AbortSignal | undefined
): Promise<boolean> {
  if (!failoverConfigured) return false;

  setProviderState("checking_fallback", reason);
  const probe = await probeProviderReadiness(nativeFetch, fallbackBaseUrl, signal, probeTimeoutMs);
  if (probe.healthy) {
    setProviderState("fallback_ready", reason);
    return true;
  }

  setProviderState("fallback_failed", probe.failureReason ?? reason);
  return false;
}

async function executeApiRequest(
  nativeFetch: typeof window.fetch,
  input: RequestInfo | URL,
  init: ApiFailoverRequestInit | undefined
): Promise<Response> {
  const requestSource: RequestInfo | URL = input instanceof Request ? input.clone() : input;
  const targetBase = resolveTargetBaseUrl();
  const targetUrl = rewriteUrlForTarget(requestSource, targetBase);
  const targetInput = buildRewrittenInput(requestSource, targetUrl);
  const initialHost = runtimeState.activeHost;
  const initialTimeoutMs = getRequestTimeoutMs(init, initialHost);
  const primaryTimeoutMs = getRequestTimeoutMs(init, "primary");
  const fallbackTimeoutMs = getRequestTimeoutMs(init, "fallback");
  const nativeInit = stripFailoverOptions(init);
  const requestStartedAt = performance.now();
  const requestedUrl = describeUrlForTelemetry(toRawUrl(requestSource));
  const activeTargetUrl = describeUrlForTelemetry(targetUrl);

  debugLog("Request started", {
    originalTarget: requestedUrl,
    activeTarget: activeTargetUrl,
    activeHost: initialHost,
    timeoutMs: initialTimeoutMs,
  });

  try {
    const response = await fetchWithTimeoutVia(nativeFetch, targetInput, nativeInit, initialTimeoutMs);
    const elapsedMs = elapsedMsSince(requestStartedAt);

    if (
      failoverConfigured &&
      initialHost === "primary" &&
      isAvailabilityHttpStatus(response.status)
    ) {
      const failoverReason = `status_${response.status}`;
      const shouldWarmup = isWarmingUpHttpStatus(response.status);

      if (shouldWarmup) {
        const warmed = await waitForPrimaryWarmup(nativeFetch, nativeInit?.signal ?? undefined, failoverReason);
        if (warmed) {
          const primaryRetrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
          const primaryRetryUrl = rewriteUrlForTarget(primaryRetrySource, primaryBaseUrl);
          const primaryRetryInput = buildRewrittenInput(primaryRetrySource, primaryRetryUrl);
          const retryStartedAt = performance.now();
          const retryResponse = await fetchWithTimeoutVia(
            nativeFetch,
            primaryRetryInput,
            nativeInit,
            primaryTimeoutMs
          );
          debugLog("Primary retry completed after warmup", {
            originalTarget: requestedUrl,
            activeTarget: describeUrlForTelemetry(primaryRetryUrl),
            elapsedMs: elapsedMsSince(retryStartedAt),
            totalElapsedMs: elapsedMsSince(requestStartedAt),
            status: retryResponse.status,
          });
          return retryResponse;
        }
      }

      const fallbackReady = await verifyFallbackBeforeSwitch(
        nativeFetch,
        failoverReason,
        nativeInit?.signal ?? undefined
      );
      if (!fallbackReady) {
        setProviderState("degraded", "fallback_unavailable_after_primary_failure");
        return response;
      }

      debugLog("Request triggered failover after primary response", {
        originalTarget: requestedUrl,
        activeTarget: activeTargetUrl,
        elapsedMs,
        status: response.status,
        failoverReason,
        hostChanged: "primary->fallback",
      });
      markPrimaryFailure(failoverReason);
      const retrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
      const fallbackUrl = rewriteUrlForTarget(retrySource, fallbackBaseUrl);
      const fallbackInput = buildRewrittenInput(retrySource, fallbackUrl);
      const fallbackStartedAt = performance.now();
      const fallbackResponse = await fetchWithTimeoutVia(nativeFetch, fallbackInput, nativeInit, fallbackTimeoutMs);
      if (fallbackResponse.status < 500) {
        markActiveHostReady("fallback_request_succeeded");
      }
      debugLog("Fallback retry completed", {
        originalTarget: requestedUrl,
        activeTarget: describeUrlForTelemetry(fallbackUrl),
        elapsedMs: elapsedMsSince(fallbackStartedAt),
        totalElapsedMs: elapsedMsSince(requestStartedAt),
        status: fallbackResponse.status,
        activeHost: "fallback",
      });
      return fallbackResponse;
    }

    if (
      failoverConfigured &&
      initialHost === "fallback" &&
      isAvailabilityHttpStatus(response.status)
    ) {
      const rescued = await tryPrimaryRescueAfterFallbackFailure(
        nativeFetch,
        requestSource,
        nativeInit,
        primaryTimeoutMs,
        `status_${response.status}`
      );
      if (rescued) {
        debugLog("Fallback response rescued by primary", {
          originalTarget: requestedUrl,
          elapsedMs: elapsedMsSince(requestStartedAt),
          status: rescued.status,
          hostChanged: "fallback->primary",
        });
        return rescued;
      }
    }

    debugLog("Request completed", {
      originalTarget: requestedUrl,
      activeTarget: activeTargetUrl,
      elapsedMs,
      status: response.status,
      activeHost: initialHost,
    });
    if (response.status < 500) {
      markActiveHostReady("request_succeeded");
    }
    return response;
  } catch (error) {
    debugLog("Request failed", {
      originalTarget: requestedUrl,
      activeTarget: activeTargetUrl,
      elapsedMs: elapsedMsSince(requestStartedAt),
      activeHost: initialHost,
      abortReason: describeError(error),
    });

    if (isExternalAbortError(error, nativeInit)) {
      const abortReason = nativeInit?.signal?.reason;
      debugLog("Request abort was external; failover not triggered", {
        originalTarget: requestedUrl,
        abortReason: describeError(abortReason),
      });
      throw error;
    }

    if (
      failoverConfigured &&
      initialHost === "primary" &&
      isAvailabilityError(error)
    ) {
      const failoverReason = describeError(error);
      const warmed = await waitForPrimaryWarmup(nativeFetch, nativeInit?.signal ?? undefined, failoverReason);
      if (warmed) {
        const primaryRetrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
        const primaryRetryUrl = rewriteUrlForTarget(primaryRetrySource, primaryBaseUrl);
        const primaryRetryInput = buildRewrittenInput(primaryRetrySource, primaryRetryUrl);
        const retryStartedAt = performance.now();
        const retryResponse = await fetchWithTimeoutVia(
          nativeFetch,
          primaryRetryInput,
          nativeInit,
          primaryTimeoutMs
        );
        debugLog("Primary retry completed after transport warmup", {
          originalTarget: requestedUrl,
          activeTarget: describeUrlForTelemetry(primaryRetryUrl),
          elapsedMs: elapsedMsSince(retryStartedAt),
          totalElapsedMs: elapsedMsSince(requestStartedAt),
          status: retryResponse.status,
        });
        return retryResponse;
      }

      const fallbackReady = await verifyFallbackBeforeSwitch(
        nativeFetch,
        failoverReason,
        nativeInit?.signal ?? undefined
      );
      if (!fallbackReady) {
        setProviderState("degraded", "fallback_unavailable_after_primary_transport_failure");
        throw error;
      }

      debugLog("Request triggered failover after primary transport failure", {
        originalTarget: requestedUrl,
        activeTarget: activeTargetUrl,
        elapsedMs: elapsedMsSince(requestStartedAt),
        failoverReason,
        hostChanged: "primary->fallback",
      });
      markPrimaryFailure(failoverReason);
      const retrySource = requestSource instanceof Request ? requestSource.clone() : requestSource;
      const fallbackUrl = rewriteUrlForTarget(retrySource, fallbackBaseUrl);
      const fallbackInput = buildRewrittenInput(retrySource, fallbackUrl);
      const fallbackStartedAt = performance.now();
      const fallbackResponse = await fetchWithTimeoutVia(nativeFetch, fallbackInput, nativeInit, fallbackTimeoutMs);
      if (fallbackResponse.status < 500) {
        markActiveHostReady("fallback_request_succeeded");
      }
      debugLog("Fallback retry completed", {
        originalTarget: requestedUrl,
        activeTarget: describeUrlForTelemetry(fallbackUrl),
        elapsedMs: elapsedMsSince(fallbackStartedAt),
        totalElapsedMs: elapsedMsSince(requestStartedAt),
        status: fallbackResponse.status,
        activeHost: "fallback",
      });
      return fallbackResponse;
    }

    if (
      failoverConfigured &&
      initialHost === "fallback" &&
      isAvailabilityError(error)
    ) {
      const rescued = await tryPrimaryRescueAfterFallbackFailure(
        nativeFetch,
        requestSource,
        nativeInit,
        primaryTimeoutMs,
        error instanceof Error ? error.message : "fallback_transport_failure"
      );
      if (rescued) {
        debugLog("Fallback failure rescued by primary", {
          originalTarget: requestedUrl,
          elapsedMs: elapsedMsSince(requestStartedAt),
          status: rescued.status,
          hostChanged: "fallback->primary",
        });
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
  timeoutMs: number,
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
    const response = await fetchWithTimeoutVia(nativeFetch, primaryInput, init, timeoutMs);
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
  const probe = await probeProviderReadiness(nativeFetch, primaryBaseUrl, undefined, probeTimeoutMs);
  return probe.healthy;
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
    debugLog("Attempting primary API recovery probe", { recoveryProbeEndpoint: recoveryProbePath });
    setProviderState("checking_primary", "scheduled_recovery_probe");
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
    primaryRequestTimeoutMs,
    fallbackRequestTimeoutMs,
    probeTimeoutMs,
    recoveryProbePath,
    primaryRetryCooldownMs,
    stateTtlMs,
    primaryWarmupWindowMs,
    primaryWarmupMinBackoffMs,
    primaryWarmupInitialBackoffMs,
    primaryWarmupMaxBackoffMs,
  });
  // If fallback state was persisted, respect the cooldown before probing the
  // primary again. The recovery probe is intentionally lightweight, but the
  // cooldown still avoids flipping hosts on every page reload.
  if (runtimeState.activeHost === "fallback") {
    void maybeRecoverPrimary(nativeFetch);
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

export function getBackendProviderStateEventName(): string {
  return PROVIDER_STATE_EVENT;
}

export function getBackendProviderStateSnapshot(): BackendProviderStateSnapshot {
  return { ...providerState };
}

export function __resetApiFailoverForTests(): void {
  runtimeState = {
    activeHost: "primary",
    lastPrimaryFailureAt: null,
    lastPrimaryProbeAt: null,
  };
  primaryProbeInFlight = null;
  primaryWarmupInFlight = null;
  primaryWarmupStartedAt = null;
  providerState = {
    phase: "unknown",
    activeHost: "primary",
    reason: null,
    updatedAt: nowMs(),
    retryAfterMs: null,
  };

  if (typeof window !== "undefined") {
    (window as Window & { __trendplusFailoverInstalled?: boolean }).__trendplusFailoverInstalled = false;
    if ((window as Window & { __trendplusNativeFetch?: typeof window.fetch }).__trendplusNativeFetch) {
      window.fetch = (window as Window & { __trendplusNativeFetch?: typeof window.fetch }).__trendplusNativeFetch!;
    }
  }
}
