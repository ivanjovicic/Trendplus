import {
  notifyBackendReachable,
  notifyBackendUnreachable,
  type BackendUnreachableReason,
} from "../context/backendReachabilityEvents";

const TRACKED_PREFIXES = ["/api", "/artikli", "/scrapers", "/health", "/ready", "/admin"];
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

function isTrackedPath(pathname: string): boolean {
  return TRACKED_PREFIXES.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

function shouldTrackBackendRequest(input: RequestInfo | URL): boolean {
  const url = parseUrl(toRequestUrl(input));
  if (!url) return false;

  const pathname = url.pathname.toLowerCase();
  if (isStaticAsset(pathname)) return false;
  if (isTrackedPath(pathname)) return true;

  const apiOrigin = getOrigin(import.meta.env.VITE_API_BASE_URL);
  const fallbackApiOrigin = getOrigin(import.meta.env.VITE_API_FALLBACK_URL);
  return Boolean(
    (apiOrigin && url.origin === apiOrigin) ||
    (fallbackApiOrigin && url.origin === fallbackApiOrigin)
  );
}

function classifyTransportFailure(error: unknown): BackendUnreachableReason | null {
  if (error instanceof DOMException && error.name === "AbortError") {
    return "timeout";
  }

  if (error instanceof Error) {
    const text = `${error.name} ${error.message}`.toLowerCase();
    if (text.includes("timeout") || text.includes("timed out")) return "timeout";
    if (
      text.includes("networkerror") ||
      text.includes("failed to fetch") ||
      text.includes("fetch failed") ||
      text.includes("connection") ||
      text.includes("econnrefused") ||
      text.includes("dns")
    ) {
      return "network";
    }
  }

  return null;
}

function isIntentionalAbort(signal: AbortSignal | null | undefined): boolean {
  if (!signal?.aborted) return false;

  const reason = signal.reason;
  if (reason instanceof DOMException && reason.name === "TimeoutError") return false;
  if (reason instanceof Error && reason.message.toLowerCase().includes("timeout")) return false;
  if (typeof reason === "string" && reason.toLowerCase().includes("timeout")) return false;

  return true;
}

export function installBackendReachabilityFetchLayer(): void {
  if (typeof window === "undefined") return;

  const globalWindow = window as Window & { __trendplusBackendReachabilityInstalled?: boolean };
  if (globalWindow.__trendplusBackendReachabilityInstalled) {
    return;
  }

  globalWindow.__trendplusBackendReachabilityInstalled = true;
  const nextFetch = window.fetch.bind(window);

  window.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    if (!shouldTrackBackendRequest(input)) {
      return nextFetch(input, init);
    }

    const requestedUrl = toRequestUrl(input);
    try {
      const response = await nextFetch(input, init);
      const url = response.url || requestedUrl;

      if (response.status >= 500) {
        notifyBackendUnreachable({
          source: "request",
          reason: "server-error",
          status: response.status,
          url,
        });
      } else {
        notifyBackendReachable({
          source: "request",
          status: response.status,
          url,
        });
      }

      return response;
    } catch (error) {
      if (isIntentionalAbort(init?.signal ?? null)) {
        throw error;
      }

      const reason = classifyTransportFailure(error);
      if (reason) {
        notifyBackendUnreachable({
          source: "request",
          reason,
          message: error instanceof Error ? error.message : String(error),
          url: requestedUrl,
        });
      }

      throw error;
    }
  }) as typeof window.fetch;
}
