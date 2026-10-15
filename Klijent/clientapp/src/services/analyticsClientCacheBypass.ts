let clientCacheBypassDepth = 0;

/**
 * Runs `start` so that analytics requests issued synchronously inside it skip the short-lived browser cache
 * (manual refresh). Kept in its own module so hooks can use it even where analyticsApi is mocked in tests.
 */
export function withClientCacheBypass<T>(start: () => Promise<T>): Promise<T> {
  clientCacheBypassDepth += 1;
  try {
    return start();
  } finally {
    clientCacheBypassDepth -= 1;
  }
}

export function isClientCacheBypassActive(): boolean {
  return clientCacheBypassDepth > 0;
}
