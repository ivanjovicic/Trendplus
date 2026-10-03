import React from "react";

const ChunkReloadStorageKey = "trendplus:chunk-load-reload-at";
const ChunkReloadCooldownMs = 30_000;
let lazyChunkImportDepth = 0;

const chunkLoadErrorPatterns = [
  "Failed to fetch dynamically imported module",
  "Importing a module script failed",
  "ChunkLoadError",
  "Loading chunk",
  "Expected a JavaScript-or-Wasm module script",
  "reading 'default'",
  'Cannot read properties of undefined (reading "default")',
];

export function isChunkLoadError(error: unknown): boolean {
  if (error instanceof ChunkLoadError || (typeof error === "object" && error !== null && "isChunkLoadError" in error && error.isChunkLoadError === true)) {
    return true;
  }

  const message = error instanceof Error
    ? error.message
    : typeof error === "string"
      ? error
      : String(error ?? "");

  return chunkLoadErrorPatterns.some((pattern) => message.includes(pattern));
}

export class ChunkLoadError extends Error {
  public readonly isChunkLoadError = true;

  public constructor(message: string, options?: ErrorOptions) {
    super(message, options);
    this.name = "ChunkLoadError";
  }
}

function extractChunkUrl(error: unknown): URL | null {
  const message = error instanceof Error ? error.message : String(error ?? "");
  const match = message.match(/(?:https?:\/\/|\/)[^\s"'`<>]+/);
  if (!match) return null;

  try {
    const candidate = match[0].replace(/[),.;]+$/, "");
    const url = new URL(candidate, window.location.href);
    return url.origin === window.location.origin ? url : null;
  } catch {
    return null;
  }
}

export async function retryLazyImport<T>(
  importer: () => Promise<T>,
  reload: () => void = () => window.location.reload(),
  now = Date.now,
  retryImporter: (url: string) => Promise<T> = (url) => import(/* @vite-ignore */ url) as Promise<T>,
): Promise<T> {
  lazyChunkImportDepth += 1;
  try {
    try {
      return await importer();
    } catch (firstError) {
      if (!isChunkLoadError(firstError)) throw firstError;

      const chunkUrl = extractChunkUrl(firstError);
      if (!chunkUrl) {
        if (recoverFromChunkLoadError(firstError, reload, now)) {
          throw new ChunkLoadError("Route chunk failed to load; application reload scheduled.", { cause: firstError });
        }
        throw new ChunkLoadError("Route chunk failed to load. Refresh the application to continue.", { cause: firstError });
      }

      chunkUrl.searchParams.set("__trendplus_retry", String(now()));
      try {
        // Vite cannot statically analyze a URL discovered from the failed chunk error.
        return await retryImporter(chunkUrl.href);
      } catch (retryError) {
        if (recoverFromChunkLoadError(retryError, reload, now)) {
          throw new ChunkLoadError("Route chunk could not be recovered; application reload scheduled.", { cause: retryError });
        }
        throw new ChunkLoadError("Route chunk could not be recovered. Refresh the application to continue.", { cause: retryError });
      }
    }
  } finally {
    lazyChunkImportDepth -= 1;
  }
}

export function lazyWithChunkRecovery<T extends { default: React.ComponentType<any> }>(
  importer: () => Promise<T>,
): React.LazyExoticComponent<T["default"]> {
  return React.lazy(() => retryLazyImport(importer));
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
    if (lazyChunkImportDepth > 0) return;

    if (recoverFromChunkLoadError(payload, reload, now)) {
      event.preventDefault();
    }
  };

  window.addEventListener("vite:preloadError", handler);
  return () => window.removeEventListener("vite:preloadError", handler);
}
