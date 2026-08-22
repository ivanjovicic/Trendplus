import { API_COLD_START_TIMEOUT_MS } from "./apiTimeouts";

export class FetchTimeoutError extends Error {
  readonly timeoutMs: number;

  constructor(timeoutMs: number) {
    super(`Request timeout after ${Math.ceil(timeoutMs / 1000)}s`);
    this.name = "TimeoutError";
    this.timeoutMs = timeoutMs;
  }
}

export async function fetchWithTimeout(
  input: RequestInfo | URL,
  init?: RequestInit,
  timeoutMs = API_COLD_START_TIMEOUT_MS
): Promise<Response> {
  const fetchImpl = typeof window !== "undefined" && typeof window.fetch === "function"
    ? window.fetch.bind(window)
    : fetch;
  const externalSignal = init?.signal;
  const { signal: _ignoredSignal, ...requestInit } = init ?? {};
  const normalizedInput = typeof input === "string"
    ? (() => {
        try {
          return new URL(input, window.location.href).toString();
        } catch {
          return input;
        }
      })()
    : input instanceof URL
      ? new URL(input.toString(), window.location.href).toString()
      : input;

  if (externalSignal?.aborted) {
    throw externalSignal.reason instanceof Error
      ? externalSignal.reason
      : new DOMException("Request aborted", "AbortError");
  }

  let timeoutId: number | undefined;
  let abortListener: (() => void) | null = null;

  const timeoutPromise = new Promise<Response>((_, reject) => {
    timeoutId = window.setTimeout(() => {
      reject(new FetchTimeoutError(timeoutMs));
    }, timeoutMs);
  });

  const abortPromise = externalSignal ? new Promise<Response>((_, reject) => {
    abortListener = () => {
      reject(externalSignal.reason instanceof Error
        ? externalSignal.reason
        : new DOMException("Request aborted", "AbortError"));
    };

    externalSignal.addEventListener("abort", abortListener, { once: true });
  }) : null;

  try {
    return await Promise.race(
      [fetchImpl(normalizedInput, requestInit), timeoutPromise, abortPromise].filter(Boolean) as Array<Promise<Response>>,
    );
  } finally {
    if (timeoutId != null) {
      window.clearTimeout(timeoutId);
    }
    if (externalSignal && abortListener) {
      externalSignal.removeEventListener("abort", abortListener);
    }
  }
}
