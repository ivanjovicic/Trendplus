import React, { createContext, useContext, useEffect, useMemo, useRef, useState } from "react";

type RequestActivityState = {
  activeRequests: number;
  hasActiveRequests: boolean;
};

const RequestActivityContext = createContext<RequestActivityState>({
  activeRequests: 0,
  hasActiveRequests: false,
});

const IGNORED_PATHS = new Set([
  "/health",
  "/api/workers/health",
  "/api/redis/status",
]);

const TRACKED_PREFIXES = ["/api", "/artikli", "/scrapers"];

const STATIC_EXTENSIONS = [
  ".png",
  ".jpg",
  ".jpeg",
  ".gif",
  ".webp",
  ".svg",
  ".ico",
  ".css",
  ".js",
  ".map",
  ".woff",
  ".woff2",
  ".ttf",
  ".eot",
];

function toRequestUrl(input: RequestInfo | URL): string {
  if (typeof input === "string") return input;
  if (input instanceof URL) return input.toString();
  return input.url;
}

function parseUrl(rawUrl: string): URL | null {
  try {
    return new URL(rawUrl, window.location.origin);
  } catch {
    return null;
  }
}

function getOrigin(rawUrl: string | undefined): string | null {
  if (!rawUrl) return null;
  const parsed = parseUrl(rawUrl);
  return parsed?.origin ?? null;
}

function isStaticAsset(pathname: string): boolean {
  return STATIC_EXTENSIONS.some((extension) => pathname.endsWith(extension));
}

function shouldTrackRequest(input: RequestInfo | URL): boolean {
  const url = parseUrl(toRequestUrl(input));
  if (!url) return false;

  const pathname = url.pathname.toLowerCase();

  if (IGNORED_PATHS.has(pathname) || isStaticAsset(pathname)) {
    return false;
  }

  if (TRACKED_PREFIXES.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`))) {
    return true;
  }

  const apiOrigin = getOrigin(import.meta.env.VITE_API_BASE_URL);
  const pythonOrigin = getOrigin(import.meta.env.VITE_PYTHON_API_URL);

  if (apiOrigin && url.origin === apiOrigin) {
    return true;
  }

  if (pythonOrigin && url.origin === pythonOrigin) {
    return true;
  }

  return false;
}

export const RequestActivityProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [activeRequests, setActiveRequests] = useState(0);
  const mountedRef = useRef(true);

  useEffect(() => {
    if (typeof window === "undefined") return;

    mountedRef.current = true;
    const originalFetch = window.fetch.bind(window);

    const trackedFetch: typeof window.fetch = async (input, init) => {
      if (!shouldTrackRequest(input)) {
        return originalFetch(input, init);
      }

      if (mountedRef.current) {
        setActiveRequests((current) => current + 1);
      }

      try {
        return await originalFetch(input, init);
      } finally {
        if (mountedRef.current) {
          setActiveRequests((current) => Math.max(0, current - 1));
        }
      }
    };

    window.fetch = trackedFetch;

    return () => {
      mountedRef.current = false;
      window.fetch = originalFetch;
    };
  }, []);

  const value = useMemo<RequestActivityState>(
    () => ({
      activeRequests,
      hasActiveRequests: activeRequests > 0,
    }),
    [activeRequests]
  );

  return <RequestActivityContext.Provider value={value}>{children}</RequestActivityContext.Provider>;
};

export function useRequestActivity(): RequestActivityState {
  return useContext(RequestActivityContext);
}
