import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import AppLayout from "./AppLayout";
import type { AnalyticsRefreshStatus } from "../types/analytics";

const getAnalyticsRefreshStatus = vi.fn();

vi.mock("../services/analyticsApi", () => ({
  getAnalyticsRefreshStatus: (...args: unknown[]) => getAnalyticsRefreshStatus(...args),
}));

vi.mock("../components/AutoReloadOnBackendOnline", () => ({ default: () => null }));
vi.mock("../components/BackendWakeupNotice", () => ({ default: () => null }));
vi.mock("../components/GlobalRequestSpinner", () => ({ default: () => null }));
vi.mock("../components/WorkerStatusAlert", () => ({ default: () => null }));
vi.mock("../components/trendshoes/SeasonalImageCarousel", () => ({ default: () => null }));
vi.mock("../components/dashboard/DashboardFooter", () => ({ default: () => null }));
vi.mock("./components/Sidebar", () => ({
  default: ({ collapsed, onToggleCollapse }: { collapsed: boolean; onToggleCollapse: () => void }) => (
    <div data-testid="sidebar" data-collapsed={String(collapsed)}>
      <button type="button" data-testid="sidebar-toggle" onClick={onToggleCollapse}>Toggle</button>
    </div>
  ),
}));
vi.mock("./components/HeaderStatus", () => ({ default: () => null }));

function setViewportWidth(width: number) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => {
    const matches = query === "(min-width: 1024px) and (max-width: 1279px)"
      ? width >= 1024 && width <= 1279
      : width <= 1023;
    return {
      matches,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    };
  });
}

function renderLayout(pathname = "/") {
  return render(
    <MemoryRouter initialEntries={[pathname]}>
      <AppLayout><div>content</div></AppLayout>
    </MemoryRouter>,
  );
}

function buildRefreshStatus(): AnalyticsRefreshStatus {
  return {
    lastSuccessfulImportAtUtc: "2026-10-05T08:00:00Z",
    observedSalesPeriodFromUtc: "2026-09-01T00:00:00Z",
    observedSalesPeriodToUtc: "2026-10-04T00:00:00Z",
    lastAttemptAtUtc: "2026-10-05T08:01:00Z",
    isRunning: false,
    refreshedObjects: [],
    failedObjects: [],
    dataFreshnessStatus: "critical",
    processMode: "worker",
    processType: "worker",
    workersEnabled: true,
    generatedAtUtc: "2026-10-05T08:02:00Z",
    jobs: [],
  };
}

describe("AppLayout", () => {
  it("puts the skip link first in keyboard order and moves focus to main", () => {
    const { container } = renderLayout();
    const firstFocusable = container.querySelector<HTMLElement>("a[href], button, input, select, textarea, [tabindex]:not([tabindex='-1'])");
    const skipLink = screen.getByRole("link", { name: "Preskoči na sadržaj" });
    const main = container.querySelector("main#main-content");

    expect(firstFocusable).toBe(skipLink);
    skipLink.focus();
    fireEvent.click(skipLink);
    expect(main).toHaveFocus();
  });

  it("defaults to the sidebar rail between 1024px and 1279px", () => {
    window.localStorage.removeItem("trendplus.sidebarCollapsed");
    setViewportWidth(1100);

    renderLayout();

    expect(screen.getByTestId("sidebar")).toHaveAttribute("data-collapsed", "true");
  });

  it("lets a persisted sidebar choice override the small-laptop default", () => {
    window.localStorage.setItem("trendplus.sidebarCollapsed", "expanded");
    setViewportWidth(1100);

    renderLayout();

    expect(screen.getByTestId("sidebar")).toHaveAttribute("data-collapsed", "false");
  });

  it("persists an explicit sidebar choice", () => {
    window.localStorage.removeItem("trendplus.sidebarCollapsed");
    setViewportWidth(1100);

    renderLayout();
    fireEvent.click(screen.getByTestId("sidebar-toggle"));

    expect(screen.getByTestId("sidebar")).toHaveAttribute("data-collapsed", "false");
    expect(window.localStorage.getItem("trendplus.sidebarCollapsed")).toBe("expanded");
  });

  it("mounts one durable freshness banner on analytics routes", async () => {
    getAnalyticsRefreshStatus.mockResolvedValue(buildRefreshStatus());

    render(
      <MemoryRouter initialEntries={["/analytics"]}>
        <AppLayout>
          <div>analytics content</div>
        </AppLayout>
      </MemoryRouter>,
    );

    await waitFor(() => expect(screen.getByText("Kritično")).toBeInTheDocument());
    expect(screen.getByText("Poslednji uspešan import:")).toBeInTheDocument();
    expect(screen.getByText("Posmatrani promet:")).toBeInTheDocument();
    expect(getAnalyticsRefreshStatus).toHaveBeenCalledTimes(1);
  });
});
