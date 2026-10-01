const ChunkReloadStorageKey = "trendplus:chunk-load-reload-at";
const ChunkReloadCooldownMs = 30_000;

const chunkLoadErrorPatterns = [
  "Failed to fetch dynamically imported module",
  "Importing a module script failed",
  "ChunkLoadError",
  "Loading chunk",
  "Expected a JavaScript-or-Wasm module script",
];

export function isChunkLoadError(error: unknown): boolean {
  const message = error instanceof Error
    ? error.message
    : typeof error === "string"
      ? error
      : String(error ?? "");

  return chunkLoadErrorPatterns.some((pattern) => message.includes(pattern));
}

export function recoverFromChunkLoadError(
  error: unknown,
  reload: () => void = () => window.location.reload(),
  now = Date.now,
): boolean {
  if (!isChunkLoadError(error)) {
    return false;
  }

  const lastReloadAt = Number(window.sessionStorage.getItem(ChunkReloadStorageKey) ?? "0");
  const elapsedMs = now() - lastReloadAt;
  if (Number.isFinite(lastReloadAt) && lastReloadAt > 0 && elapsedMs >= 0 && elapsedMs < ChunkReloadCooldownMs) {
    return false;
  }

  window.sessionStorage.setItem(ChunkReloadStorageKey, String(now()));
  reload();
  return true;
}

export function installChunkLoadRecovery(
  reload: () => void = () => window.location.reload(),
  now = Date.now,
): () => void {
  const handler = (event: Event) => {
    const preloadEvent = event as Event & { payload?: unknown; detail?: unknown };
    const payload = preloadEvent.payload ?? preloadEvent.detail ?? event;

    // Suppress Vite's default rejection only when we are actually replacing
    // the stale document. During the cooldown, propagate the import failure so
    // React.lazy/ErrorBoundary receives a real error instead of undefined.
    if (recoverFromChunkLoadError(payload, reload, now)) {
      event.preventDefault();
    }
  };

  window.addEventListener("vite:preloadError", handler);
  return () => window.removeEventListener("vite:preloadError", handler);
}
