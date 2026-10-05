import { render, screen, waitFor } from "@testing-library/react";
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
vi.mock("./components/Sidebar", () => ({ default: () => null }));
vi.mock("./components/HeaderStatus", () => ({ default: () => null }));

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
