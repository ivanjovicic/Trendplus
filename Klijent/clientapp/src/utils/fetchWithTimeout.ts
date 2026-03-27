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
  timeoutMs = 15_000
): Promise<Response> {
  const controller = new AbortController();
  const externalSignal = init?.signal;
  let didTimeout = false;
  let wasExternallyAborted = false;

  const timeoutId = window.setTimeout(() => {
    didTimeout = true;
    controller.abort(new DOMException("Request timed out", "TimeoutError"));
  }, timeoutMs);

  const abortFromExternalSignal = () => {
    wasExternallyAborted = true;
    controller.abort(externalSignal?.reason);
  };

  if (externalSignal) {
    if (externalSignal.aborted) {
      wasExternallyAborted = true;
      controller.abort();
    } else {
      externalSignal.addEventListener("abort", abortFromExternalSignal, { once: true });
    }
  }

  try {
    return await fetch(input, {
      ...init,
      signal: controller.signal,
    });
  } catch (error) {
    if (didTimeout) {
      throw new FetchTimeoutError(timeoutMs);
    }

    if (wasExternallyAborted && error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    throw error;
  } finally {
    window.clearTimeout(timeoutId);
    externalSignal?.removeEventListener("abort", abortFromExternalSignal);
  }
}
