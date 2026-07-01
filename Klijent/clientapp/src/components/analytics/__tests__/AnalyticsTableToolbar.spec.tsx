import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AnalyticsTableToolbar from "../AnalyticsTableToolbar";
import { getPrintPayload } from "../../../services/analyticsTableState";
import { downloadExport, generateExport, requestPrintPreview, waitForExport } from "../../../services/exportApi";

vi.mock("../../../services/exportApi", async () => {
  const actual = await vi.importActual<typeof import("../../../services/exportApi")>("../../../services/exportApi");
  return {
    ...actual,
    generateExport: vi.fn(),
    requestPrintPreview: vi.fn(),
    waitForExport: vi.fn(),
    downloadExport: vi.fn(),
    resolveApiUrl: vi.fn((path: string) => `https://api.local${path}`),
  };
});

type TestRow = {
  supplier: string;
  revenue: number;
  hiddenInternal?: string;
};

const rows: TestRow[] = [
  { supplier: "Dobavljač A", revenue: 120000, hiddenInternal: "ignore-a" },
  { supplier: "Dobavljač B", revenue: 80000, hiddenInternal: "ignore-b" },
];

const columns = [
  { key: "supplier", header: "Dobavljač", dataType: "text" as const },
  { key: "revenue", header: "Prihod", dataType: "currency" as const, getValue: (row: TestRow) => row.revenue },
];

function renderToolbar() {
  return render(
    <AnalyticsTableToolbar
      tableKey="supplier-test"
      tableTitle="Supplier test"
      columns={columns}
      rows={rows}
      filters={[{ key: "period", label: "Period", value: "30d" }]}
      metadata={[{ key: "generatedAt", label: "Generisano", value: "2026-07-01" }]}
      defaultOrientation="landscape"
    />,
  );
}

describe("AnalyticsTableToolbar", () => {
  beforeEach(() => {
    vi.mocked(generateExport).mockReset();
    vi.mocked(requestPrintPreview).mockReset();
    vi.mocked(waitForExport).mockReset();
    vi.mocked(downloadExport).mockReset();
    vi.spyOn(window, "open").mockImplementation(() => null);
  });

  it("prints the exact resolved table payload through local print state", () => {
    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Štampaj/i }));

    expect(window.open).toHaveBeenCalledTimes(1);
    const [url] = vi.mocked(window.open).mock.calls[0];
    expect(String(url)).toContain("/print/analytics/supplier-test?stateKey=");

    const stateKey = new URL(String(url), "http://localhost").searchParams.get("stateKey");
    const payload = getPrintPayload(stateKey);

    expect(payload).not.toBeNull();
    expect(payload?.tableKey).toBe("supplier-test");
    expect(payload?.rows).toEqual([
      { supplier: "Dobavljač A", revenue: 120000 },
      { supplier: "Dobavljač B", revenue: 80000 },
    ]);
    expect(payload?.filters).toEqual([{ key: "period", label: "Period", value: "30d" }]);
    expect(payload?.metadata).toEqual([{ key: "generatedAt", label: "Generisano", value: "2026-07-01" }]);
  });

  it("opens the export menu and sends sync Excel export with table rows, filters and metadata", async () => {
    vi.mocked(generateExport).mockResolvedValue({
      isAsync: false,
      downloadUrl: "/exports/supplier-test.xlsx",
      fileName: "supplier-test.xlsx",
    });

    renderToolbar();

    expect(screen.getByText("Redova: 2")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    expect(screen.getByText("Izveštaj za menadžment i štampu")).toBeInTheDocument();
    expect(screen.getByText("Tabela za dalju analizu")).toBeInTheDocument();
    expect(screen.getByText("Brz flat-file izvoz")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Excel/i }));
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
    expect(screen.getByText("Premium analytics export")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    await waitFor(() => expect(generateExport).toHaveBeenCalledTimes(1));
    expect(generateExport).toHaveBeenCalledWith(
      expect.objectContaining({
        tableKey: "supplier-test",
        rows: [
          { supplier: "Dobavljač A", revenue: 120000 },
          { supplier: "Dobavljač B", revenue: 80000 },
        ],
        filters: [{ key: "period", label: "Period", value: "30d" }],
        metadata: [{ key: "generatedAt", label: "Generisano", value: "2026-07-01" }],
      }),
      expect.objectContaining({
        format: "xlsx",
        orientation: "landscape",
        includeFiltersAndMetadata: true,
      }),
    );
    expect(downloadExport).toHaveBeenCalledWith("/exports/supplier-test.xlsx", "supplier-test.xlsx");
    expect(screen.getByText("Eksport je preuzet.")).toBeInTheDocument();
  });

  it("routes PDF preview through print preview instead of direct export", async () => {
    vi.mocked(requestPrintPreview).mockResolvedValue({ printUrl: "/print-preview/123" });

    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("button", { name: /^PDF/i }));
    fireEvent.click(screen.getByRole("button", { name: /Otvori preview/i }));

    await waitFor(() => expect(requestPrintPreview).toHaveBeenCalledTimes(1));
    expect(generateExport).not.toHaveBeenCalled();
    expect(window.open).toHaveBeenCalledWith("https://api.local/print-preview/123", "_blank", "noopener");
  });

  it("waits for async exports before downloading the completed document", async () => {
    vi.mocked(generateExport).mockResolvedValue({ isAsync: true, documentId: "doc-1" });
    vi.mocked(waitForExport).mockResolvedValue({ downloadUrl: "/exports/ready.csv", fileName: "ready.csv" });

    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("button", { name: /CSV/i }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    await waitFor(() => expect(waitForExport).toHaveBeenCalledWith("doc-1"));
    expect(downloadExport).toHaveBeenCalledWith("/exports/ready.csv", "ready.csv");
    expect(screen.getByText("Eksport je završen i preuzet.")).toBeInTheDocument();
  });
});
