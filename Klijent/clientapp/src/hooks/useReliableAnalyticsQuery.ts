import { useCallback, useEffect, useRef, useState } from "react";
import { withClientCacheBypass } from "../services/analyticsClientCacheBypass";

export type ReliableAnalyticsQuery<T> = (signal: AbortSignal) => Promise<T>;

export type UseReliableAnalyticsQueryOptions<T> = {
  query: ReliableAnalyticsQuery<T>;
  enabled?: boolean;
  initialData?: T | null;
  getErrorMessage?: (reason: unknown) => string;
  /**
   * Identity of the request context (period, store, supplier, dataScope, filters). When it changes the
   * previous snapshot is dropped, so a failed request never shows data of another context as current.
   * Opt-in: when omitted the snapshot is kept and a failed refetch is shown as stale (existing behaviour).
   */
  contextKey?: string;
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
    contextKey,
  } = options;
  const contextIdentity: string | undefined = contextKey;
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
  const contextRef = useRef<string | undefined>(contextIdentity);
  const bypassClientCacheRef = useRef(false);

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
    if (contextRef.current !== contextIdentity) {
      // New context: the old snapshot belongs to other filters and must not be shown as current.
      contextRef.current = contextIdentity;
      dataRef.current = null;
      setData(null);
      setLoadedAt(null);
    }
    if (dataRef.current == null) {
      setInitialLoading(true);
    } else {
      setRefetching(true);
    }

    const bypassClientCache = bypassClientCacheRef.current;
    bypassClientCacheRef.current = false;
    const start = () => query(controller.signal);
    void (bypassClientCache ? withClientCacheBypass(start) : start())
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
  }, [enabled, getErrorMessage, query, reloadVersion, contextIdentity]);

  const refetch = useCallback(() => {
    // Manual refresh must reach the server instead of the short-lived browser cache.
    bypassClientCacheRef.current = true;
    setReloadVersion((current) => current + 1);
  }, []);

  // An effect cannot protect the synchronous render that first sees new filters.
  // Hide the old snapshot immediately, before useEffect aborts/restarts the request.
  // A failed refresh with an unchanged context continues to show the stale snapshot.
  const contextIsCurrent = contextRef.current === contextIdentity;
  return {
    data: contextIsCurrent ? data : null,
    initialLoading: contextIsCurrent ? initialLoading : enabled,
    refetching: contextIsCurrent ? refetching : false,
    error: contextIsCurrent ? error : null,
    errorReason: contextIsCurrent ? errorReason : null,
    staleWarning: contextIsCurrent ? staleWarning : null,
    staleReason: contextIsCurrent ? staleReason : null,
    loadedAt: contextIsCurrent ? loadedAt : null,
    requestGeneration,
    refetch,
  };
}
