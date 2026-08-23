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
  const baseHref = typeof window !== "undefined" ? window.location.href : undefined;
  const normalizedInput = typeof input === "string"
    ? normalizeInput(input, baseHref)
    : input instanceof URL
      ? normalizeInput(input.toString(), baseHref)
      : input;

  if (externalSignal?.aborted) {
    throw normalizeAbortReason(externalSignal.reason);
  }

  const controller = new AbortController();
  let timeoutHandle: ReturnType<typeof globalThis.setTimeout> | undefined;
  const abortListener = externalSignal
    ? () => controller.abort(normalizeAbortReason(externalSignal.reason))
    : null;

  if (externalSignal && abortListener) {
    externalSignal.addEventListener("abort", abortListener, { once: true });
  }

  timeoutHandle = globalThis.setTimeout(() => {
    controller.abort(new FetchTimeoutError(timeoutMs));
  }, timeoutMs);

  try {
    return await fetchImpl(normalizedInput, {
      ...requestInit,
      signal: controller.signal,
    });
  } catch (error) {
    if (controller.signal.aborted) {
      throw normalizeAbortReason(controller.signal.reason);
    }

    throw error;
  } finally {
    if (timeoutHandle != null) {
      globalThis.clearTimeout(timeoutHandle);
    }
    if (externalSignal && abortListener) {
      externalSignal.removeEventListener("abort", abortListener);
    }
  }
}

function normalizeInput(value: string, baseHref: string | undefined): string {
  if (!baseHref) {
    return value;
  }

  try {
    return new URL(value, baseHref).toString();
  } catch {
    return value;
  }
}

function normalizeAbortReason(reason: unknown): Error {
  if (reason instanceof Error || reason instanceof DOMException) {
    return reason;
  }

  if (
    reason &&
    typeof reason === "object" &&
    "message" in reason &&
    typeof (reason as { message?: unknown }).message === "string"
  ) {
    return new DOMException(String((reason as { message: string }).message), "AbortError");
  }

  return new DOMException("Request aborted", "AbortError");
}
