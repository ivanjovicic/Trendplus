import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SupplierDecisionReportActions from "../SupplierDecisionReportActions";

const exportExcelMock = vi.fn(() => Promise.resolve());
const exportPdfMock = vi.fn(() => Promise.resolve());
const printPreviewMock = vi.fn(() => Promise.resolve());
const exportCsvMock = vi.fn(() => undefined);
const buildSummaryMock = vi.fn(() => "summary");
const getAnalyticsActionSourceStatusesMock = vi.fn(() => Promise.resolve({ items: [] }));
const upsertAnalyticsActionWithResultMock = vi.fn(() => Promise.resolve({
  item: { sourceKey: "supplier:signal_check:all:unknown-period:all" },
  created: true,
  existing: false,
  status: "new",
  sourceKey: "supplier:signal_check:all:unknown-period:all",
}));

vi.mock("../../../services/supplierDecisionReport", () => ({
  exportSupplierDecisionReportExcel: (...args: unknown[]) => exportExcelMock(...args),
  exportSupplierDecisionReportPdf: (...args: unknown[]) => exportPdfMock(...args),
  openSupplierDecisionPrintPreview: (...args: unknown[]) => printPreviewMock(...args),
  exportSupplierDecisionReportCsv: (...args: unknown[]) => exportCsvMock(...args),
  buildSupplierDecisionReportSummaryText: (...args: unknown[]) => buildSummaryMock(...args),
}));

vi.mock("../../../services/analyticsApi", () => ({
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  upsertAnalyticsActionWithResult: (...args: unknown[]) => upsertAnalyticsActionWithResultMock(...args),
}));

const payload = {
  tableKey: "supplier-decision-report",
  tableTitle: "Trendplus Supplier Decision Report",
  documentType: "supplier-decision-report",
  columns: [{ key: "item", header: "Stavka", dataType: "text" }],
  rows: [{ item: "Test", value: "1" }],
  filters: [],
  metadata: [
    { key: "recommendationAllowed", label: "Preporuka dozvoljena", value: false },
    { key: "dataScope", label: "Opseg podataka", value: "all" },
  ],
  locale: "sr-RS",
};

describe("SupplierDecisionReportActions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("hides PDF export action when feature is disabled", () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "false");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} />
      </MemoryRouter>
    );

    expect(screen.queryByRole("button", { name: "Izvezi PDF" })).not.toBeInTheDocument();
  });

  it("calls print preview action", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "true");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Štampaj izveštaj" }));

    await waitFor(() => {
      expect(printPreviewMock).toHaveBeenCalledTimes(1);
      expect(printPreviewMock).toHaveBeenCalledWith(payload);
    });
  });

  it("calls excel and pdf export actions", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "true");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Izvezi Excel" }));
    await waitFor(() => expect(exportExcelMock).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "Izvezi PDF" }));
    await waitFor(() => expect(exportPdfMock).toHaveBeenCalledTimes(1));
  });

  it("hides export actions when payload is missing", () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "true");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={null} />
      </MemoryRouter>
    );

    expect(screen.queryByRole("button", { name: "Štampaj izveštaj" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Kopiraj sažetak" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Izvezi CSV" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Izvezi Excel" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Izvezi PDF" })).not.toBeInTheDocument();
  });

  it("notifies caller on PDF export errors", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "true");
    exportPdfMock.mockRejectedValueOnce(new Error("PDF servis nije dostupan"));
    const onError = vi.fn();

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} onError={onError} />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Izvezi PDF" }));

    await waitFor(() => {
      expect(onError).toHaveBeenCalledWith("PDF izvoz trenutno nije dostupan. Koristite štampu ili Excel.");
    });
  });

  it("maps recommendationAllowed=false to signal review action", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "false");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} durableReportHref="/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30" />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Dodaj u akcije" }));

    await waitFor(() => {
      expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledTimes(1);
    });

    expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledWith(expect.objectContaining({
      sourceType: "supplier",
      title: "Proveri signal dobavljača",
      recommendationStatus: "SIGNAL_REVIEW",
      priority: "P2",
    }));
  });

  it("shows existing message when backend reports existing action", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "false");
    upsertAnalyticsActionWithResultMock.mockResolvedValueOnce({
      item: { sourceKey: "supplier:signal_check:all:unknown-period:all" },
      created: false,
      existing: true,
      status: "accepted",
      sourceKey: "supplier:signal_check:all:unknown-period:all",
    });

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} durableReportHref="/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30" />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Dodaj u akcije" }));

    await waitFor(() => {
      expect(screen.getByText("Akcija je već u centralnim akcijama.")).toBeInTheDocument();
    });
  });

  it("copies negotiation pack rows into summary text", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "false");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions
          payload={{
            ...payload,
            rows: [
              { section: "Header", item: "Dobavljač", value: "Alpha" },
              { section: "supplier_negotiation_pack", item: "Prihod", value: "520000", secondary: "Sažetak" },
              { section: "supplier_negotiation_pack", item: "Finalni savet", value: "Pojačaj saradnju", secondary: "Predlog razgovora" },
            ],
          }}
        />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Kopiraj sažetak" }));

    await waitFor(() => {
      expect(buildSummaryMock).toHaveBeenCalledTimes(1);
    });

    expect(buildSummaryMock.mock.calls[0][0].rows.some((row: { section?: string }) => row.section === "supplier_negotiation_pack")).toBe(true);
  });
});
