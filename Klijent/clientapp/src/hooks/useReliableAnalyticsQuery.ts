import { useCallback, useEffect, useRef, useState } from "react";

export type ReliableAnalyticsQuery<T> = (signal: AbortSignal) => Promise<T>;

export type UseReliableAnalyticsQueryOptions<T> = {
  query: ReliableAnalyticsQuery<T>;
  enabled?: boolean;
  initialData?: T | null;
  getErrorMessage?: (reason: unknown) => string;
};

export type UseReliableAnalyticsQueryResult<T> = {
  data: T | null;
  initialLoading: boolean;
  refetching: boolean;
  error: string | null;
  errorReason: unknown | null;
  staleWarning: string | null;
  staleReason: unknown | null;
  loadedAt: string | null;
  requestGeneration: number;
  refetch: () => void;
};

function isAbortError(reason: unknown): boolean {
  return reason instanceof DOMException && reason.name === "AbortError"
    || (typeof reason === "object"
      && reason !== null
      && "name" in reason
      && reason.name === "AbortError");
}

function defaultErrorMessage(reason: unknown): string {
  return reason instanceof Error ? reason.message : "Analitički podaci trenutno nisu dostupni.";
}

/**
 * Owns the request lifecycle shared by analytics list pages.
 *
 * A failed initial request is blocking. A failed refetch retains the last
 * successful snapshot and reports a stale warning instead of fabricating an
 * empty result. Query functions receive the current abort signal and only the
 * newest generation may commit state.
 */
export function useReliableAnalyticsQuery<T>(
  options: UseReliableAnalyticsQueryOptions<T>,
): UseReliableAnalyticsQueryResult<T> {
  const {
    query,
    enabled = true,
    initialData = null,
    getErrorMessage = defaultErrorMessage,
  } = options;
  const [data, setData] = useState<T | null>(initialData);
  const [initialLoading, setInitialLoading] = useState(enabled && initialData == null);
  const [refetching, setRefetching] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorReason, setErrorReason] = useState<unknown | null>(null);
  const [staleWarning, setStaleWarning] = useState<string | null>(null);
  const [staleReason, setStaleReason] = useState<unknown | null>(null);
  const [loadedAt, setLoadedAt] = useState<string | null>(initialData == null ? null : new Date().toISOString());
  const [reloadVersion, setReloadVersion] = useState(0);
  const [requestGeneration, setRequestGeneration] = useState(0);
  const dataRef = useRef<T | null>(initialData);
  const generationRef = useRef(0);
  const mountedRef = useRef(true);
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      generationRef.current += 1;
      controllerRef.current?.abort();
    };
  }, []);

  useEffect(() => {
    if (!enabled) {
      controllerRef.current?.abort();
      setInitialLoading(false);
      setRefetching(false);
      return;
    }

    const controller = new AbortController();
    controllerRef.current?.abort();
    controllerRef.current = controller;
    const generation = generationRef.current + 1;
    generationRef.current = generation;
    setRequestGeneration(generation);
    setError(null);
    setErrorReason(null);
    setStaleWarning(null);
    setStaleReason(null);
    if (dataRef.current == null) {
      setInitialLoading(true);
    } else {
      setRefetching(true);
    }

    void query(controller.signal)
      .then((result) => {
        if (!mountedRef.current || generation !== generationRef.current) return;
        dataRef.current = result;
        setData(result);
        setLoadedAt(new Date().toISOString());
        setError(null);
        setErrorReason(null);
        setStaleWarning(null);
        setStaleReason(null);
      })
      .catch((reason: unknown) => {
        if (!mountedRef.current || generation !== generationRef.current || isAbortError(reason)) return;
        const message = getErrorMessage(reason);
        if (dataRef.current == null) {
          setError(message);
          setErrorReason(reason);
        } else {
          setStaleWarning(message);
          setStaleReason(reason);
        }
      })
      .finally(() => {
        if (!mountedRef.current || generation !== generationRef.current) return;
        setInitialLoading(false);
        setRefetching(false);
      });

    return () => {
      controller.abort();
    };
  }, [enabled, getErrorMessage, query, reloadVersion]);

  const refetch = useCallback(() => {
    setReloadVersion((current) => current + 1);
  }, []);

  return {
    data,
    initialLoading,
    refetching,
    error,
    errorReason,
    staleWarning,
    staleReason,
    loadedAt,
    requestGeneration,
    refetch,
  };
}
