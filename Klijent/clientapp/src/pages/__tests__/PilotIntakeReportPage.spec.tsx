import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PilotIntakeReportPage from "../PilotIntakeReportPage";

const getBrowserPreviewPayloadMock = vi.fn();
const getAnalyticsRefreshStatusMock = vi.fn();
const getPilotIntakeDurableReportMock = vi.fn();

vi.mock("../../services/analyticsTableState", () => ({
  getBrowserPreviewPayload: (...args: unknown[]) => getBrowserPreviewPayloadMock(...args),
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getAnalyticsRefreshStatus: (...args: unknown[]) => getAnalyticsRefreshStatusMock(...args),
    getPilotIntakeDurableReport: (...args: unknown[]) => getPilotIntakeDurableReportMock(...args),
  };
});

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => <div>trust-header</div> }));
vi.mock("../../components/analytics/AnalyticsRefreshStatusBanner", () => ({ default: () => <div>refresh-banner</div> }));
vi.mock("../../components/analytics/PilotDataQualityIntakeReport", () => ({
  default: ({ durableReport }: { durableReport: { reportTitle?: string } | null }) => (
    <div>pilot-report:{durableReport?.reportTitle ?? "missing"}</div>
  ),
}));

function durableReport() {
  return {
    reportId: "pilot-1",
    stableQueryUrl: "/analytics/reports/pilot-intake?fromDate=2026-04-01&toDate=2026-06-30",
    reportTitle: "Trajni pilot report",
    reportType: "pilot-intake",
    generatedAtUtc: "2026-06-30T12:00:00Z",
    period: { fromUtc: "2026-04-01", toUtc: "2026-06-30", label: "Q2" },
    dataQualityStatus: "good",
    recommendationAllowed: true,
    warnings: [],
    methodology: "test",
    rows: [],
    sections: [],
    payload: { tableKey: "pilot-intake", tableTitle: "Pilot", columns: [], rows: [], filters: [], metadata: [] },
  };
}

function renderPage(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/analytics/reports/pilot-intake" element={<PilotIntakeReportPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("PilotIntakeReportPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getBrowserPreviewPayloadMock.mockReturnValue(null);
    getAnalyticsRefreshStatusMock.mockResolvedValue({ isRunning: false, dataFreshnessStatus: "fresh" });
  });

  it("reloads a durable query URL from the backend after remount", async () => {
    getPilotIntakeDurableReportMock.mockResolvedValue(durableReport());
    const url = "/analytics/reports/pilot-intake?fromDate=2026-04-01&toDate=2026-06-30";

    const first = renderPage(url);
    expect(await screen.findByText("pilot-report:Trajni pilot report")).toBeInTheDocument();
    first.unmount();

    renderPage(url);
    expect(await screen.findByText("pilot-report:Trajni pilot report")).toBeInTheDocument();

    expect(getPilotIntakeDurableReportMock).toHaveBeenCalledTimes(2);
    expect(getBrowserPreviewPayloadMock).not.toHaveBeenCalled();
  });

  it("shows expired state only when an explicit browser preview has no snapshot", async () => {
    renderPage("/analytics/reports/pilot-intake?preview=browser&stateKey=missing");

    expect(await screen.findByRole("heading", { name: "Pregled izveštaja je istekao" })).toBeInTheDocument();
    expect(getPilotIntakeDurableReportMock).not.toHaveBeenCalled();
    expect(getBrowserPreviewPayloadMock).toHaveBeenCalledWith("missing");
  });

  it("ignores a stale stateKey when the URL has a durable query context", async () => {
    getPilotIntakeDurableReportMock.mockResolvedValue(durableReport());

    renderPage("/analytics/reports/pilot-intake?fromDate=2026-04-01&toDate=2026-06-30&stateKey=stale");

    expect(await screen.findByText("pilot-report:Trajni pilot report")).toBeInTheDocument();
    expect(getBrowserPreviewPayloadMock).not.toHaveBeenCalled();
    expect(screen.queryByRole("heading", { name: "Pregled izveštaja je istekao" })).not.toBeInTheDocument();
  });
});
