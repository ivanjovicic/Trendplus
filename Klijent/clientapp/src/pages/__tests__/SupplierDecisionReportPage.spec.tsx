import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionReportPage from "../SupplierDecisionReportPage";

const getPrintPayloadMock = vi.fn();
const resolveAnalyticsTablePayloadMock = vi.fn((payload) => payload);
const getSupplierDecisionDurableReportMock = vi.fn();

vi.mock("../../services/analyticsTableState", () => ({
  getPrintPayload: (...args: unknown[]) => getPrintPayloadMock(...args),
  resolveAnalyticsTablePayload: (...args: unknown[]) => resolveAnalyticsTablePayloadMock(...args),
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getSupplierDecisionDurableReport: (...args: unknown[]) => getSupplierDecisionDurableReportMock(...args),
  };
});

vi.mock("../../components/analytics/SupplierDecisionReport", () => ({
  default: () => <div>report-body</div>,
}));

vi.mock("../../components/analytics/SupplierDecisionReportActions", () => ({
  default: ({ onError }: { onError?: (message: string) => void }) => (
    <button type="button" onClick={() => onError?.("PDF export trenutno nije dostupan. Koristite Print izveštaj ili Export Excel.")}>
      trigger-export-error
    </button>
  ),
}));

function renderPage(path = "/analytics/supplier/report?stateKey=test") {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/analytics/supplier/report" element={<SupplierDecisionReportPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("SupplierDecisionReportPage", () => {
  it("forwards section query to durable endpoint and injects methodology keys", async () => {
    getPrintPayloadMock.mockReturnValueOnce(null);
    getSupplierDecisionDurableReportMock.mockResolvedValueOnce({
      payload: {
        tableKey: "supplier-decision-report",
        tableTitle: "Trendplus izveštaj dobavljača",
        documentType: "supplier-decision-report",
        templateName: "supplier-decision",
        locale: "sr-RS",
        columns: [{ key: "section", header: "Sekcija", dataType: "text" }],
        rows: [{ section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" }],
        filters: [],
        metadata: [],
      },
    });

    renderPage("/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30&section=supplier_negotiation_pack");

    await screen.findByText("report-body");

    expect(getSupplierDecisionDurableReportMock).toHaveBeenCalledWith(
      expect.objectContaining({
        fromDate: "2026-04-01",
        toDate: "2026-06-30",
        section: "supplier_negotiation_pack",
      })
    );

    expect(resolveAnalyticsTablePayloadMock).toHaveBeenCalledWith(
      expect.objectContaining({
        methodologyMetricKeys: [
          "revenue",
          "marginContribution",
          "markdownDependency",
          "stockAtRisk",
          "confidencePct",
          "reliabilityPct",
        ],
      })
    );
  });

  it("renders filtered_out empty state with recovery actions when payload is missing", () => {
    getPrintPayloadMock.mockReturnValueOnce(null);

    renderPage();

    expect(screen.getByRole("heading", { name: "Pregled izveštaja je istekao" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Vrati se na dobavljače" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ponovo generiši report" })).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Otvori Scorecard" }).length).toBeGreaterThan(0);
  });

  it("shows export error state when actions report PDF failure", () => {
    getPrintPayloadMock.mockReturnValueOnce({
      tableKey: "supplier-decision-report",
      tableTitle: "Trendplus izveštaj dobavljača",
      columns: [{ key: "section", header: "Sekcija", dataType: "text" }],
      rows: [{ section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" }],
      filters: [],
      metadata: [],
      locale: "sr-RS",
    });

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "trigger-export-error" }));

    expect(screen.getByText("Izvoz izveštaja nije uspeo")).toBeInTheDocument();
    expect(screen.getByText(/PDF export trenutno nije dostupan/i)).toBeInTheDocument();
  });
});
