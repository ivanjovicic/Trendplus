import { afterEach, describe, expect, it, vi } from "vitest";
import { FetchTimeoutError, fetchWithTimeout } from "../fetchWithTimeout";

describe("fetchWithTimeout", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("aborts the underlying request when the timeout elapses", async () => {
    vi.useFakeTimers();

    const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      const signal = init?.signal;
      return new Promise<Response>((_, reject) => {
        signal?.addEventListener(
          "abort",
          () => {
            reject(signal.reason ?? new DOMException("Request aborted", "AbortError"));
          },
          { once: true },
        );
      });
    });

    vi.stubGlobal("fetch", fetchMock);
    Object.defineProperty(window, "fetch", {
      configurable: true,
      writable: true,
      value: fetchMock,
    });

    const request = fetchWithTimeout("/api/slow", undefined, 1_000);
    const assertion = expect(request).rejects.toBeInstanceOf(FetchTimeoutError);
    await vi.advanceTimersByTimeAsync(1_000);
    await assertion;
    expect(fetchMock).toHaveBeenCalledTimes(1);

    const signal = (fetchMock.mock.calls[0]?.[1] as RequestInit | undefined)?.signal;
    expect(signal).toBeDefined();
    expect((signal as AbortSignal).aborted).toBe(true);
    expect((signal as AbortSignal).reason).toBeInstanceOf(FetchTimeoutError);
  });

  it("surfaces external aborts without turning them into timeouts", async () => {
    const controller = new AbortController();
    const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      const signal = init?.signal;
      return new Promise<Response>((_, reject) => {
        signal?.addEventListener(
          "abort",
          () => {
            reject(signal.reason ?? new DOMException("Request aborted", "AbortError"));
          },
          { once: true },
        );
      });
    });

    vi.stubGlobal("fetch", fetchMock);
    Object.defineProperty(window, "fetch", {
      configurable: true,
      writable: true,
      value: fetchMock,
    });

    const request = fetchWithTimeout("/api/slow", { signal: controller.signal }, 1_000);
    controller.abort(new DOMException("External abort", "AbortError"));

    await expect(request).rejects.toBeInstanceOf(DOMException);
    await expect(request).rejects.toHaveProperty("name", "AbortError");
    await expect(request).rejects.toHaveProperty("message", "External abort");
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
