import { describe, expect, it, vi } from "vitest";
import { disableWorkers, enableWorkers } from "../workersApi";
import { fetchWithTimeout } from "../../utils/fetchWithTimeout";

vi.mock("../../utils/fetchWithTimeout", () => ({ fetchWithTimeout: vi.fn() }));
vi.mock("../../utils/apiUrl", () => ({ apiUrl: (path: string) => path }));

describe("workersApi admin writes", () => {
  it.each([
    [enableWorkers, "/api/workers/control/enable"],
    [disableWorkers, "/api/workers/control/disable"],
  ])("sends the supplied admin key to %s", async (action, path) => {
    vi.mocked(fetchWithTimeout).mockResolvedValue({ ok: true } as Response);

    await action(" admin-secret ");

    expect(fetchWithTimeout).toHaveBeenCalledWith(
      path,
      { method: "POST", headers: { "X-Admin-Key": "admin-secret" } },
      expect.any(Number),
    );
  });
});
