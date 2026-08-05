import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionReportPage from "../SupplierDecisionReportPage";

const getPrintPayloadSnapshotMock = vi.fn();
const resolveAnalyticsTablePayloadMock = vi.fn((payload) => payload);
const getSupplierDecisionDurableReportMock = vi.fn();

vi.mock("../../services/analyticsTableState", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsTableState")>("../../services/analyticsTableState");
  return {
    ...actual,
    getPrintPayloadSnapshot: (...args: unknown[]) => getPrintPayloadSnapshotMock(...args),
    resolveAnalyticsTablePayload: (...args: unknown[]) => resolveAnalyticsTablePayloadMock(...args),
  };
});

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

function legacySnapshot(overrides: Record<string, unknown> = {}) {
  return {
    payload: {
      tableKey: "supplier-decision-report",
      tableTitle: "Trendplus izveštaj dobavljača",
      columns: [{ key: "section", header: "Sekcija", dataType: "text" }],
      rows: [{ section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" }],
      filters: [],
      metadata: [],
      locale: "sr-RS",
    },
    savedAtUtc: "2026-08-05T08:00:00.000Z",
    expiresAtUtc: "2026-08-05T08:10:00.000Z",
    ttlMs: 10 * 60 * 1000,
    ageMs: 60_000,
    ...overrides,
  };
}

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
    getPrintPayloadSnapshotMock.mockReturnValueOnce(null);
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
          "sellThrough",
        ],
      })
    );
  });

  it("renders filtered_out empty state with recovery actions when payload is missing", () => {
    getPrintPayloadSnapshotMock.mockReturnValueOnce(null);

    renderPage();

    expect(screen.getByRole("heading", { name: "Pregled izveštaja je istekao" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Vrati se na dobavljače" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ponovo generiši report" })).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Otvori Scorecard" }).length).toBeGreaterThan(0);
  });

  it("watermarks local browser preview and disables export/print when backend fails", async () => {
    getPrintPayloadSnapshotMock.mockReturnValue(legacySnapshot());
    getSupplierDecisionDurableReportMock.mockRejectedValueOnce(new Error("Backend report down"));

    renderPage("/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30&stateKey=test");

    expect(await screen.findByTestId("local-preview-banner")).toBeInTheDocument();
    expect(screen.getByText(/LOKALNI BROWSER PREVIEW/i)).toBeInTheDocument();
    expect(screen.getByTestId("local-preview-meta")).toHaveTextContent(/TTL/i);
    expect(screen.getByTestId("local-preview-export-disabled")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "trigger-export-error" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Štampaj iz pregleda" })).not.toBeInTheDocument();
    expect(screen.getByText("report-body")).toBeInTheDocument();
  });

  it("watermarks stateKey-only local preview without durable params", () => {
    getPrintPayloadSnapshotMock.mockReturnValueOnce(legacySnapshot());

    renderPage();

    expect(screen.getByTestId("local-preview-banner")).toBeInTheDocument();
    expect(screen.getByTestId("local-preview-export-disabled")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Štampaj iz pregleda" })).not.toBeInTheDocument();
  });

  it("shows export error state when actions report PDF failure on durable payload", async () => {
    getPrintPayloadSnapshotMock.mockReturnValueOnce(null);
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

    renderPage("/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30");

    await screen.findByText("report-body");
    fireEvent.click(screen.getByRole("button", { name: "trigger-export-error" }));

    expect(screen.getByText("Izvoz izveštaja nije uspeo")).toBeInTheDocument();
    expect(screen.getByText(/PDF export trenutno nije dostupan/i)).toBeInTheDocument();
  });
});
