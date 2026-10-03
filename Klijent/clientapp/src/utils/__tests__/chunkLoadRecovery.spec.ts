import { beforeEach, describe, expect, it, vi } from "vitest";
import { ChunkLoadError, installChunkLoadRecovery, isChunkLoadError, recoverFromChunkLoadError, retryLazyImport } from "../chunkLoadRecovery";

describe("chunkLoadRecovery", () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it("detects dynamic import chunk load failures", () => {
    expect(isChunkLoadError(new TypeError("Failed to fetch dynamically imported module: /assets/ConfigurationPage.js"))).toBe(true);
    expect(isChunkLoadError("Expected a JavaScript-or-Wasm module script but the server responded with a MIME type of \"text/html\".")).toBe(true);
    expect(isChunkLoadError(new Error("ordinary application error"))).toBe(false);
  });

  it("reloads once for a chunk load failure and then respects cooldown", () => {
    const reload = vi.fn();
    let currentTime = 1_000;
    const now = vi.fn(() => currentTime);

    expect(recoverFromChunkLoadError(new TypeError("Failed to fetch dynamically imported module"), reload, now)).toBe(true);
    expect(reload).toHaveBeenCalledTimes(1);

    currentTime = 2_000;
    expect(recoverFromChunkLoadError(new TypeError("Failed to fetch dynamically imported module"), reload, now)).toBe(false);
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it("retries a failed lazy import once with a cache-busted chunk URL", async () => {
    const importer = vi.fn().mockRejectedValue(new TypeError("Failed to fetch dynamically imported module: /assets/Route-abc.js"));
    const retryImporter = vi.fn().mockResolvedValue({ default: "route" });
    const reload = vi.fn();
    const result = await retryLazyImport(importer, reload, () => 1234, retryImporter);

    expect(result).toEqual({ default: "route" });
    expect(importer).toHaveBeenCalledTimes(1);
    expect(retryImporter).toHaveBeenCalledTimes(1);
    expect(retryImporter.mock.calls[0][0]).toBe(`${window.location.origin}/assets/Route-abc.js?__trendplus_retry=1234`);
    expect(reload).not.toHaveBeenCalled();
  });

  it("lets lazy import failures reach the retry helper and reloads after retry failure", async () => {
    const reload = vi.fn();
    const now = vi.fn(() => 50_000);
    const importer = vi.fn().mockImplementation(async () => {
      const event = new CustomEvent("vite:preloadError", {
        cancelable: true,
        detail: new TypeError("Failed to fetch dynamically imported module: /assets/Route-abc.js"),
      });
      expect(window.dispatchEvent(event)).toBe(true);
      expect(event.defaultPrevented).toBe(false);
      throw event.detail;
    });
    const retryImporter = vi.fn().mockRejectedValue(new TypeError("Failed to fetch dynamically imported module"));
    const cleanup = installChunkLoadRecovery(reload, now);

    await expect(retryLazyImport(importer, reload, now, retryImporter)).rejects.toBeInstanceOf(ChunkLoadError);
    expect(retryImporter).toHaveBeenCalledTimes(1);
    expect(reload).toHaveBeenCalledTimes(1);
    cleanup();
  });

  it("prevents preload default only when a reload is actually scheduled", () => {
    const reload = vi.fn();
    let currentTime = 1_000;
    const now = vi.fn(() => currentTime);
    const cleanup = installChunkLoadRecovery(reload, now);

    const first = new CustomEvent("vite:preloadError", {
      cancelable: true,
      detail: new TypeError("Failed to fetch dynamically imported module"),
    });
    expect(window.dispatchEvent(first)).toBe(false);
    expect(first.defaultPrevented).toBe(true);
    expect(reload).toHaveBeenCalledTimes(1);

    currentTime = 2_000;
    const second = new CustomEvent("vite:preloadError", {
      cancelable: true,
      detail: new TypeError("Failed to fetch dynamically imported module"),
    });
    expect(window.dispatchEvent(second)).toBe(true);
    expect(second.defaultPrevented).toBe(false);
    expect(reload).toHaveBeenCalledTimes(1);

    cleanup();
  });
});
