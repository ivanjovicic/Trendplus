function readTimeoutFromEnv(name: string, fallbackMs: number): number {
  const raw = import.meta.env[name];
  const parsed = Number(raw);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallbackMs;
}

export const API_COLD_START_TIMEOUT_MS = readTimeoutFromEnv(
  "VITE_API_COLD_START_TIMEOUT_MS",
  import.meta.env.DEV ? 45_000 : 75_000,
);

export const API_INITIAL_REQUEST_TIMEOUT_MS = Math.min(
  readTimeoutFromEnv(
    "VITE_API_INITIAL_REQUEST_TIMEOUT_MS",
    import.meta.env.DEV ? 10_000 : 15_000,
  ),
  API_COLD_START_TIMEOUT_MS,
);

export const API_HEALTH_TIMEOUT_MS = Math.min(
  readTimeoutFromEnv(
    "VITE_API_HEALTH_TIMEOUT_MS",
    import.meta.env.DEV ? 12_000 : 20_000,
  ),
  API_COLD_START_TIMEOUT_MS,
);

export const API_HEALTH_FAILURE_GRACE_MS = Math.max(
  readTimeoutFromEnv(
    "VITE_API_HEALTH_FAILURE_GRACE_MS",
    import.meta.env.DEV ? 30_000 : 90_000,
  ),
  API_COLD_START_TIMEOUT_MS,
);

export function getRetryTimeouts(totalTimeoutMs = API_COLD_START_TIMEOUT_MS): {
  firstAttemptTimeoutMs: number;
  totalTimeoutMs: number;
} {
  return {
    firstAttemptTimeoutMs: Math.min(API_INITIAL_REQUEST_TIMEOUT_MS, totalTimeoutMs),
    totalTimeoutMs: Math.max(totalTimeoutMs, API_INITIAL_REQUEST_TIMEOUT_MS),
  };
}