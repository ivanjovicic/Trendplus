import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionReportActions from "../SupplierDecisionReportActions";

const exportExcelMock = vi.fn(() => Promise.resolve());
const exportPdfMock = vi.fn(() => Promise.resolve());
const printPreviewMock = vi.fn(() => Promise.resolve());
const exportCsvMock = vi.fn(() => undefined);
const buildSummaryMock = vi.fn(() => "summary");

vi.mock("../../../services/supplierDecisionReport", () => ({
  exportSupplierDecisionReportExcel: (...args: unknown[]) => exportExcelMock(...args),
  exportSupplierDecisionReportPdf: (...args: unknown[]) => exportPdfMock(...args),
  openSupplierDecisionPrintPreview: (...args: unknown[]) => printPreviewMock(...args),
  exportSupplierDecisionReportCsv: (...args: unknown[]) => exportCsvMock(...args),
  buildSupplierDecisionReportSummaryText: (...args: unknown[]) => buildSummaryMock(...args),
}));

const payload = {
  tableKey: "supplier-decision-report",
  tableTitle: "Trendplus Supplier Decision Report",
  documentType: "supplier-decision-report",
  columns: [{ key: "item", header: "Stavka", dataType: "text" }],
  rows: [{ item: "Test", value: "1" }],
  filters: [],
  metadata: [],
  locale: "sr-RS",
};

describe("SupplierDecisionReportActions", () => {
  it("hides PDF export action when feature is disabled", () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "false");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} />
      </MemoryRouter>
    );

    expect(screen.queryByRole("button", { name: "Export PDF" })).not.toBeInTheDocument();
  });

  it("calls print preview action", async () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "true");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={payload} />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Print izvestaj" }));

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

    fireEvent.click(screen.getByRole("button", { name: "Export Excel" }));
    await waitFor(() => expect(exportExcelMock).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "Export PDF" }));
    await waitFor(() => expect(exportPdfMock).toHaveBeenCalledTimes(1));
  });

  it("disables actions when payload is missing", () => {
    vi.stubEnv("VITE_ENABLE_PDF_EXPORT", "true");

    render(
      <MemoryRouter>
        <SupplierDecisionReportActions payload={null} />
      </MemoryRouter>
    );

    expect(screen.getByRole("button", { name: "Print izvestaj" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Kopiraj sazetak" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Export CSV" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Export Excel" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Export PDF" })).toBeDisabled();
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

    fireEvent.click(screen.getByRole("button", { name: "Export PDF" }));

    await waitFor(() => {
      expect(onError).toHaveBeenCalledWith("PDF export trenutno nije dostupan. Koristite Print izvestaj ili Export Excel.");
    });
  });
});
