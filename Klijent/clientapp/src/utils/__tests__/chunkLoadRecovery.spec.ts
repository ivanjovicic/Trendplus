import { beforeEach, describe, expect, it, vi } from "vitest";
import { installChunkLoadRecovery, isChunkLoadError, recoverFromChunkLoadError } from "../chunkLoadRecovery";

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
