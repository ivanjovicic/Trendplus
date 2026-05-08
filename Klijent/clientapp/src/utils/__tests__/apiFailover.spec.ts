import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

function configureFailoverEnv(): void {
  vi.stubEnv("VITE_API_RENDER_BASE_URL", "https://render.example");
  vi.stubEnv("VITE_API_FLY_BASE_URL", "https://fly.example");
  vi.stubEnv("VITE_API_BASE_URL", "https://render.example");
  vi.stubEnv("VITE_API_FALLBACK_URL", "https://fly.example");
  vi.stubEnv("VITE_API_PRIMARY_REQUEST_TIMEOUT_MS", "200");
  vi.stubEnv("VITE_API_FALLBACK_REQUEST_TIMEOUT_MS", "200");
  vi.stubEnv("VITE_API_FALLBACK_PROBE_TIMEOUT_MS", "200");
  vi.stubEnv("VITE_API_PRIMARY_RETRY_COOLDOWN_MS", "20");
  vi.stubEnv("VITE_API_PRIMARY_WARMUP_MAX_MS", "120");
  vi.stubEnv("VITE_API_PRIMARY_WARMUP_MIN_BACKOFF_MS", "5");
  vi.stubEnv("VITE_API_PRIMARY_WARMUP_INITIAL_BACKOFF_MS", "5");
  vi.stubEnv("VITE_API_PRIMARY_WARMUP_MAX_BACKOFF_MS", "15");
  vi.stubEnv("VITE_API_RECOVERY_PROBE_PATH", "/ready");
  vi.stubEnv("VITE_API_FALLBACK_DEBUG", "0");
}

describe("apiFailover", () => {
  const originalFetch = window.fetch.bind(window);

  beforeEach(() => {
    vi.resetModules();
    configureFailoverEnv();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  afterEach(async () => {
    const mod = await import("../apiFailover");
    mod.__resetApiFailoverForTests();
    window.fetch = originalFetch;
    vi.unstubAllEnvs();
  });

  it("uses render as default backend provider", async () => {
    const mod = await import("../apiFailover");
    const preference = mod.getBackendRoutingPreference();
    expect(preference.primaryProvider).toBe("render");
  });

  it("keeps render as active provider during warmup and avoids immediate fly fallback", async () => {
    let renderApiCalls = 0;
    let renderReadyCalls = 0;
    let flyCalls = 0;

    window.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === "https://render.example/api/test") {
        renderApiCalls += 1;
        if (renderApiCalls === 1) {
          return new Response(JSON.stringify({ status: "warming_up" }), {
            status: 503,
            headers: { "Content-Type": "application/json", "Retry-After": "0" },
          });
        }

        return new Response(JSON.stringify({ ok: true }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      }

      if (url === "https://render.example/ready") {
        renderReadyCalls += 1;
        if (renderReadyCalls === 1) {
          return new Response(JSON.stringify({ status: "warming_up", ready: false }), {
            status: 503,
            headers: { "Content-Type": "application/json", "Retry-After": "0" },
          });
        }

        return new Response(JSON.stringify({ status: "healthy", ready: true }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      }

      if (url.startsWith("https://fly.example")) {
        flyCalls += 1;
        return new Response(JSON.stringify({ ok: true }), { status: 200 });
      }

      throw new Error(`Unexpected URL: ${url}`);
    }) as typeof window.fetch;

    const mod = await import("../apiFailover");
    mod.installApiFailoverFetchLayer();

    const response = await window.fetch("/api/test");
    expect(response.status).toBe(200);
    expect(flyCalls).toBe(0);
    expect(renderReadyCalls).toBeGreaterThan(0);
    expect(mod.getBackendProviderStateSnapshot().activeHost).toBe("primary");
  });

  it("does not trigger fallback on external AbortError", async () => {
    let flyCalls = 0;

    window.fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith("https://fly.example")) {
        flyCalls += 1;
      }

      if (init?.signal?.aborted) {
        throw new DOMException("Request aborted", "AbortError");
      }

      await new Promise((resolve) => window.setTimeout(resolve, 20));
      return new Response(JSON.stringify({ ok: true }), { status: 200 });
    }) as typeof window.fetch;

    const mod = await import("../apiFailover");
    mod.installApiFailoverFetchLayer();

    const controller = new AbortController();
    controller.abort("route-change");
    const requestPromise = window.fetch("/api/test", { signal: controller.signal });

    await expect(requestPromise).rejects.toBeInstanceOf(DOMException);
    expect(flyCalls).toBe(0);
    expect(mod.getBackendProviderStateSnapshot().activeHost).toBe("primary");
  });

  it("falls back to fly only after warmup window is exhausted and fly is healthy", async () => {
    vi.stubEnv("VITE_API_PRIMARY_WARMUP_MAX_MS", "40");
    vi.resetModules();
    configureFailoverEnv();
    vi.stubEnv("VITE_API_PRIMARY_WARMUP_MAX_MS", "40");

    let flyApiCalls = 0;
    let flyReadyCalls = 0;
    let renderReadyCalls = 0;

    window.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === "https://render.example/api/test") {
        throw new TypeError("Failed to fetch");
      }

      if (url === "https://render.example/ready") {
        renderReadyCalls += 1;
        return new Response(JSON.stringify({ status: "warming_up", ready: false }), {
          status: 503,
          headers: { "Content-Type": "application/json", "Retry-After": "0" },
        });
      }

      if (url === "https://fly.example/ready") {
        flyReadyCalls += 1;
        return new Response(JSON.stringify({ status: "healthy", ready: true }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      }

      if (url === "https://fly.example/api/test") {
        flyApiCalls += 1;
        return new Response(JSON.stringify({ provider: "fly" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      }

      throw new Error(`Unexpected URL: ${url}`);
    }) as typeof window.fetch;

    const mod = await import("../apiFailover");
    mod.installApiFailoverFetchLayer();

    const response = await window.fetch("/api/test");
    const payload = await response.json();
    expect(response.status).toBe(200);
    expect(payload.provider).toBe("fly");
    expect(renderReadyCalls).toBeGreaterThan(0);
    expect(flyReadyCalls).toBeGreaterThan(0);
    expect(flyApiCalls).toBeGreaterThan(0);
    expect(mod.getBackendProviderStateSnapshot().activeHost).toBe("fallback");
  });
});
