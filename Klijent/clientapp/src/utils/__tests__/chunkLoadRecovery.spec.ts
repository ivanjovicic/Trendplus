import { beforeEach, describe, expect, it, vi } from "vitest";
import { isChunkLoadError, recoverFromChunkLoadError } from "../chunkLoadRecovery";

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
});
