import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import HomePage from "./HomePage";

const backendStatus = vi.hoisted(() => ({
  current: { online: true, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") as Date | null },
}));

vi.mock("../context/useBackendStatus", () => ({
  useBackendStatus: () => backendStatus.current,
}));
vi.mock("../components/dashboard/TrendModelList", () => ({
  default: () => <div data-testid="trend-model-list" />,
}));
vi.mock("../components/trendshoes/SeasonalImageCarousel", () => ({
  default: () => <div data-testid="seasonal-image-carousel" />,
}));

function renderHome() {
  return render(
    <MemoryRouter initialEntries={["/"]}>
      <HomePage />
    </MemoryRouter>,
  );
}

describe("HomePage business truth (RQ661)", () => {
  beforeEach(() => {
    backendStatus.current = { online: true, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") };
  });

  it("does not present hard-coded numbers or recommendations as measured business facts", () => {
    const { container } = renderHome();
    const text = container.textContent ?? "";

    for (const claim of ["+12.8%", "Trend signal", "Import jobs", "Aktivne sekcije", "18%", "3 SKU", "top 10 artikala", "Insight Pulse", "trend scraping"]) {
      expect(text, `Home must not show "${claim}"`).not.toContain(claim);
    }
    expect(screen.queryByTestId("trend-model-list")).not.toBeInTheDocument();
    expect(screen.queryByTestId("seasonal-image-carousel")).not.toBeInTheDocument();
  });

  it("links to the core retail analytics reports", () => {
    renderHome();

    for (const [name, href] of [
      ["Pregled poslovanja", "/analytics"],
      ["Dnevna prodaja", "/analytics/daily-sales"],
      ["Analitika zaliha", "/analytics/inventory"],
      ["Prodaja po dobavljačima", "/analytics/supplier"],
      ["Prioriteti nivelacije", "/analytics/pre-nivelacija-prioriteti"],
      ["Puls odluka", "/analytics/decision-pulse"],
    ] as const) {
      expect(screen.getByRole("link", { name: new RegExp(name) })).toHaveAttribute("href", href);
    }
  });

  it("explains historical versus current numbers through the data-quality trust page", () => {
    renderHome();

    expect(screen.getByRole("link", { name: /Proveri svežinu i kvalitet podataka/ })).toHaveAttribute("href", "/analytics/data-quality");
  });

  it.each([
    ["initial probe", { online: false, checking: true, lastCheckedAt: null }, "PROVERA"],
    ["recovering", { online: false, checking: true, lastCheckedAt: new Date("2026-10-14T10:00:00Z") }, "BUDI SE"],
    ["online", { online: true, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") }, "ONLINE"],
    ["offline", { online: false, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") }, "OFFLINE"],
  ])("shows the real backend status while %s", (_label, status, expected) => {
    backendStatus.current = status;
    renderHome();

    expect(screen.getByText("Backend status")).toBeInTheDocument();
    expect(screen.getByText(expected)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Pregled poslovanja/ })).toBeInTheDocument();
  });
});
