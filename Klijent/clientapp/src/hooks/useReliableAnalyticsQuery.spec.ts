import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useReliableAnalyticsQuery } from "./useReliableAnalyticsQuery";

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((nextResolve, nextReject) => {
    resolve = nextResolve;
    reject = nextReject;
  });
  return { promise, resolve, reject };
}

describe("useReliableAnalyticsQuery", () => {
  it("loads an initial snapshot and records its generation and timestamp", async () => {
    const query = vi.fn().mockResolvedValue({ rows: [1] });
    const { result } = renderHook(() => useReliableAnalyticsQuery({ query }));

    expect(result.current.initialLoading).toBe(true);
    await waitFor(() => expect(result.current.data).toEqual({ rows: [1] }));

    expect(result.current.initialLoading).toBe(false);
    expect(result.current.error).toBeNull();
    expect(result.current.loadedAt).toEqual(expect.any(String));
    expect(result.current.requestGeneration).toBe(1);
  });

  it("keeps an initial failure blocking", async () => {
    const query = vi.fn().mockRejectedValue(new Error("upit nije uspeo"));
    const { result } = renderHook(() => useReliableAnalyticsQuery({ query }));

    await waitFor(() => expect(result.current.error).toBe("upit nije uspeo"));
    expect(result.current.data).toBeNull();
    expect(result.current.staleWarning).toBeNull();
  });

  it("preserves a successful empty snapshot as data", async () => {
    const query = vi.fn().mockResolvedValue({ rows: [], meta: { emptyReason: "no_data_in_period" } });
    const { result } = renderHook(() => useReliableAnalyticsQuery({ query }));

    await waitFor(() => expect(result.current.data).toEqual({
      rows: [],
      meta: { emptyReason: "no_data_in_period" },
    }));
    expect(result.current.error).toBeNull();
  });

  it("refetches successfully without exposing a blocking error", async () => {
    const query = vi.fn()
      .mockResolvedValueOnce({ version: 1 })
      .mockResolvedValueOnce({ version: 2 });
    const { result } = renderHook(() => useReliableAnalyticsQuery({ query }));
    await waitFor(() => expect(result.current.data).toEqual({ version: 1 }));

    act(() => result.current.refetch());
    await waitFor(() => expect(result.current.data).toEqual({ version: 2 }));

    expect(result.current.refetching).toBe(false);
    expect(result.current.error).toBeNull();
    expect(result.current.staleWarning).toBeNull();
    expect(query).toHaveBeenCalledTimes(2);
  });

  it("retains the last snapshot and reports a stale warning on refetch failure", async () => {
    const query = vi.fn()
      .mockResolvedValueOnce({ version: 1 })
      .mockRejectedValueOnce(new Error("privremeni pad"));
    const { result } = renderHook(() => useReliableAnalyticsQuery({ query }));
    await waitFor(() => expect(result.current.data).toEqual({ version: 1 }));

    act(() => result.current.refetch());
    await waitFor(() => expect(result.current.staleWarning).toBe("privremeni pad"));

    expect(result.current.data).toEqual({ version: 1 });
    expect(result.current.error).toBeNull();
  });

  it("aborts the previous request without rendering an error", async () => {
    const first = deferred<{ version: number }>();
    const second = deferred<{ version: number }>();
    const signals: AbortSignal[] = [];
    const firstQuery = vi.fn((signal: AbortSignal) => {
      signals.push(signal);
      return first.promise;
    });
    const secondQuery = vi.fn((signal: AbortSignal) => {
      signals.push(signal);
      return second.promise;
    });
    const { result, rerender } = renderHook(
      ({ query }) => useReliableAnalyticsQuery({ query }),
      { initialProps: { query: firstQuery } },
    );

    rerender({ query: secondQuery });
    expect(signals[0].aborted).toBe(true);
    await act(async () => second.resolve({ version: 2 }));
    await waitFor(() => expect(result.current.data).toEqual({ version: 2 }));
    expect(result.current.error).toBeNull();
    first.resolve({ version: 1 });
    await Promise.resolve();
    expect(result.current.data).toEqual({ version: 2 });
  });

  it("allows only the latest response to commit during a slow-old/new-fast race", async () => {
    const oldRequest = deferred<{ version: string }>();
    const newRequest = deferred<{ version: string }>();
    const oldQuery = vi.fn(() => oldRequest.promise);
    const newQuery = vi.fn(() => newRequest.promise);
    const { result, rerender } = renderHook(
      ({ query }) => useReliableAnalyticsQuery({ query }),
      { initialProps: { query: oldQuery } },
    );

    rerender({ query: newQuery });
    await act(async () => newRequest.resolve({ version: "new" }));
    await waitFor(() => expect(result.current.data).toEqual({ version: "new" }));
    oldRequest.resolve({ version: "old" });
    await Promise.resolve();

    expect(result.current.data).toEqual({ version: "new" });
  });

  it("aborts pending work during unmount cleanup", () => {
    let signal: AbortSignal | undefined;
    const query = vi.fn((nextSignal: AbortSignal) => {
      signal = nextSignal;
      return new Promise<{ rows: number[] }>(() => undefined);
    });
    const { unmount } = renderHook(() => useReliableAnalyticsQuery({ query }));

    unmount();
    expect(signal?.aborted).toBe(true);
  });
});
