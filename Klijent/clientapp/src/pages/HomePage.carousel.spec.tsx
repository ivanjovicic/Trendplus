import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import HomePage from "./HomePage";
import AppLayout from "../layout/AppLayout";

const getAnalyticsRefreshStatus = vi.fn();
const fetchMock = vi.fn();

vi.mock("../services/analyticsApi", () => ({
  getAnalyticsRefreshStatus: (...args: unknown[]) => getAnalyticsRefreshStatus(...args),
  makeUrl: (path: string) => path,
}));

vi.mock("../components/AutoReloadOnBackendOnline", () => ({ default: () => null }));
vi.mock("../components/BackendWakeupNotice", () => ({ default: () => null }));
vi.mock("../components/GlobalRequestSpinner", () => ({ default: () => null }));
vi.mock("../components/WorkerStatusAlert", () => ({ default: () => null }));
vi.mock("../components/dashboard/DashboardFooter", () => ({ default: () => null }));
vi.mock("../components/dashboard/DashboardCards", () => ({ default: () => null }));
vi.mock("../components/dashboard/TrendModelList", () => ({ default: () => null }));
vi.mock("../layout/components/Sidebar", () => ({ default: () => null }));
vi.mock("../layout/components/HeaderStatus", () => ({ default: () => null }));
vi.mock("../context/useBackendStatus", () => ({
  useBackendStatus: () => ({ online: true, checking: false, lastCheckedAt: new Date() }),
}));

describe("seasonal carousel route ownership", () => {
  beforeEach(() => {
    getAnalyticsRefreshStatus.mockResolvedValue({
      lastSuccessfulImportAtUtc: null,
      observedSalesPeriodFromUtc: null,
      observedSalesPeriodToUtc: null,
      lastAttemptAtUtc: null,
      isRunning: false,
      refreshedObjects: [],
      failedObjects: [],
      dataFreshnessStatus: "ok",
      processMode: "worker",
      processType: "worker",
      workersEnabled: true,
      generatedAtUtc: "2026-10-05T08:00:00Z",
      jobs: [],
    });
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => [
        {
          id: 1,
          imageUrl: "https://example.com/a.jpg",
          source: "unsplash",
        },
      ],
    });
    fetchMock.mockClear();
    vi.stubGlobal("fetch", fetchMock);
    vi.spyOn(window, "matchMedia").mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }) as MediaQueryList);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows carousel on home and loads seasonal images", async () => {
    render(
      <MemoryRouter initialEntries={["/"]}>
        <AppLayout>
          <HomePage />
        </AppLayout>
      </MemoryRouter>,
    );

    expect(screen.getByTestId("home-seasonal-carousel")).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(fetchMock.mock.calls.some((call) => String(call[0]).includes("/api/trends/seasonal-images"))).toBe(true);
  });

  it("does not mount carousel or fetch seasonal images on analytics routes", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics"]}>
        <Routes>
          <Route
            path="/analytics"
            element={
              <AppLayout>
                <div>analytics content</div>
              </AppLayout>
            }
          />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.queryByTestId("home-seasonal-carousel")).not.toBeInTheDocument();
    expect(screen.queryByTestId("carousel-strip")).not.toBeInTheDocument();
    await waitFor(() => expect(getAnalyticsRefreshStatus).toHaveBeenCalled());
    expect(fetchMock.mock.calls.some((call) => String(call[0]).includes("/api/trends/seasonal-images"))).toBe(false);
  });
});