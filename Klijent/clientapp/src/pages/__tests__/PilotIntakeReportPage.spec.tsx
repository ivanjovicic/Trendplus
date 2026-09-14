import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PilotIntakeReportPage from "../PilotIntakeReportPage";
import { AnalyticsMetaError } from "../../services/analyticsApi";

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

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ periodFrom, periodTo, lastRefreshAt }: { periodFrom?: string | null; periodTo?: string | null; lastRefreshAt?: string | null }) => (
    <div data-testid="trust-header">
      trust-header:{periodFrom ?? "unknown-period-from"}|{periodTo ?? "unknown-period-to"}|{lastRefreshAt ?? "unknown-refresh"}
    </div>
  ),
}));
vi.mock("../../components/analytics/AnalyticsRefreshStatusBanner", () => ({ default: () => <div>refresh-banner</div> }));
vi.mock("../../components/analytics/PilotDataQualityIntakeReport", () => ({
  default: ({ durableReport }: { durableReport: { reportTitle?: string; generatedAtUtc?: string | null; periodFrom?: string | null; periodTo?: string | null } | null }) => (
    <div>
      pilot-report:{durableReport?.reportTitle ?? "missing"}|generated:{durableReport?.generatedAtUtc ?? "unknown-generated"}|period:{durableReport?.periodFrom ?? "unknown-from"}-{durableReport?.periodTo ?? "unknown-to"}
    </div>
  ),
}));

function browserPreviewPayload(metadata: Array<{ key: string; label: string; value: string | null }> = [], rows: unknown[] = []) {
  return {
    tableKey: "pilot-intake",
    tableTitle: "Browser preview",
    documentType: "pilot-intake",
    templateName: "analytics-table-default",
    locale: "sr-RS",
    columns: [],
    rows,
    filters: [],
    metadata,
  };
}

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
    getAnalyticsRefreshStatusMock.mockResolvedValue({
      isRunning: false,
      dataFreshnessStatus: "fresh",
      lastSuccessfulRefreshAtUtc: "2026-09-14T10:00:00Z",
    });
  });

  it("reloads a durable query URL from the backend after remount", async () => {
    getPilotIntakeDurableReportMock.mockResolvedValue(durableReport());
    const url = "/analytics/reports/pilot-intake?fromDate=2026-04-01&toDate=2026-06-30";

    const first = renderPage(url);
    expect(await screen.findByText(/pilot-report:Trajni pilot report/)).toBeInTheDocument();
    first.unmount();

    renderPage(url);
    expect(await screen.findByText(/pilot-report:Trajni pilot report/)).toBeInTheDocument();

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

    expect(await screen.findByText(/pilot-report:Trajni pilot report/)).toBeInTheDocument();
    expect(getBrowserPreviewPayloadMock).not.toHaveBeenCalled();
    expect(screen.queryByRole("heading", { name: "Pregled izveštaja je istekao" })).not.toBeInTheDocument();
  });

  it("renders an explicit invalid-period state instead of a substituted report", async () => {
    getPilotIntakeDurableReportMock.mockRejectedValue(
      new AnalyticsMetaError("Period izveštaja nije validan.", { errorCode: "invalid_period" })
    );

    renderPage("/analytics/reports/pilot-intake?fromDate=2026-06-30&toDate=2026-06-01");

    expect(await screen.findByRole("heading", { name: "Period izveštaja nije validan." })).toBeInTheDocument();
    expect(screen.getByText("Unesite oba datuma u formatu YYYY-MM-DD.")).toBeInTheDocument();
    expect(screen.queryByText("pilot-report:Trajni pilot report")).not.toBeInTheDocument();
  });

  it("keeps explicit query period separate from preview provenance", async () => {
    getBrowserPreviewPayloadMock.mockReturnValue(browserPreviewPayload([
      { key: "generatedAtUtc", label: "Generisano", value: "2026-07-01T08:00:00Z" },
      { key: "lastRefreshAtUtc", label: "Osveženo", value: "2026-07-01T07:00:00Z" },
    ]));

    renderPage("/analytics/reports/pilot-intake?preview=browser&stateKey=explicit&fromDate=2026-04-01&toDate=2026-06-30");

    expect(await screen.findByText(/pilot-report:Browser preview\|generated:2026-07-01T08:00:00Z\|period:2026-04-01-2026-06-30/)).toBeInTheDocument();
    expect(screen.getByTestId("trust-header")).toHaveTextContent("2026-04-01|2026-06-30|2026-07-01T07:00:00Z");
  });

  it("uses explicit metadata period and timestamps when the query is absent", async () => {
    getBrowserPreviewPayloadMock.mockReturnValue(browserPreviewPayload([
      { key: "periodFromUtc", label: "Od", value: "2026-05-01" },
      { key: "periodToUtc", label: "Do", value: "2026-05-31" },
      { key: "generatedAtUtc", label: "Generisano", value: "2026-06-01T09:00:00Z" },
      { key: "lastRefreshAtUtc", label: "Osveženo", value: "2026-06-01T08:30:00Z" },
    ]));

    renderPage("/analytics/reports/pilot-intake?preview=browser&stateKey=metadata");

    expect(await screen.findByText(/period:2026-05-01-2026-05-31/)).toBeInTheDocument();
    expect(screen.getByTestId("trust-header")).toHaveTextContent("2026-05-01|2026-05-31|2026-06-01T08:30:00Z");
    expect(screen.getByText(/generated:2026-06-01T09:00:00Z/)).toBeInTheDocument();
  });

  it("keeps period and provenance unavailable when legacy preview metadata is absent", async () => {
    getBrowserPreviewPayloadMock.mockReturnValue(browserPreviewPayload([], []));

    renderPage("/analytics/reports/pilot-intake?preview=browser&stateKey=unknown");

    expect(await screen.findByText(/generated:unknown-generated\|period:unknown-from-unknown-to/)).toBeInTheDocument();
    expect(screen.getByTestId("trust-header")).toHaveTextContent("unknown-period-from|unknown-period-to|unknown-refresh");
    expect(screen.queryByText(/2026-09-14/)).not.toBeInTheDocument();
  });

  it("renders a valid empty browser preview without inventing data or provenance", async () => {
    getBrowserPreviewPayloadMock.mockReturnValue(browserPreviewPayload([], []));

    renderPage("/analytics/reports/pilot-intake?preview=browser&stateKey=empty");

    expect(await screen.findByText(/pilot-report:Browser preview/)).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Pregled izveštaja je istekao" })).not.toBeInTheDocument();
  });
});
