import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import LogsPage from "../LogsPage";
import { clearLogs, getLogById, getLogs } from "../../services/logsApi";

vi.mock("../../services/logsApi", () => ({
  clearLogs: vi.fn(),
  getLogById: vi.fn(),
  getLogs: vi.fn(),
}));

describe("LogsPage responsive toolbar and list", () => {
  beforeEach(() => {
    vi.spyOn(window, "prompt").mockReturnValue("fixture-admin-key");
    vi.mocked(getLogs).mockResolvedValue({
      logs: [{ id: 1, timestamp: "2026-10-01T10:00:00Z", level: "Information", message: "Sintetički log" }],
      totalCount: 1, pageNumber: 1, pageSize: 100,
    });
    vi.mocked(getLogById).mockResolvedValue(null);
    vi.mocked(clearLogs).mockResolvedValue({ deletedCount: 0 } as any);
  });

  afterEach(() => vi.restoreAllMocks());

  it("wraps toolbar actions and preserves the sticky key column and detail target", async () => {
    render(<LogsPage />);

    const region = await screen.findByRole("region", { name: "Tabela logova" });
    expect(region).toHaveAttribute("tabindex", "0");
    expect(region.querySelector("thead th")).toHaveClass("sticky", "left-0");
    expect(screen.getByRole("button", { name: "Otvori detalje loga" })).toHaveClass("min-h-11", "min-w-11");
    expect(document.querySelector(".toolbar")).toHaveClass("lg:grid-cols-3", "xl:grid-cols-6");
  });
});
